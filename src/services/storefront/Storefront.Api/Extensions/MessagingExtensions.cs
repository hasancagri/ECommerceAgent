namespace Storefront.Api.Extensions;

// Storefront mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
// SIRA: çağrısı Program.cs'te AddCachingAspect'ten ÖNCE olmalı (cache aspect IMessageBus'ı sarar).
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

            // OrderCompleted → UserPurchase birikimi (kişisel feed sinyali). Binding'i TUKETICI kurar;
            // ayni tek-kuyruk deseni (4. exchange → storefront.events).
            rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.OrderCompleted.Queues.Storefront);
            });

            // ProductDiscountChanged → StorefrontView.ApplyDiscount. Binding'i TUKETICI kurar (007);
            // aynı tek-kuyruk deseni (5. exchange → storefront.events, Sequential).
            rabbit.DeclareExchange(RabbitMqConstants.ProductDiscountChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductDiscountChanged.Queues.Storefront);
            });

            // TEK kuyruk (storefront.events): üç exchange de buraya bağlı; Sequential işleme sayesinde
            // aynı view satırına eşzamanlı yazım olmaz — ConcurrencyException kaynağında çözülür.
            opts.ListenToRabbitQueue(RabbitMqConstants.StorefrontEvents.Queue).Sequential();

            // Composite satirda kaynaklar-arasi eszamanli yazim cakismasi (optimistic concurrency) → retry.
            opts.OnException<JasperFx.ConcurrencyException>().RetryTimes(5);
            opts.Policies.UseDurableLocalQueues();
            // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir.
            // REST + MCP ortak yetki noktasi.
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Konvansiyonel keşif bu sınıfları atlıyor (nedeni araştırılacak); açık kayıt garantili yol.
            opts.Discovery.IncludeType(typeof(Storefront.Api.CatalogConsumers));
            opts.Discovery.IncludeType(typeof(Storefront.Api.ReviewsConsumers));
            opts.Discovery.IncludeType(typeof(Storefront.Api.StockConsumers));
            opts.Discovery.IncludeType(typeof(Storefront.Api.OrderConsumers));
            opts.Discovery.IncludeType(typeof(Storefront.Api.DiscountConsumers));
        });

        return builder;
    }
}
