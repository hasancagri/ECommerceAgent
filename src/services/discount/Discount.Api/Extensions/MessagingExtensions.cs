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

            opts.Policies.UseDurableLocalQueues();
            // 088: retry tükenince ölü-mesaj deposuna taşı (uniform yakalama).
            opts.OnException<Exception>().RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)).Then.MoveToErrorQueue();
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
        });

        return builder;
    }
}
