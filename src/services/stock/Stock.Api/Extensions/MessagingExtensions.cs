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

            // 092 checkout koreografisi: Order'dan stok commit talebi — kendi kuyruğunu bağlar + dinler (007).
            rabbit.DeclareExchange(RabbitMqConstants.StockCommitRequested.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.StockCommitRequested.Queues.Stock);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.StockCommitRequested.Queues.Stock);

            // Commit sonucu Order'a (fanout; Order kendi kuyruğunu bağlar).
            rabbit.DeclareExchange(RabbitMqConstants.StockCommitted.Exchange, e => e.ExchangeType = ExchangeType.Fanout);
            opts.PublishMessage<IntegrationEvents.StockCommitted>().ToRabbitExchange(RabbitMqConstants.StockCommitted.Exchange);
            rabbit.DeclareExchange(RabbitMqConstants.StockCommitFailed.Exchange, e => e.ExchangeType = ExchangeType.Fanout);
            opts.PublishMessage<IntegrationEvents.StockCommitFailed>().ToRabbitExchange(RabbitMqConstants.StockCommitFailed.Exchange);

            // Event tüketiminde altyapı (teknik) hatası retry; tükenince ölü-mesaj deposuna (088 DLQ).
            // İş hatası (yetersiz stok) exception değil StockCommitFailed event'idir, bunu tetiklemez.
            opts.OnException<Exception>().RetryWithCooldown(
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
                // 088: retry tükenince ölü-mesaj deposuna taşı (uniform yakalama).
                .Then.MoveToErrorQueue();

            opts.Policies.UseDurableLocalQueues();
            // Handler-level yetki: middleware SADECE [RequiredScope] tasiyan komut/sorgulara weave edilir.
            // REST + MCP ortak yetki noktasi.
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Konvansiyonel keşif *Consumers (çoğul) sınıfını atlar → açık kayıt ZORUNLU (yoksa event sessiz yutulur).
            opts.Discovery.IncludeType(typeof(Stock.Api.CatalogConsumers));
            opts.Discovery.IncludeType(typeof(Stock.Api.OrderConsumers));
            // 088: ölü-mesaj operatör handler'ları Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.DeadLetters.DeadLetterAdminHandlers));
            // Durable cache boşaltma handler'ı Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.Caching.CacheInvalidationRequestedHandler));
        });

        return builder;
    }
}
