namespace Personalization.Api.Extensions;

// Personalization kalıcılık kurulumu: Marten (Postgres document store) + Wolverine entegrasyonu.
// Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı). Append-only arama logu (UserSearchRecord).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddPersonalizationMarten(this WebApplicationBuilder builder)
    {
        var personalizationDb = builder.Configuration.GetConnectionString("personalizationDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.PersonalizationSchemaName;
                opts.Connection(personalizationDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

                // Append-only arama logu; UserId index (sonra kullanıcı-bazlı okuma/ML türetimi).
                opts.Schema.For<UserSearchRecord>()
                    .DocumentAlias("user_search_record")
                    .Index(x => x.UserId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
