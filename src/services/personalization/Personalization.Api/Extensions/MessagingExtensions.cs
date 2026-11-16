namespace Personalization.Api.Extensions;

// Personalization mesajlaşma kurulumu: Wolverine + RabbitMQ. Personalization YALNIZ TÜKETİCİ (012) —
// app-BFF'in UserSearched fanout'una kendi kuyruğunu bağlar (binding tüketicide — 007 soğuk-açılış
// dersi). Yayın YOK. Hata → uniform retry merdiveni → error-queue (DLQ, 088).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddPersonalizationMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // Tüketici: Catalog arama ucu UserSearched → ham arama kaydı. Binding'i tüketici kurar + kendi kuyruğunu dinler.
            rabbit.DeclareExchange(RabbitMqConstants.UserSearched.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.UserSearched.Queues.Personalization);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.UserSearched.Queues.Personalization);

            opts.Policies.UseDurableLocalQueues();
            opts.OnException<Exception>()
                .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
                .Then.MoveToErrorQueue();

            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // *Consumers (çoğul) ad-son-eki Wolverine keşfine uymaz → elle kayıt ZORUNLU (tuzak).
            opts.Discovery.IncludeType(typeof(Personalization.Api.CatalogSearchConsumers));
        });

        return builder;
    }
}
