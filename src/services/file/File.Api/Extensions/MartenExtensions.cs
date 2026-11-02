using FileApi.Domains.FileAsset;

namespace File.Api.Extensions;

// File kalıcılık kurulumu: Marten (Postgres document store) = FileAsset kayıt defteri + Wolverine
// entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddFileMarten(this WebApplicationBuilder builder)
    {
        // Marten fileDb — FileAsset kayıt defteri. ImageName UNIQUE index (invariant 3). Newtonsoft
        // (non-public setter + ctor, proje standardı). Wolverine in-proc IMessageBus (broker YOK).
        var fileDb = builder.Configuration.GetConnectionString("fileDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.FileSchemaName;
                opts.Connection(fileDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

                opts.Schema.For<FileAsset>()
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.ImageName)
                    .Index(x => x.ImageName);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
