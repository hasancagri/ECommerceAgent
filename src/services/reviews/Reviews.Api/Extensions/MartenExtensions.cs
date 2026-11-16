namespace Reviews.Api.Extensions;

// Reviews kalıcılık kurulumu: Marten (Postgres document store) + aggregate şemaları/index'leri +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddReviewsMarten(this WebApplicationBuilder builder)
    {
        var reviewsDb = builder.Configuration.GetConnectionString("reviewsDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.ReviewsSchemaName;
                opts.Connection(reviewsDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

                // R9: tek-yorum kilidinin son sozu — uygulama kontrolu + unique index (cift savunma).
                // TUZAK (tr-TR noktasız ı): "UserId"/"ProductId" ToLower → userıd/product_ıd → uidx adında
                // ı → 42P07 crashloop. Computed unique index adı ELLE, lowercase/I'sız (CLAUDE.md kuralı).
                opts.Schema.For<Review>()
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, "uidx_review_user_product", x => x.UserId, x => x.ProductId)
                    .Index(x => x.ProductId);

                // satın-alma kanıtı read-model (Id = "{userId:N}:{productId:N}"; eligibility PK lookup).
                opts.Schema.For<PurchasedProduct>();
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
