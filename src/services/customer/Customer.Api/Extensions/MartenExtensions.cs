namespace Customer.Api.Extensions;

// Customer kalıcılık kurulumu: Marten (Postgres document store) + aggregate şemaları/index'leri +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddCustomerMarten(this WebApplicationBuilder builder)
    {
        var customerDb = builder.Configuration.GetConnectionString("customerDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.CustomerSchemaName;
                opts.Connection(customerDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);
                // Wallet (076) + MerchantInformation (093 PG pivotu) SÖKÜLDÜ; Customer BC = AddressBook.
                opts.Schema.For<Customer.Api.Domains.AddressBooks.AddressBook>().Index(x => x.UserId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
