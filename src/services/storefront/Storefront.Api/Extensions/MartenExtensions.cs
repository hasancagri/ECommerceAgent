namespace Storefront.Api.Extensions;

// Storefront kalıcılık kurulumu: Marten (Postgres document store) + pgvector + read-model/embedding
// şemaları + Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddStorefrontMarten(this WebApplicationBuilder builder)
    {
        var storefrontDb = builder.Configuration.GetConnectionString("storefrontDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.StorefrontSchemaName;
                opts.Connection(storefrontDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

                // pgvector extension'ı şemaya ekler + Npgsql vector type handler kaydeder. Embedding JSONB
                // içinde float[] yaşar; kNN sorgusu (data->>'DescriptionEmbedding')::vector cast'iyle koşar.
                // VectorOn/HNSW bilinçli YOK (research R7): 20k satırda exact scan ms mertebesi, index'e gerek yok.
                opts.UsePgVector();

                // Rich aggregate degil (invariant tasimaz); ProductId, Marten Id'si. Tek composite satir.
                // Optimistic concurrency: farkli kaynaklarin ayni satira eszamanli yazmasinda lost-update
                // olmaz — cakisan handler ConcurrencyException alir, Wolverine retry'da taze yukleyip uygular.
                opts.Schema.For<StorefrontView>().Identity(x => x.ProductId).UseOptimisticConcurrency(true);

                // kullanıcı satın-alma birikimi (kişisel feed sinyali). PK = "{userId:N}:{productId:N}"
                // (idempotent upsert); feed sorgusunun tek erişim yolu UserId — index onun için.
                opts.Schema.For<Storefront.Api.Domains.UserPurchase.UserPurchase>().Index(x => x.UserId);

                // anlamsal temsil AYRI dokümanda (view satırı şişmez; tam-satır okuma yolları etkilenmez).
                // Optimistic concurrency bilinçli YOK: handler/backfill yarışında son yazan kazanır (aynı metnin
                // temsili — içerik eşdeğer). Görünürlük StorefrontView satılabilirlik filtresinde (FR-007).
                opts.Schema.For<Storefront.Api.Domains.StorefrontView.ProductDescriptionEmbedding>()
                    .Identity(x => x.ProductId);

                // sorgu izi (ret dahil her query_storefront çağrısı bir satır; FR-006/SC-005).
                opts.Schema.For<Storefront.Api.AgentSql.AgentQueryLog>();
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
