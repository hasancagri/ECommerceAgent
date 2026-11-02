namespace Stock.Api.Extensions;

// Stock mesajlaşma kurulumu: Wolverine + RabbitMQ topolojisi. Stock YAYINCIDIR (002): her adet
// değişiminde StockChanged fanout — binding'i tüketici (Catalog) kurar (007 soğuk-açılış dersi).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddStockMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) — repo konvansiyonu (hayalet-node gurultusunu onler).
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // Yayıncı: adet değişimi — yalnız exchange deklare edilir (binding tüketici Catalog'da).
            rabbit.DeclareExchange(RabbitMqConstants.StockChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<Shared.IntegrationEvents.StockChanged>()
                .ToRabbitExchange(RabbitMqConstants.StockChanged.Exchange);

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
