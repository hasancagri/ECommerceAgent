namespace Payment.Api.Extensions;

// Payment kalıcılık kurulumu: Marten (Postgres document store) + aggregate şemaları/index'leri +
// Wolverine entegrasyonu. Program.cs orkestrasyon dışı tutulur (yükseklik ayrımı).
public static class MartenExtensions
{
    public static WebApplicationBuilder AddPaymentMarten(this WebApplicationBuilder builder)
    {
        var paymentDb = builder.Configuration.GetConnectionString("paymentDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.PaymentSchemaName;
                opts.Connection(paymentDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s =>
                    {
                        s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
                    });

                // hosted-CF PaymentIntent (mock Payment aggregate söküldü). TxRef unique = idempotency temeli
                // (çift callback tek sonuç); UserId index = get_my_payments + canlı-intent re-use sorgusu.
                // Sabit alias ŞART: PaymentIntent tablo adı tr-TR ToLower'da 'mt_doc_paymentıntent' (dotless ı)
                // olur; Marten'in computed-index delta eşleşmesi TABLO adındaki ı'da bozulur → var olan index'i
                // görmez → her boot recreate → 42P07. (order/basket ı'yı yalnız index ADINDA taşır, tablo adında
                // değil → idempotent.) Alias ı'yı tümden kaldırır: mt_doc_payment_intent.
                opts.Schema.For<Payment.Api.Domains.Payments.PaymentIntent>()
                    .DocumentAlias("payment_intent")
                    .UniqueIndex(Marten.Schema.UniqueIndexType.Computed, x => x.TxRef)
                    .Index(x => x.UserId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
