namespace Stock.Api.Extensions;

// Stock mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/publish/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
// SIRA: çağrısı Program.cs'te AddCachingAspect'ten ÖNCE olmalı (cache aspect IMessageBus'ı sarar).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddStockMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
            // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            rabbit.DeclareExchange(RabbitMqConstants.StockChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.StockChanged.Queues.Storefront);
            });

            opts.PublishMessage<Shared.IntegrationEvents.StockChangedEvent>()
                .ToRabbitExchange(RabbitMqConstants.StockChanged.Exchange);

            // 050/051: Catalog ProductAdded tüketicisi — barkod↔ProductId eşlemesi + ilk OnHand (binding'i tüketici kurar).
            // Sıralı kuyruk (aynı barkod sıralı işlenir). İlk yayıncı = kitap import (051); feed söküldü (050).
            rabbit.DeclareExchange(RabbitMqConstants.ProductAdded.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductAdded.Queues.Stock);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.ProductAdded.Queues.Stock).Sequential();

            // checkout stok komutlarını (Commit/RevertCommit) dinle; yanıtları orchestrator reply kuyruğuna.
            opts.ListenToRabbitQueue(RabbitMqConstants.Checkout.StockCommandsQueue);
            opts.PublishMessage<CheckoutMessages.StockCommitted>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);
            opts.PublishMessage<CheckoutMessages.StockCommitReverted>().ToRabbitQueue(RabbitMqConstants.Checkout.RepliesQueue);

            // 049/074: checkout sağası step-komut tüketiminde altyapı hatası retry (Checkout.Orchestrator'daki
            // policyle aynı — FR-024). İş hatası (Result.Permanent) bunu tetiklemez, yalnız fırlayan exception.
            opts.OnException<Exception>().RetryWithCooldown(
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

            opts.Policies.UseDurableLocalQueues();
            // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir.
            // REST + MCP ortak yetki noktasi.
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Konvansiyonel keşif event-handler sınıfını atlayabiliyor (Storefront emsali) — açık kayıt garantili yol.
            opts.Discovery.IncludeType(typeof(Stock.Api.CatalogConsumers));
            opts.Discovery.IncludeType(typeof(Stock.Api.Saga.CheckoutConsumers));
        });

        return builder;
    }
}
