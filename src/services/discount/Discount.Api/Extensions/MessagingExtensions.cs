namespace Discount.Api.Extensions;

// Discount mesajlaşma kurulumu. Discount YAYINCIDIR (003): ürünün etkin oranı her değiştiğinde
// (başlama/bitme/iptal/ezilme — YÜRÜRLÜK anında, CampaignWindowProcess) ProductDiscountChanged
// fanout — binding'i tüketici (Catalog) kurar (007 soğuk-açılış dersi).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddDiscountMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) — repo konvansiyonu (hayalet-node gurultusunu onler).
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            rabbit.DeclareExchange(RabbitMqConstants.ProductDiscountChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<Shared.IntegrationEvents.ProductDiscountChanged>()
                .ToRabbitExchange(RabbitMqConstants.ProductDiscountChanged.Exchange);

            // 010: Tüketici — Catalog 'ProductDiscountImported' → açık-uçlu Campaign + oran yayını.
            // Binding'i TÜKETİCİ kurar (007 soğuk-açılış dersi) + kendi kuyruğunu dinler.
            rabbit.DeclareExchange(RabbitMqConstants.ProductDiscountImported.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductDiscountImported.Queues.Discount);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.ProductDiscountImported.Queues.Discount);

            opts.Policies.UseDurableLocalQueues();
            // 088: retry tükenince ölü-mesaj deposuna taşı (uniform yakalama).
            opts.OnException<Exception>().RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)).Then.MoveToErrorQueue();
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // 010: *Consumers (çoğul) ad-son-eki convention'a uymaz → elle kayıt ZORUNLU.
            opts.Discovery.IncludeType(typeof(Discount.Api.CatalogConsumers));
        });

        return builder;
    }
}
