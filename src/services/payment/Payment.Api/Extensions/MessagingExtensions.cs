using Wolverine.ErrorHandling;

namespace Payment.Api.Extensions;

// Payment mesajlaşma kurulumu: Wolverine + RabbitMQ broker topolojisi (exchange/binding/publish/listen)
// + handler keşfi. Program.cs orkestrasyon dışı tutulur.
// SIRA: çağrısı Program.cs'te AddCachingAspect'ten ÖNCE olmalı (cache aspect IMessageBus'ı sarar).
public static class MessagingExtensions
{
    public static WebApplicationBuilder AddPaymentMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            // Dev: tek dugum (Solo) - leader election/node-agent koordinasyonu kapali; kirli kapanan
            // debug oturumlarinin hayalet-node StopRemoteAgent timeout gurultusunu kokten onler.
            if (builder.Environment.IsDevelopment())
                opts.Durability.Mode = DurabilityMode.Solo;

            // CreatePaymentIntent handler'ı typed HttpClient (MerchantKeyClient/PgHostedPaymentClient,
            // AddHttpClient<T> = opaque lambda transient) inject eder; Wolverine inline codegen bunları
            // service-location ister. Varsayılan NotAllowed → 500. Order.Api ile aynı politika.
            opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

            // checkout Charge broker yolu SÖKÜLDÜ (PaymentCommandsQueue listen + PaymentCharged publish).
            var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!).AutoProvision();

            // hosted-CF sonucu fanout → Order tüketir (binding'i tüketici kurar). Yayıncı yalnız exchange declare.
            rabbit.DeclareExchange(Shared.RabbitMqConstants.PaymentSucceeded.Exchange, e => e.ExchangeType = ExchangeType.Fanout);
            rabbit.DeclareExchange(Shared.RabbitMqConstants.PaymentFailed.Exchange, e => e.ExchangeType = ExchangeType.Fanout);
            opts.PublishMessage<Shared.IntegrationEvents.PaymentSucceeded>()
                .ToRabbitExchange(Shared.RabbitMqConstants.PaymentSucceeded.Exchange);
            opts.PublishMessage<Shared.IntegrationEvents.PaymentFailed>()
                .ToRabbitExchange(Shared.RabbitMqConstants.PaymentFailed.Exchange);

            opts.Policies.UseDurableLocalQueues();
            // 088: retry tükenince ölü-mesaj deposuna taşı (uniform yakalama).
            opts.OnException<Exception>().RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)).Then.MoveToErrorQueue();
            opts.Policies.AddMiddleware(
                typeof(Common.Utils.Authorization.ScopeAuthorizationMiddleware),
                chain => chain.MessageType.GetCustomAttribute<Common.Utils.Authorization.RequiredScopeAttribute>() is not null);
            opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
            // Handler/Consumer son eki taşımayan süreç sınıfı taramada keşfedilmez → açık kayıt şart.
            opts.Discovery.IncludeType(typeof(Payment.Api.Process.PaymentIntentExpiry));
            // 088: ölü-mesaj operatör handler'ları Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.DeadLetters.DeadLetterAdminHandlers));
            // Durable cache boşaltma handler'ı Common'da — açık kayıt.
            opts.Discovery.IncludeType(typeof(Common.Utils.Caching.CacheInvalidationRequestedHandler));
        });

        return builder;
    }
}
