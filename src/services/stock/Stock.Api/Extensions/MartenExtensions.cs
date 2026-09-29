namespace Stock.Api.Extensions;

// Stock kalıcılık kurulumu: Marten (Postgres document store) + aggregate şemaları/index'leri +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddStockMarten(this WebApplicationBuilder builder)
    {
        var stockDb = builder.Configuration.GetConnectionString("stockDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.StockSchemaName;
                opts.Connection(stockDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
                // son-urun yarisi optimistic concurrency ile cozulur (cift satis yok / SC-001).
                opts.Schema.For<ProductStock>().Index(x => x.ProductId).UseOptimisticConcurrency(true);

                // barkod ↔ ProductId eşlemesi (Catalog ProductAdded yazar).
                opts.Schema.For<BarcodeLink>();
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
