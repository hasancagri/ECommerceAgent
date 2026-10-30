using System.Text.Json;
using Catalog.Api.Options;

namespace Catalog.Api.Seeding;

// 091: açılış katalog seed'i; 095: İKİ FAZLI — Faz A kapalı kategori ağacını kurar (tüm yollardan
// deterministik, FR-007), Faz B satır satır ürün (insert-or-ignore: Gtin varsa ATLA — var olan ürün
// admin'indir, UPSERT bilinçli reddedildi). Kayıt başına ürün + ProductAdded + ProductChangedEvent
// satırın KENDİ commit'inde ([Transactional] SeedBookRow — outbox atomik). Yarıda kesilme = sonraki
// açılış kaldığı yerden; hız ikincil.
// TEK SEFERLİK: ExecuteAsync dosyayı işleyip RETURN eder; ikinci açılış "hepsi var" deyip biter.
// K7: dosya yoksa bilgi-logla atla (prod/seed-kapalı meşru); parse/ağaç hatası fırlar → host durur
// (fail-fast, bozuk commit'li veri sessiz geçilmez). Kullanıcı tetiklemez → Domains/ dışı (Seeding/).
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

        using var scope = services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        // Faz A: kapalı kategori ağacı — tüm yollar (primary + related) + "Genel" fallback kökü.
        // Ad-çakışması/seviye ihlali CategoryTreeSeed.Build'den fırlar → host durur (deterministik hata).
        var paths = records.SelectMany(r => r.RelatedCategoryPaths.Prepend(r.CategoryPath))
            .Append(["Genel"])
            .ToList();
        var treeCreated = await bus.InvokeAsync<int>(
            new SeedCategoryTree.SeedCategoryTreeCommand(paths), stoppingToken);
        logger.LogInformation("Catalog seed kategori ağacı hazır: {Created} yeni düğüm.", treeCreated);

        // Faz B: satır satır ürün.
        int created = 0, skipped = 0, invalid = 0;
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

// 095 Faz A: kapalı kategori ağacını kurar (yalnız seed'den — FR-007). [Transactional] tek commit;
// var olan düğüme dokunmaz (normalize ad eşleşmesi), yalnız eksik düğümü ekler. Yeni düğüm doğduysa
// handler-içi atomik cache boşaltma (090 idiomu).
public static class SeedCategoryTree
{
    public record SeedCategoryTreeCommand(List<List<string>> Paths);

    [Transactional]
    public class SeedCategoryTreeCommandHandler
    {
        public async Task<int> Handle(
            SeedCategoryTreeCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var nodes = CategoryTreeSeed.Build(cmd.Paths);

            var existing = await session.Query<Category>().ToListAsync(ct);
            var byNormalized = existing.ToDictionary(c => c.NormalizedName);

            var created = 0;
            foreach (var node in nodes)
            {
                var key = NameNormalization.Normalize(node.Name);
                if (byNormalized.ContainsKey(key))
                    continue;

                Guid? parentId = node.ParentName is null
                    ? null
                    : byNormalized[NameNormalization.Normalize(node.ParentName)].Id;
                var category = Category.Create(node.Name, parentId).Data!;
                session.Store(category);
                byNormalized[key] = category;
                created++;
            }

            if (created > 0)
                await bus.PublishAsync(new CacheInvalidationRequested(CatalogCacheTags.AgentLists));

            return created;
        }
    }
}

// Tek seed satırını ürüne çevirir (insert-or-ignore). [Transactional] — ürün Store + ProductAdded +
// ProductChangedEvent AYNI Marten commit (Wolverine outbox atomik). ISBN üründe VARSA no-op (false —
// var olan ürün admin'indir, seed dokunmaz). 095: kategori get-or-create YOK — leaf kapalı ağaçtan
// (Faz A) çözülür; çözülemeyen primary → "Genel", çözülemeyen related → atlanır (satır reddedilmez).
public static class SeedBookRow
{
    public record SeedBookRowCommand(SeedBookRecord Record);

    [Transactional]
    public class SeedBookRowCommandHandler
    {
        // Get-or-create helper'ları YENİ referans (yazar/yayınevi) yazınca işaretler.
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
            await ApplyAuthorBiosAsync(session, authors, record.AuthorBios);
            var publisher = await GetOrCreatePublisherAsync(session, Fallback(record.Publisher, "Unknown"), ct);

