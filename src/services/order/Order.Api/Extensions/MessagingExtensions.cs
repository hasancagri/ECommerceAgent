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
            // kuramaz ve service-location ister. StockCommitClientProxy CreateOrder handler'ina enjekte edilir.
            opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

            // OrderCreated exchange kaldirildi; sepet temizligi CheckoutSaga gRPC adimi.
            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // siparis odeme onayli tamamlaninca (CheckoutSaga pivot) Personalization'a yayinlanir.
            // Yayinci yalniz exchange deklare eder; kuyruk + binding TUKETICIDE (007 dersi).
            rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<IntegrationEvents.OrderCompleted>()
                .ToRabbitExchange(RabbitMqConstants.OrderCompleted.Exchange);

            // checkout sipariş komutlarını (Create/Confirm/Cancel) dinle; yanıtları reply kuyruğuna.
            opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.OrderCommandsQueue);
            // 049/077: hosted-CF ödeme başarılı → StartCheckout (AlreadyCaptured) orchestrator'a (cross-service).
            opts.PublishMessage<CheckoutMessages.StartCheckout>().ToRabbitQueue(RabbitMqConstants.Checkout.StartQueue);
            opts.PublishMessage<CheckoutMessages.OrderCreated>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);
            opts.PublishMessage<CheckoutMessages.OrderConfirmed>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);
            opts.PublishMessage<CheckoutMessages.OrderCancelled>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

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

            // 049/074: checkout sağası step-komut tüketiminde altyapı hatası retry (Checkout.Orchestrator'daki
            // policyle aynı — FR-024). İş hatası (Result.Permanent) bunu tetiklemez, yalnız fırlayan exception.
            opts.OnException<Exception>().RetryWithCooldown(
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Konvansiyonel keşif *EventHandlers/*Consumers sınıfını atlayabiliyor → açık kayıt (Stock emsali).
            opts.Discovery.IncludeType(typeof(Order.Api.Saga.CheckoutConsumers));
            opts.Discovery.IncludeType(typeof(Order.Api.PaymentConsumers));
            // 088: ölü-mesaj operatör handler'ları Common'da (assembly taraması dışı) — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.DeadLetters.DeadLetterAdminHandlers));
            // Durable cache boşaltma handler'ı Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.Caching.CacheInvalidationRequestedHandler));
        });

        return builder;
    }
}
