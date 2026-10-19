namespace Basket.Api.Extensions;

// Basket mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (listen/publish) + handler keşfi.
// Program.cs orkestrasyon dışı tutulur.
// SIRA: çağrısı Program.cs'te AddCachingAspect'ten ÖNCE olmalı (cache aspect IMessageBus'ı sarar).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddBasketMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
            // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // 092 checkout koreografisi: sipariş tamamlanınca (OrderCompleted fanout) sepeti sil.
            // Binding'i tüketici (Basket) kurar (007 soğuk-açılış dersi).
            rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.OrderCompleted.Queues.Basket);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.OrderCompleted.Queues.Basket);

            // Event tüketiminde altyapı (teknik) hatası retry; tükenince ölü-mesaj deposuna (088 DLQ).
            opts.OnException<Exception>().RetryWithCooldown(
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)).Then.MoveToErrorQueue();

            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware(
                typeof(ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // *Consumers (çoğul) ad konvansiyonuyla otomatik keşfedilmiyor → açık kayıt ZORUNLU
            // (yoksa OrderCompleted sessiz yutulur, sepet temizlenmez).
            opts.Discovery.IncludeType(typeof(Basket.Api.OrderConsumers));
            // 088: ölü-mesaj operatör handler'ları Common'da (assembly taraması dışı) — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.DeadLetters.DeadLetterAdminHandlers));
            // Durable cache boşaltma handler'ı Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.Caching.CacheInvalidationRequestedHandler));
        });

        return builder;
    }
}
