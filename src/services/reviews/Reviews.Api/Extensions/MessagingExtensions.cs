namespace Reviews.Api.Extensions;

// Reviews mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/publish/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
// SIRA: çağrısı Program.cs'te cache-aspect çağrısından ÖNCE olmalı (cache aspect IMessageBus'ı sarar).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddReviewsMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) — repo konvansiyonu (hayalet-node gurultusunu onler).
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // Yayinci yalniz exchange'i deklare eder; kuyruk + binding TUKETICIDE (007 dersi).
            rabbit.DeclareExchange(RabbitMqConstants.ReviewSummaryChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });

            opts.PublishMessage<Shared.IntegrationEvents.ReviewSummaryChanged>()
                .ToRabbitExchange(RabbitMqConstants.ReviewSummaryChanged.Exchange);

            // moderasyon istegi ayri worker'a (RabbitMQ). Yayinci yalniz exchange deklare eder;
            // [Transactional] SubmitReview + transactional outbox → broker down olsa submit reviewsDb'ye
            // commit olur, mesaj outbox'ta bekler (fail-open, submit broker'a senkron baglanmaz).
            rabbit.DeclareExchange(RabbitMqConstants.ReviewModerationRequested.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<Shared.IntegrationEvents.ReviewModerationRequested>()
                .ToRabbitExchange(RabbitMqConstants.ReviewModerationRequested.Exchange);

            // worker'in karari — tuketici kendi kuyrugunu deklare edilen exchange'e baglar (007) + dinler.
            rabbit.DeclareExchange(RabbitMqConstants.ReviewModerated.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ReviewModerated.Queues.Reviews);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.ReviewModerated.Queues.Reviews);

            // Order 'OrderCompleted' tüketilir → satın-alma kanıtı read-model. Tüketici kendi kuyruğunu
            // deklare edilen exchange'e bağlar (007) + dinler. Durable → Reviews kapalıyken kaybolmaz.
            rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.OrderCompleted.Queues.Reviews);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.OrderCompleted.Queues.Reviews);

            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // *Consumers Wolverine isim-konvansiyonunca keşfedilMEZ — elle dahil et (Stock/Catalog emsali).
            opts.Discovery.IncludeType(typeof(Reviews.Api.ModerationAgentConsumers));
            opts.Discovery.IncludeType(typeof(Reviews.Api.OrderConsumers));
        });

        return builder;
    }
}