            // Primary leaf: [üst] → üst, [üst, leaf] → leaf; boş/çözülemeyen → "Genel" (Faz A garanti kökü).
            var leaf = await FindLeafAsync(session, record.CategoryPath, ct)
                       ?? await FindByNameAsync(session, "Genel", ct)
                       ?? throw new InvalidOperationException("Kategori ağacı kurulmamış: 'Genel' kökü yok (Faz A koşmadı mı?).");

            var product = Product.Create(record.Title, record.Isbn, price, record.Description).Data!;
            product.SetAuthors(authors.Select(a => a.Id));
            product.SetPublisher(publisher.Id);
            product.SetImage(null); // kapak async File.Api'den (CoverIngested)
            product.SetBookMeta(record.PageCount, record.PublishedOn, record.Language);
            product.SetTranslators(record.Translators);
            product.SetCategory(leaf.Id);

            // İlgili kategoriler (editoryal): leaf'leri çöz, çözülemeyeni atla (satır reddedilmez).
            var relatedIds = new List<Guid>();
            foreach (var relatedPath in record.RelatedCategoryPaths)
            {
                var related = await FindLeafAsync(session, relatedPath, ct);
                if (related is not null)
                    relatedIds.Add(related.Id);
            }
            product.SetRelatedCategories(relatedIds); // dedup + primary'yi eleme aggregate'te
            session.Store(product);

            // Fiyat geçmişin ilk satırı (OldPrice=null) — fiyat>0 garantili.
            session.Store(ProductPriceChange.Create(product.Id, oldPrice: null, price.Amount, DateTime.UtcNow));

            // Yayın-anı zincir çözümü (leaf + ata; ad VE id) — FileConsumers ile ortak tek kaynak.
            var chainMap = new Dictionary<Guid, Category> { [leaf.Id] = leaf };
            if (leaf.ParentCategoryId is Guid parentId
                && await session.LoadAsync<Category>(parentId, ct) is { } parent)
                chainMap[parentId] = parent;
            var (categoryIds, categoryNames) = CategoryNaming.ResolveChain(leaf.Id, chainMap);

            // İki event = iki hat: ProductAdded kimlik/kapak (File kapak çözer), ProductChangedEvent
            // vitrin (Library tüketir). Categories/CategoryIds = leaf + ata (related GİRMEZ).
            await bus.PublishAsync(new IntegrationEvents.ProductAdded(record.Isbn, product.Id));
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.Description, price.Amount,
                authors.Select(a => new IntegrationEvents.AuthorRef(a.Id, a.Name)).ToList(),
                publisher.Id, publisher.Name,
                leaf.Id, leaf.Name,
                product.ImageUrl, IsDeleted: false,
                Categories: categoryNames,
                CategoryIds: categoryIds));

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

        // Kanonik yolun leaf'ini ([üst] → üst, [üst, leaf] → leaf) kapalı ağaçtan çözer; yoksa null.
        private static async Task<Category?> FindLeafAsync(
            IDocumentSession session, List<string> path, CancellationToken ct)
        {
            var leafName = path.LastOrDefault(s => !string.IsNullOrWhiteSpace(s));
            return leafName is null ? null : await FindByNameAsync(session, leafName, ct);
        }

        private static async Task<Category?> FindByNameAsync(
            IDocumentSession session, string name, CancellationToken ct)
        {
            var normalized = NameNormalization.Normalize(name);
            return await session.Query<Category>().FirstOrDefaultAsync(c => c.NormalizedName == normalized, ct);
        }

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

        // Yazar biyografisini uygular (Author.SetBio: boş/dolu no-op, ilk-dolu kazanır). Yalnız gerçekten
        // değişen yazarı yeniden Store eder (mevcut yazar admin'inkiyse ve bio zaten varsa dokunmaz).
        private static async Task ApplyAuthorBiosAsync(
            IDocumentSession session, List<Author> authors, Dictionary<string, string> bios)
        {
            foreach (var author in authors)
            {
                if (!bios.TryGetValue(author.Name, out var bio) || string.IsNullOrWhiteSpace(bio))
                    continue;
                var before = author.Bio;
                author.SetBio(bio);
                if (author.Bio != before)
                    session.Store(author);
            }
            await Task.CompletedTask;
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
    }
}
