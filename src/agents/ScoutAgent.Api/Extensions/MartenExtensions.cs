namespace ScoutAgent.Api.Extensions;

// ScoutAgent kalıcılık: Marten (scoutDb — kendi deposu, İlke I) + Wolverine entegrasyonu.
// Conversation belgesi için DocumentAlias ELLE (lowercase, I'sız) — tr-TR ı tuzağı (mt_doc_conversatıon
// crashloop'u önler). UserId index'i: geçmiş listeleme yalnız kendi sohbetleri (FR-010).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddScoutMarten(this WebApplicationBuilder builder)
    {
        var scoutDb = builder.Configuration.GetConnectionString("scoutDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = ScoutSchemaConstants.SchemaName;
                opts.Connection(scoutDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

                opts.Schema.For<Conversation>()
                    .DocumentAlias(ScoutSchemaConstants.ConversationAlias)
                    .Index(x => x.UserId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
