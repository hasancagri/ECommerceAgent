namespace Checkout.Orchestrator.Extensions;

// Checkout mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (saga komut/yanıt kuyrukları)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddCheckoutMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // hedefli komutlar per-BC kuyruğa; yanıtlar tek reply kuyruğundan dinlenir (broker saga).
            opts.PublishMessage<CreateOrderCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.OrderCommandsQueue);
            opts.PublishMessage<ConfirmOrderCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.OrderCommandsQueue);
            opts.PublishMessage<CancelOrderCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.OrderCommandsQueue);
            // ChargePaymentCommand route SÖKÜLDÜ (ödeme hosted-CF ile öncedendir; saga charge çekmez).
            opts.PublishMessage<CommitStockCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.StockCommandsQueue);
            opts.PublishMessage<RevertCommitStockCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.StockCommandsQueue);
            opts.PublishMessage<ClearBasketCommand>().ToRabbitQueue(RabbitMqConstants.Checkout.BasketCommandsQueue);

            // Giriş: StartCheckout (WebApp endpoint local publish + chat/Order cross-service) buraya gelir → saga doğar.
            opts.PublishMessage<StartCheckout>().ToRabbitQueue(RabbitMqConstants.Checkout.StartQueue);
            opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.StartQueue);

            // Hedef BC'ler yanıtları buraya yayınlar (tüketici binding'i burada — 007 dersi).
            opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

            // geçici hata retry saga'da DEĞİL — Wolverine policy'de (FR-024). Artan gecikmeyle 3 deneme;
            // tükenirse mesaj dead-letter'a. (Aynı politika hedef BC'lerde de step-komut tüketiminde geçerli.)
            // 088: retry tükenince ölü-mesaj deposuna taşı (uniform yakalama).
            opts.OnException<Exception>().RetryWithCooldown(
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
                .Then.MoveToErrorQueue();

            opts.Policies.UseDurableLocalQueues();
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // 088: ölü-mesaj operatör handler'ları Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.DeadLetters.DeadLetterAdminHandlers));
        });

        return builder;
    }
}
