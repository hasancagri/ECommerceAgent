namespace Library.Api.Extensions;

// Library mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/publish/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddLibraryMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) — repo konvansiyonu (hayalet-node gurultusunu onler).
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // Tüketici: Catalog'un product.changed fanout'una kendi kuyruğunu bağlar (007 dersi) + dinler.
            rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductChanged.Queues.Library);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.ProductChanged.Queues.Library);

            // 086: Tüketici: Order 'OrderCompleted' → UserPurchase birikimi (Storefront'tan taşındı).
            // Binding'i TÜKETİCİ kurar (007 soğuk-açılış dersi) + kendi kuyruğunu dinler.
            rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.OrderCompleted.Queues.Library);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.OrderCompleted.Queues.Library);

            // Tüketici: NotificationAgent'ın gönderim sonucu → NotificationRecord izi.
            rabbit.DeclareExchange(RabbitMqConstants.NotificationSent.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.NotificationSent.Queues.Library);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.NotificationSent.Queues.Library);

            // Yayıncı: alarm tetiği — yalnız exchange deklare eder (binding tüketici NotificationAgent'ta).
            rabbit.DeclareExchange(RabbitMqConstants.PriceAlarmTriggered.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<Shared.IntegrationEvents.PriceAlarmTriggered>()
                .ToRabbitExchange(RabbitMqConstants.PriceAlarmTriggered.Exchange);

            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // *Consumers (çoğul) Wolverine isim-konvansiyonunca keşfedilMEZ — elle dahil et (Reviews emsali).
            opts.Discovery.IncludeType(typeof(Library.Api.CatalogConsumers));
            opts.Discovery.IncludeType(typeof(Library.Api.NotificationAgentConsumers));
            opts.Discovery.IncludeType(typeof(Library.Api.OrderConsumers));
        });

        return builder;
    }
}
