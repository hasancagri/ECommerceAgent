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

            opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // OrderCreated dinleyicisi kaldirildi — sepet temizligi saga'nin gRPC adimi (ClearBasket).

            // checkout sepet temizleme komutunu dinle; yanıtı orchestrator reply kuyruğuna.
            opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.BasketCommandsQueue);
            opts.PublishMessage<CheckoutMessages.BasketCleared>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

            // 049/074: checkout sağası step-komut tüketiminde altyapı hatası retry (Checkout.Orchestrator'daki
            // policyle aynı — FR-024). İş hatası (Result.Permanent) bunu tetiklemez, yalnız fırlayan exception.
            opts.OnException<Exception>().RetryWithCooldown(
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)).Then.MoveToErrorQueue();

            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware(
                typeof(ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // *EventHandlers static sinifi ad konvansiyonuyla otomatik kesfedilmiyor (Storefront deseni);
            // acikca dahil et — yoksa ClearBasketCommand (049) calismaz.
            opts.Discovery.IncludeType(typeof(Saga.CheckoutConsumers));
            // 088: ölü-mesaj operatör handler'ları Common'da (assembly taraması dışı) — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.DeadLetters.DeadLetterAdminHandlers));
        });

        return builder;
    }
}
