using Storefront.Api.Consumers;

namespace Storefront.Api.Extensions;

// Storefront mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddStorefrontMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
            // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // ReviewSummaryChanged binding'ini TUKETICI kurar (041 dersi); yayinci yalniz exchange
            // deklare eder. Ayni storefront.events kuyruguna baglanir (Sequential — satir yarisi yok).
            rabbit.DeclareExchange(RabbitMqConstants.ReviewSummaryChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ReviewSummaryChanged.Queues.Storefront);
            });

            // 086: OrderCompleted binding SÖKÜLDÜ — UserPurchase Library BC'ye taşındı (FR-011).

            // ProductDiscountChanged → ürün stream'ine append. Binding'i TUKETICI kurar (007);
            // aynı tek-kuyruk deseni (5. exchange → storefront.events, Sequential).
            rabbit.DeclareExchange(RabbitMqConstants.ProductDiscountChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductDiscountChanged.Queues.Storefront);
            });

            // TEK kuyruk (storefront.events): dört exchange de buraya bağlı. Sequential KALIR — aynı ürün
            // stream'ine eşzamanlı append determinizmi (stream sırası = fold sırası, R3).
            opts.ListenToRabbitQueue(RabbitMqConstants.StorefrontEvents.Queue).Sequential();

            // 086: ConcurrencyException retry SÖKÜLDÜ (C2) — eski StorefrontView doc optimistic-concurrency
            // içindi; artık event-log'a append (append-only, doc çakışması yok).
            opts.Policies.UseDurableLocalQueues();
            // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir.
            // REST + MCP ortak yetki noktasi.
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Konvansiyonel keşif bu sınıfları atlıyor (nedeni araştırılacak); açık kayıt garantili yol.
            opts.Discovery.IncludeType(typeof(CatalogConsumers));
            opts.Discovery.IncludeType(typeof(ReviewsConsumers));
            opts.Discovery.IncludeType(typeof(StockConsumers));
            opts.Discovery.IncludeType(typeof(DiscountConsumers));
        });

        return builder;
    }
}
