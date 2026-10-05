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
                // Wallet (kart-saklama) SÖKÜLDÜ; Customer BC = AddressBook + MerchantInformation.
                opts.Schema.For<Customer.Api.Domains.AddressBooks.AddressBook>().Index(x => x.UserId);
                // Merchant kimliği (tekil kayıt) — merchant onboarding/admin.
                opts.Schema.For<Customer.Api.Domains.MerchantInformations.MerchantInformation>();
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
