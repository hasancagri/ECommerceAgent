namespace Catalog.Api.Extensions;

// Catalog kalıcılık kurulumu: Marten (Postgres document store) + aggregate şemaları/index'leri +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddCatalogMarten(this WebApplicationBuilder builder)
    {
        var catalogDb = builder.Configuration.GetConnectionString("catalogDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.CatalogSchemaName;
                opts.Connection(catalogDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s =>
                    {
                        s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
                    });

                // 096: pgvector extension kaydı + Npgsql vector type handler. Embedding JSONB içinde
                // float[] yaşar; HNSW/kNN index BİLİNÇLİ YOK (R2 — tüketici yüzey yok, 11.4k satırda
                // exact scan ms mertebesi; index kararı arama spec'inin işi).
                opts.UsePgVector();
                // Üretim/yenileme tek yazardan (EmbeddingReconcileProcess) — concurrency kaydı gerekmez.
                opts.Schema.For<Catalog.Api.Embeddings.ProductEmbedding>();

                // Gtin (ISBN) ürün kimlik/lookup anahtarıdır (095: Sku söküldü, FamilyCode kalktı).
                opts.Schema.For<Product>().Index(x => x.Gtin);

                // 095: ProductTag aggregate'i SÖKÜLDÜ (model + şema; eski tablo dev full-reset'le gider).

                // fiyat geçmişi append-only kaydı — ürün bazlı okuma için lookup index'i.
                opts.Schema.For<ProductPriceChange>().Index(x => x.ProductId);

                // NormalizedName teklik anahtarıdır (R4) — computed unique index son güvence.
                // Legacy Brand migrasyonu YOK (kullanıcı kararı): DB sıfırlanarak başlatılır, katalog feed'den dolar.
                opts.Schema.For<Category>().UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);
                // Brand→Author rename + yeni Publisher — ikisi de NormalizedName teklik anahtarı (get-or-create güvencesi).
                opts.Schema.For<Catalog.Api.Domains.Authors.Author>()
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);
                opts.Schema.For<Catalog.Api.Domains.Publishers.Publisher>()
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);

                // SpecificationAttribute registry SÖKÜLDÜ — facet sistemi kaldırıldı.

                // 091: ISBN=Gtin kimlik — seed satır-satır lookup'ı index'ten döner (19.7k × tarama değil)
                // + çift-ISBN'e DB-seviyesi guard (admin_create_product yarışı dahil). Null Gtin serbest
                // (Postgres unique NULL'ları saymaz).
                opts.Schema.For<Catalog.Api.Domains.Products.Product>()
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.Gtin!);

                // 091: ImportSession/ImportRow şemaları SÖKÜLDÜ (Excel import gitti). Eski tablolar
                // dev full-reset'le gider (ApplyAllDatabaseChangesOnStartup tablo düşürmez).
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}