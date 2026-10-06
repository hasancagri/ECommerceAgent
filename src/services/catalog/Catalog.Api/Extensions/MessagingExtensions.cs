using Wolverine.ErrorHandling;

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

            rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductChanged.Queues.Storefront);
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

            // T022: File.Api'nin CoverIngested'ini tüket (Catalog'un İLK consumer'ı). Binding'i tüketici
            // kurar (007 dersi); FileConsumers.Handle → Product.SetImage → ProductChangedEvent.
            rabbit.DeclareExchange(RabbitMqConstants.CoverIngested.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.CoverIngested.Queues.Catalog);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.CoverIngested.Queues.Catalog);

            opts.Policies.UseDurableLocalQueues();
            // 088: retry tükenince ölü-mesaj deposuna taşı (uniform yakalama).
            opts.OnException<Exception>().RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)).Then.MoveToErrorQueue();
            opts.Policies.AddMiddleware(
                typeof(ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // TUZAK: Wolverine keşfi çoğul *Consumers sınıfını taramaz → yeni consumer/handler ekleyince
            // buraya IncludeType ile EKLE (ZORUNLU; yoksa mesaj sessizce yutulur — dead-letter da yok).
            opts.Discovery.IncludeType(typeof(Catalog.Api.FileConsumers));
            // 088: ölü-mesaj operatör handler'ları Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(global::Common.Utils.DeadLetters.DeadLetterAdminHandlers));
        });

        return builder;
    }
}
