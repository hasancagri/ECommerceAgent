namespace Catalog.Api.Extensions;

// Catalog mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/publish/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
// SIRA: çağrısı Program.cs'te AddCachingAspect'ten ÖNCE olmalı (cache aspect IMessageBus'ı sarar).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddCatalogMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
            // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // Yayıncı yalnız exchange deklare eder; kuyruk + binding tüketicide (Library, 007).
            // (Storefront binding'i SÖKÜLDÜ — o BC kaldırıldı.)
            rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });

            opts.PublishMessage<Shared.IntegrationEvents.ProductChangedEvent>()
                .ToRabbitExchange(RabbitMqConstants.ProductChanged.Exchange);

            // 050/051: yayınlanan üründe barkod↔ProductId eşlemesi Stock'a duyurulur (yayıncı yalnız exchange deklare eder).
            // İlk yayıncı = kitap import (051); feed 050'de söküldü.
            rabbit.DeclareExchange(RabbitMqConstants.ProductAdded.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<Shared.IntegrationEvents.ProductAdded>()
                .ToRabbitExchange(RabbitMqConstants.ProductAdded.Exchange);

            // 004: CoverIngested tüketimi GERİ (097 kapaksızlık kararı tersine) — File kapak zinciri
            // → Product.SetImage. Binding'i TÜKETİCİ kurar; hatada uniform retry→error-queue (DLQ).
            rabbit.DeclareExchange(RabbitMqConstants.CoverIngested.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.CoverIngested.Queues.Catalog);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.CoverIngested.Queues.Catalog);

            // 002: Tüketici — Stock 'StockChanged' → Product.StockCount kopyası (IsSalable stok bacağı).
            // Binding'i TÜKETİCİ kurar (007 soğuk-açılış dersi) + kendi kuyruğunu dinler. Hatada
            // aşağıdaki uniform retry→error-queue policy'si geçerli (DLQ — 088).
            rabbit.DeclareExchange(RabbitMqConstants.StockChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.StockChanged.Queues.Catalog);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.StockChanged.Queues.Catalog);

            // 003: Tüketici — Discount 'ProductDiscountChanged' → Product.DiscountRate kopyası.
            // Binding'i TÜKETİCİ kurar; hatada uniform retry→error-queue policy'si (DLQ — 088).
            rabbit.DeclareExchange(RabbitMqConstants.ProductDiscountChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductDiscountChanged.Queues.Catalog);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.ProductDiscountChanged.Queues.Catalog);

            opts.Policies.UseDurableLocalQueues();
            // 088: retry tükenince ölü-mesaj deposuna taşı (uniform yakalama).
            opts.OnException<Exception>().RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)).Then.MoveToErrorQueue();
            opts.Policies.AddMiddleware(
                typeof(ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Durable cache boşaltma handler'ı Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.Caching.CacheInvalidationRequestedHandler));
            // *Consumers (çoğul) Wolverine isim-konvansiyonunca keşfedilMEZ — elle dahil et.
            opts.Discovery.IncludeType(typeof(Catalog.Api.StockConsumers));
            opts.Discovery.IncludeType(typeof(Catalog.Api.DiscountConsumers));
            // 004: kapak dönüşü (CoverIngested → SetImage). Import süreci mesajsız —
            // ImportJobWorker (BackgroundService, Program.cs'te AddHostedService) durum yoklar.
            opts.Discovery.IncludeType(typeof(Catalog.Api.FileConsumers));
        });

        return builder;
    }
}
