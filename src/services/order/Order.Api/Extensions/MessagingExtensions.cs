namespace Order.Api.Extensions;

// Order mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/publish/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
// SIRA: çağrısı Program.cs'te AddCachingAspect'ten ÖNCE olmalı (cache aspect IMessageBus'ı sarar).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddOrderMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
            // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            // gRPC tipli client (AddGrpcClient) opaque factory'dir; Wolverine handler codegen'i inline
            // kuramaz ve service-location ister. start_payment Payment/Basket/Customer/Discount istemcileri enjekte edilir.
            opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // siparis odeme onayli tamamlaninca (Confirm pivotu) OrderCompleted fanout'u yayilir
            // (Reviews + Library + Basket). Yayinci yalniz exchange deklare eder; binding TUKETICIDE (007 dersi).
            rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<IntegrationEvents.OrderCompleted>()
                .ToRabbitExchange(RabbitMqConstants.OrderCompleted.Exchange);

            // dijital (PDF) pivot: Stock BC söküldü → stok commit koreografisi kalktı. Ödeme sonucu
            // doğrudan Order.Confirm/Cancel tetikler (PaymentConsumers).

            // Payment.Api hosted-CF sonuç fanout'ları — tüketici kendi kuyruğunu bağlar + dinler (007 dersi).
            rabbit.DeclareExchange(RabbitMqConstants.PaymentSucceeded.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.PaymentSucceeded.Queues.Order);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.PaymentSucceeded.Queues.Order);
            rabbit.DeclareExchange(RabbitMqConstants.PaymentFailed.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.PaymentFailed.Queues.Order);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.PaymentFailed.Queues.Order);

            // Event tüketiminde altyapı (teknik) hatası retry; tükenince ölü-mesaj deposuna taşı (088 DLQ
            // görünürlüğü — watchdog yok, 092). İş hatası (yetersiz stok) exception değil event'tir, bunu tetiklemez.
            opts.OnException<Exception>().RetryWithCooldown(
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
                .Then.MoveToErrorQueue();

            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Konvansiyonel keşif *Consumers (çoğul) sınıfını atlar → açık kayıt ZORUNLU (yoksa event sessiz yutulur).
            opts.Discovery.IncludeType(typeof(Order.Api.PaymentConsumers));
            // Durable cache boşaltma handler'ı Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.Caching.CacheInvalidationRequestedHandler));
        });

        return builder;
    }
}
