namespace Basket.Api.Extensions;

// Basket kalıcılık kurulumu: Marten (Postgres document store) + aggregate şeması/index'i +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddBasketMarten(this WebApplicationBuilder builder)
    {
        var basketDb = builder.Configuration.GetConnectionString("basketDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.BasketSchemaName;
                opts.Connection(basketDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
                opts.Schema.For<Basket.Api.Domains.Baskets.Basket>().Index(x => x.UserId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
