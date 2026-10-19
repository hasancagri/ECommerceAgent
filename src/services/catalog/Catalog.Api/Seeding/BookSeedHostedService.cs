using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Catalog.Api.Domains.Authors;
using Catalog.Api.Domains.Publishers;
using Catalog.Api.Domains.Categories;
using Catalog.Api.Options;

namespace Catalog.Api.Seeding;

// 091: açılış katalog seed'i (051 BookImportHostedService'in dönüşü; 083 Excel import söküldü).
// Commit'li books.json'u okur, SATIR SATIR insert-or-ignore (kullanıcı kararı: UPSERT DEĞİL — var olan
// ürüne dokunulmaz, yoksa admin MCP düzenlemesinin kıymeti kalmazdı; seed yalnız eksik ürünü ekler).
// Kayıt başına Gtin lookup → varsa atla, yoksa ürün + ProductAdded + ProductChangedEvent satırın KENDİ
// commit'inde ([Transactional] SeedBookRow — outbox atomik). Yarıda kesilme = sonraki açılış kaldığı
// yerden; hız ikincil.
// TEK SEFERLİK: ExecuteAsync dosyayı işleyip RETURN eder — döngü/poll yok (ImportProcessor'ın sonsuz
// döngüsünün aksine); ikinci açılış "hepsi var" deyip biter.
// K7: dosya yoksa bilgi-logla atla (prod/seed-kapalı meşru); parse hatası fırlar → host durur (fail-fast,
// bozuk commit'li veri sessiz geçilmez). Kullanıcı tetiklemez → Domains/ dışı (Seeding/).
public class BookSeedHostedService(
    IServiceProvider services,
    SeedOptions options,
    ILogger<BookSeedHostedService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.SourceJsonPath))
        {
            logger.LogInformation("Catalog seed atlandı (Enabled={Enabled}, yol boş mu={PathEmpty}).",
                options.Enabled, string.IsNullOrWhiteSpace(options.SourceJsonPath));
            return;
        }

        if (!File.Exists(options.SourceJsonPath))
        {
            logger.LogInformation("Catalog seed atlandı — dosya yok: {Path}.", options.SourceJsonPath);
            return;
        }

        // Parse hatası BİLEREK yakalanmaz: BackgroundService istisnası host'u durdurur (fail-fast, K7).
        List<SeedBookRecord> records;
        await using (var stream = File.OpenRead(options.SourceJsonPath))
            records = await JsonSerializer.DeserializeAsync<List<SeedBookRecord>>(stream, JsonOptions, stoppingToken) ?? [];

        logger.LogInformation("Catalog seed başladı: {Count} kayıt ({Path}).", records.Count, options.SourceJsonPath);

        int created = 0, skipped = 0, invalid = 0;
        using var scope = services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        foreach (var record in records)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            var validation = record.Validate();
            if (!validation.IsSuccess)
            {
                invalid++;
                logger.LogWarning("Seed satırı atlandı (ISBN={Isbn}): {Code}.",
                    record.Isbn, validation.Messages?.FirstOrDefault()?.Code);
                continue;
            }

            if (await bus.InvokeAsync<bool>(new SeedBookRow.SeedBookRowCommand(record), stoppingToken))
                created++;
            else
                skipped++;

            var done = created + skipped + invalid;
            if (done % 1000 == 0)
                logger.LogInformation("Catalog seed ilerleme: {Done}/{Total}.", done, records.Count);
        }

        logger.LogInformation("Catalog seed bitti: {Created} eklendi, {Skipped} zaten vardı, {Invalid} geçersiz.",
            created, skipped, invalid);
    }
}

// Tek seed satırını ürüne çevirir (insert-or-ignore). [Transactional] — ürün Store + ProductAdded +
// ProductChangedEvent AYNI Marten commit (Wolverine outbox atomik). ISBN üründe VARSA no-op (false —
// var olan ürün admin'indir, seed dokunmaz). Get-or-create mantığı sökülen ImportProcessor'dan devralındı.
public static class SeedBookRow
{
    public record SeedBookRowCommand(SeedBookRecord Record);

    [Transactional]
    public class SeedBookRowCommandHandler
    {
        // Get-or-create helper'ları YENİ referans (yazar/yayınevi/kategori) yazınca işaretler.
        private bool _createdReference;

        public async Task<bool> Handle(
            SeedBookRowCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var record = cmd.Record;

            // Satır satır idempotency: ISBN=Gtin üründe varsa dokunma (ikinci açılış / yarım seed devamı
            // / admin'in düzenlediği ürün). Gtin unique-index'li (MartenExtensions).
            var existing = await session.Query<Product>().FirstOrDefaultAsync(p => p.Gtin == record.Isbn, ct);
            if (existing is not null)
                return false;

            var price = Money.Create(record.PriceTry)!; // Validate() fiyat>0'ı garantiledi

            var authors = await GetOrCreateAuthorsAsync(session, NormalizeAuthors(record.Authors), ct);
            var publisher = await GetOrCreatePublisherAsync(session, Fallback(record.Publisher, "Unknown"), ct);
            var mid = await GetOrCreateCategoryAsync(session, Fallback(record.CategoryMid, "Genel"), parentId: null, ct);
            var leafName = Fallback(record.CategoryLeaf, mid.Name);
            var leaf = NameNormalization.Normalize(leafName) == mid.NormalizedName
                ? mid // leaf==mid tuzağı: aynı ada ikinci Store unique ihlali üretir
                : await GetOrCreateCategoryAsync(session, leafName, parentId: mid.Id, ct);

            // SKU=ISBN (ImportBook emsali). ImageUrl boş — kapak async File.Api'den (CoverIngested).
            var product = Product.Create(record.Title, record.Isbn, ProductType.Simple, price, "", record.Description).Data!;
            product.SetIdentifiers(record.Isbn, gtin: record.Isbn, manufacturerPartNumber: null);
            product.SetAuthors(authors.Select(a => a.Id));
            product.SetPublisher(publisher.Id);
            product.SetImage(null);
            product.AssignToCategory(leaf.Id, isFeatured: false, displayOrder: 0);
            session.Store(product);

            // Fiyat geçmişin ilk satırı (OldPrice=null) — fiyat>0 garantili.
            session.Store(ProductPriceChange.Create(product.Id, oldPrice: null, price.Amount, DateTime.UtcNow));

            // İki event = iki hat (kullanıcı onayı): ProductAdded envanter/kimlik (Stock BarcodeLink+OnHand
            // satır stok değeriyle doğar; File kapak çözer), ProductChangedEvent vitrin (Storefront/Library/Discount).
            await bus.PublishAsync(new IntegrationEvents.ProductAdded(record.Isbn, product.Id, record.Stock));
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.FullDescription, price.Amount,
                authors.Select(a => new IntegrationEvents.AuthorRef(a.Id, a.Name)).ToList(),
                publisher.Id, publisher.Name,
                leaf.Id, leaf.Name,
                product.ImageUrl, IsDeleted: false, OldPrice: null));

            // Tek idiom (090): boşaltma HER yazma yolunda handler-içi publish — yeni referans
            // doğduysa agent-lists boşaltılır; [Transactional] içinden publish = outbox atomikliği.
            if (_createdReference)
                await bus.PublishAsync(new CacheInvalidationRequested(CatalogCacheTags.AgentLists));

            return true;
        }

        private static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string[] NormalizeAuthors(List<string> authors)
        {
            var names = authors.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToArray();
            return names.Length == 0 ? ["Unknown"] : names;
        }

        // Sökülen ProcessImportRow'un get-or-create yardımcılarının devamı (091'de buraya taşındı).
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