namespace Storefront.Api.Search;

// 086: Elasticsearch kurulumu — Aspire client integration DI kaydı + açılış index bootstrap'ı.
// Program.cs orkestrasyon dışı tutulur (MartenExtensions/MessagingExtensions emsali).
public static class ElasticsearchExtensions
{
    public static WebApplicationBuilder AddStorefrontSearch(this WebApplicationBuilder builder)
    {
        // Aspire ES client integration → ElasticsearchClient singleton (conn adı AppHost kaynağıyla eşit).
        builder.AddElasticsearchClient("elasticsearch");

        // Açılışta sabit index'i mapping'le kur (yoksa) — ilk sorgu hazır index'e düşer.
        builder.Services.AddHostedService<StorefrontSearchIndexBootstrap>();

        // US3 reindex (index sil+kur+rebuild) ERTELENDİ → backlog (dev full-reset+republish yeter; R8).

        return builder;
    }
}