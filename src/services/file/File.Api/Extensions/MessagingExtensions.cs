namespace File.Api.Extensions;

// File mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/publish/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddFileMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek düğüm (Solo) — repo konvansiyonu (hayalet-node gürültüsünü önler).
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            // T018: BC-arası kapak akışı için RabbitMQ transport (bugüne dek in-proc only). ProductAdded'i
            // (Catalog fanout exchange) kendi kuyruğundan dinle; kapak hazırsa CoverIngested yay.
            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
                .AutoProvision();

            // Tüketici binding'i (007 soğuk-açılış dersi): File kendi kuyruğunu Catalog'un exchange'ine bağlar.
            rabbit.DeclareExchange(RabbitMqConstants.ProductAdded.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductAdded.Queues.File);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.ProductAdded.Queues.File);

            // Yayıncı: kapak çözülünce CoverIngested (Catalog tüketir, binding'i Catalog kurar).
            rabbit.DeclareExchange(RabbitMqConstants.CoverIngested.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
            });
            opts.PublishMessage<IntegrationEvents.CoverIngested>()
                .ToRabbitExchange(RabbitMqConstants.CoverIngested.Exchange);

            opts.Policies.UseDurableLocalQueues();
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Wolverine keşfi çoğul *Consumers sınıfını taramaz → açıkça ekle (ZORUNLU; yoksa mesaj yutulur).
            opts.Discovery.IncludeType(typeof(FileApi.CatalogConsumers));
        });

        return builder;
    }
}
