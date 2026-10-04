namespace Library.Api.Extensions;

// Library kalıcılık kurulumu: Marten (Postgres document store) + aggregate şemaları/index'leri +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddLibraryMarten(this WebApplicationBuilder builder)
    {
        var libraryDb = builder.Configuration.GetConnectionString("libraryDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.LibrarySchemaName;
                opts.Connection(libraryDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

                // FR-002: aynı kullanıcı + ürüne tek alarm — uygulama kontrolü + sorgu index'i.
                opts.Schema.For<PriceAlarm>()
                    .Index(x => x.UserId)
                    .Index(x => x.ProductId);

                // 086: kişisel satın-alma birikimi (Storefront'tan taşındı). Id = "{userId}:{productId}"
                // (idempotent upsert); erişim yolu UserId → index onun için.
                opts.Schema.For<Library.Api.Domains.UserPurchase.UserPurchase>().Index(x => x.UserId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
