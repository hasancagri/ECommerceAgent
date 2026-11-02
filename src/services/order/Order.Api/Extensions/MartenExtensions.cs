namespace Order.Api.Extensions;

// Order kalıcılık kurulumu: Marten (Postgres document store) + aggregate şemaları/index'leri +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddOrderMarten(this WebApplicationBuilder builder)
    {
        var orderDb = builder.Configuration.GetConnectionString("orderDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.OrderSchemaName;
                opts.Connection(orderDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s =>
                    {
                        s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
                    });

                opts.Schema.For<Order.Api.Domains.Orders.Order>().Index(x => x.BuyerId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
