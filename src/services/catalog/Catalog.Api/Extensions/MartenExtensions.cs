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

                // Gtin (barkod) ürün lookup/teklik anahtarıdır — lookup index'i.
                // FamilyCode agent okumaları için ucuz lookup index'i (gruplama Storefront'ta).
                opts.Schema.For<Product>().Index(x => x.Gtin).Index(x => x.FamilyCode);

                // K9: ProductTag yeni aggregate — dış yüzeyi yok, şemada yaşar (besleyen akış 041+).
                opts.Schema.For<ProductTag>();

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

                // özellik registry'si — NormalizedName teklik anahtarı (seed get-or-create güvencesi).
                opts.Schema.For<Catalog.Api.Domains.SpecificationAttributes.SpecificationAttribute>()
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.NormalizedName);

                // Excel import staging + capability token. Token teklik anahtarı (link=yetki); ImportRow
                // ISBN idempotency + Status processor sorgusu (WHERE Status=Pending) için lookup index'i.
                // DocumentAlias ZORUNLU: tip adı büyük I ile başlıyor, tr-TR makinede varsayılan alias
                // lowercase'i noktasız ı üretir → computed-index delta her boot bozulur (42P07). ascii alias baypas.
                opts.Schema.For<Catalog.Api.Import.ImportSession>()
                    .DocumentAlias("importsession")
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.Token);
                opts.Schema.For<Catalog.Api.Import.ImportRow>()
                    .DocumentAlias("importrow")
                    .Index(x => x.Isbn).Index(x => x.Status);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}