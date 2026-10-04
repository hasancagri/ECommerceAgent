
namespace Storefront.Api.Extensions;

// 086: Storefront kalıcılık = Marten EVENT-LOG (gerçek-kaynak) + async projection daemon (→ Elasticsearch).
// Eski read-model (StorefrontView doc) + pgvector + ayrı embedding doc SÖKÜLDÜ. Gelen integration event'ler
// ürün stream'ine append edilir; StorefrontProjection stream'i katlayıp ES'e yazar/siler. AgentQueryLog doc
// (ES sorgu izi) Postgres'te KALIR.
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

                // sorgu izi (ret dahil her query_storefront çağrısı bir satır; FR-009).
                opts.Schema.For<Storefront.Api.QueryLog.AgentQueryLog>();
            })
            .IntegrateWithWolverine()
            // Event-log → Elasticsearch: DI'lı async projeksiyon (ctor ES client + embedding üretici).
            .AddProjectionWithServices<StorefrontProjection>(ProjectionLifecycle.Async, ServiceLifetime.Singleton)
            // Async daemon Solo (dev Wolverine Solo durability ile uyumlu; daemon koşmazsa projeksiyon çalışmaz).
            .AddAsyncDaemon(DaemonMode.Solo)
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}