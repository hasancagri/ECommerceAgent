using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Catalog.Api.Domains.Authors;
using Catalog.Api.Domains.Publishers;
using Catalog.Api.Domains.Categories;

namespace Catalog.Api.Import;

// T013/FR-003+FR-004: staging'deki bekleyen satırları arka planda ürüne çeviren dayanıklı süreç
// (Process/ deseni — BC'nin KENDİ süreci, kullanıcı tetiklemez). WHERE Status=Pending çek (Excel değil
// tablo sorgusu — FR-002); her satırı [Transactional] ProcessImportRow ile işler. Çökme = rollback,
// satır Pending kalır, tekrar işlenir (exactly-once; ISBN idempotency).
public class ImportProcessor(IServiceProvider services, ILogger<ImportProcessor> logger) : BackgroundService
{
    private const int BatchSize = 200;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var query = scope.ServiceProvider.GetRequiredService<IQuerySession>();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

                var pendingIds = await query.Query<ImportRow>()
                    .Where(r => r.Status == ImportRowStatus.Pending)
                    .OrderBy(r => r.CreatedTime)
                    .Select(r => r.Id)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (pendingIds.Count == 0)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                    continue;
                }

                foreach (var id in pendingIds)
                    await bus.InvokeAsync(new ProcessImportRow.ProcessImportRowCommand(id), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // graceful shutdown — bkz. HttpClient timeout StopHost tuzağı (stoppingToken ayrımı)
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import processor batch failed; retrying after delay.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }
}

// T013: tek satırı ürüne çevirir. [Transactional] — ürün Store + ProductAdded publish + ImportRow
// MarkProcessed AYNI Marten commit (Wolverine outbox atomik). ISBN üründe VARSA ürün oluşturulmaz ama
// satır yine Processed (additive-only atla, FR-005). Draft doğar (FR-006): Publish YOK, ProductChangedEvent
// YAYILMAZ; ProductAdded (stok) yayılır ki Stock BarcodeLink+OnHand satırı doğsun (AdminCreateProduct doktrini).
public static class ProcessImportRow
{
    public record ProcessImportRowCommand(Guid RowId);

    [Transactional]
    public class ProcessImportRowCommandHandler
    {
        // Get-or-create helper'ları YENİ referans (yazar/yayınevi/kategori) yazınca işaretler.
        private bool _createdReference;

        public async Task Handle(
            ProcessImportRowCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var row = await session.LoadAsync<ImportRow>(cmd.RowId, ct);
            if (row is null || row.Status != ImportRowStatus.Pending)
                return; // yeniden teslim / zaten işlenmiş — no-op (exactly-once)

            // Idempotency: ISBN üründe varsa ürün oluşturma; satırı işlenmiş say (additive-only atla).
            var existing = await session.Query<Product>().FirstOrDefaultAsync(p => p.Gtin == row.Isbn, ct);
            if (existing is not null)
            {
                row.MarkProcessed(existing.Id);
                session.Store(row);
                return;
            }

            var price = Money.Create(row.PriceTry) ?? Money.Zero();

            var authors = await GetOrCreateAuthorsAsync(session, SplitAuthors(row.Authors), ct);
            var publisher = await GetOrCreatePublisherAsync(session, Fallback(row.Publisher, "Unknown"), ct);
            var mid = await GetOrCreateCategoryAsync(session, Fallback(row.CategoryMid, "Genel"), parentId: null, ct);
            var leafName = Fallback(row.CategoryLeaf, mid.Name);
            var leaf = NameNormalization.Normalize(leafName) == mid.NormalizedName
                ? mid
                : await GetOrCreateCategoryAsync(session, leafName, parentId: mid.Id, ct);

            // TASLAK doğar (Publish YOK). SKU=ISBN (ImportBook emsali). ImageUrl boş — kapak async File.Api'den.
            var product = Product.Create(row.Title, row.Isbn, ProductType.Simple, price, "", row.Description);
            product.SetIdentifiers(row.Isbn, gtin: row.Isbn, manufacturerPartNumber: null);
            product.SetAuthors(authors.Select(a => a.Id));
            product.SetPublisher(publisher.Id);
            product.SetImage(null);
            product.AssignToCategory(leaf.Id, isFeatured: false, displayOrder: 0);
            session.Store(product);

            // FR-013: fiyat>0 ise geçmişin ilk satırı (OldPrice=null); fiyatsız taslak satır düşürmez.
            if (price.Amount > 0)
                session.Store(ProductPriceChange.Create(product.Id, oldPrice: null, price.Amount, DateTime.UtcNow));

            // Stock BarcodeLink+OnHand satırı YALNIZ bu event'ten doğar (draft dahil) + File.Api kapak çözer.
            // ProductChangedEvent YAYILMAZ (draft; vitrine publish_imported ya da CoverIngested sokar).
            await bus.PublishAsync(new IntegrationEvents.ProductAdded(row.Isbn, product.Id, row.Stock));

            // Decorator-dışı yazma yolu: [InvalidatesCache] burada İŞLEMEZ (attribute yalnız InvokeAsync<T>
            // sarmalında okunur). Yeni referans doğduysa agent-lists elle boşaltılır — [Transactional]
            // handler içinden publish = gerçek outbox atomikliği.
            if (_createdReference)
                await bus.PublishAsync(new CacheInvalidationRequested(CatalogCacheTags.AgentLists));

            row.MarkProcessed(product.Id);
            session.Store(row);
        }

        private static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string[] SplitAuthors(string authors)
        {
            var names = (authors ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return names.Length == 0 ? ["Unknown"] : names;
        }

        // Aşağıdakiler ImportBook'un get-or-create yardımcılarının ikizi (051 seeder söküldü — bilinçli tekrar).
        private async Task<List<Author>> GetOrCreateAuthorsAsync(
            IDocumentSession session, string[] names, CancellationToken ct)
        {
            var result = new List<Author>();
            var seen = new HashSet<string>();
            foreach (var name in names)
            {
                var normalized = NameNormalization.Normalize(name);
                if (!seen.Add(normalized))
                    continue;
                var author = result.FirstOrDefault(a => a.NormalizedName == normalized)
                             ?? await session.Query<Author>().FirstOrDefaultAsync(a => a.NormalizedName == normalized, ct);
                if (author is null)
                {
                    author = Author.Create(name).Data!;
                    session.Store(author);
                    _createdReference = true;
                }
                result.Add(author);
            }
            return result;
        }

        private async Task<Publisher> GetOrCreatePublisherAsync(
            IDocumentSession session, string name, CancellationToken ct)
        {
            var normalized = NameNormalization.Normalize(name);
            var existing = await session.Query<Publisher>().FirstOrDefaultAsync(p => p.NormalizedName == normalized, ct);
            if (existing is not null)
                return existing;

            var publisher = Publisher.Create(name).Data!;
            session.Store(publisher);
            _createdReference = true;
            return publisher;
        }

        private async Task<Category> GetOrCreateCategoryAsync(
            IDocumentSession session, string name, Guid? parentId, CancellationToken ct)
        {
            var normalized = NameNormalization.Normalize(name);
            var existing = await session.Query<Category>().FirstOrDefaultAsync(c => c.NormalizedName == normalized, ct);
            if (existing is not null)
                return existing;

            var category = Category.Create(name, parentCategoryId: parentId).Data!;
            category.SetPublished(true);
            session.Store(category);
            _createdReference = true;
            return category;
        }
    }
}
