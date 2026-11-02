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

                // Ürün başına TEK stok kaydı (002 FR-001) — DB-seviyesi guard.
                opts.Schema.For<StockItem>()
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.ProductId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
