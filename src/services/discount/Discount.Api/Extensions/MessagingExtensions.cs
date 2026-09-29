namespace Discount.Api.Extensions;

// Discount mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/publish/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
// SIRA: çağrısı Program.cs'te cache-aspect çağrısından ÖNCE olmalı (cache aspect IMessageBus'ı sarar).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddDiscountMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            // Apply/Clear helper'ı typed hizmet enjekte etmez ama inline codegen güvenli tarafta kalsın.
            opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!).AutoProvision();

            // kitap başına indirim penceresi (fanout) → Storefront tüketir. Yayıncı yalnız exchange deklare eder.
            rabbit.DeclareExchange(RabbitMqConstants.ProductDiscountChanged.Exchange, e => e.ExchangeType = ExchangeType.Fanout);
            opts.PublishMessage<IntegrationEvents.ProductDiscountChanged>()
                .ToRabbitExchange(RabbitMqConstants.ProductDiscountChanged.Exchange);

            // Catalog ProductChangedEvent TÜKETİLİR (ProductCatalogRef besleme) — binding'i tüketici kurar (007).
            rabbit.DeclareExchange(RabbitMqConstants.ProductChanged.Exchange, e =>
            {
                e.ExchangeType = ExchangeType.Fanout;
                e.BindQueue(RabbitMqConstants.ProductChanged.Queues.Discount);
            });
            opts.ListenToRabbitQueue(RabbitMqConstants.ProductChanged.Queues.Discount).Sequential();

            // Süre yönetimi scheduled message (in-proc durable local queue).
            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // "Consumers"/Process sınıfları taramada atlanabilir → açık kayıt garantili yol.
            opts.Discovery.IncludeType(typeof(Discount.Api.CatalogConsumers));
            opts.Discovery.IncludeType(typeof(Discount.Api.Process.CampaignScheduleHandler));
        });

        return builder;
    }
}
