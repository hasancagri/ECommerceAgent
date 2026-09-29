namespace Payment.Api.Domains.Payments;

// hosted-CF ödeme girişimi. "ödeme yap" → PG hosted link istenir; link + PG referansı burada
// Pending saklanır, callback (başarı/başarısız) veya terk-timeout durumu terminal'e taşır. Kart alanı
// HİÇ taşımaz. Durum geçişleri + guard'lar aggregate'te (İLKE II); anemik değil. TxRef mağaza-üretimli
// tekil (Marten unique index) → çift callback idempotent tek sonuç.
public class PaymentIntent : AggregateRoot
{
    private PaymentIntent() { }

    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    // Kullanıcı-kapsamlı re-use eşleşmesi (UserId+BasketRef canlı Pending → aynı link).
    public string BasketRef { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    // Mağaza-üretimli tekil referans; PG'ye OrderRef, callback'te geri gelir.
    public string TxRef { get; private set; } = string.Empty;
    // PG döner (iz/destek); link isteği yanıtında + başarı callback'inde güncellenir.
    public string? PgPaymentRef { get; private set; }
    public string HostedUrl { get; private set; } = string.Empty;
    public PaymentIntentStatus Status { get; private set; }
    // Failed/Expired sebep kodu ("ABANDONED", PG reason); Pending/Succeeded'de null.
    public string? FailureReason { get; private set; }

    /// <summary>Yeni bir Pending ödeme girişimi oluşturur; id'ler/tutar/txRef/hostedUrl doğrulanır.</summary>
    public static ResultDomain<PaymentIntent> Create(
        Guid orderId, Guid userId, string basketRef, decimal amount,
        string txRef, string? pgPaymentRef, string hostedUrl)
    {
        var messages = new List<MessageItem>();

        if (orderId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(OrderId), Code = PaymentResourceConstants.PAYMENT_ORDER_ID_REQUIRED });

        if (userId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(UserId), Code = PaymentResourceConstants.PAYMENT_USER_ID_REQUIRED });

        if (amount <= 0)
            messages.Add(new MessageItem { Property = nameof(Amount), Code = PaymentResourceConstants.PAYMENT_AMOUNT_INVALID });

        if (string.IsNullOrWhiteSpace(txRef))
            messages.Add(new MessageItem { Property = nameof(TxRef), Code = PaymentResourceConstants.PAYMENT_INTENT_TXREF_REQUIRED });

        if (string.IsNullOrWhiteSpace(hostedUrl))
            messages.Add(new MessageItem { Property = nameof(HostedUrl), Code = PaymentResourceConstants.PAYMENT_INTENT_HOSTED_URL_REQUIRED });

        if (messages.Count > 0)
            return ResultDomain<PaymentIntent>.Error(messages);

        return ResultDomain<PaymentIntent>.Ok(new PaymentIntent
        {
            OrderId = orderId,
            UserId = userId,
            BasketRef = basketRef ?? string.Empty,
            Amount = amount,
            TxRef = txRef.Trim(),
            PgPaymentRef = pgPaymentRef,
            HostedUrl = hostedUrl.Trim(),
            Status = PaymentIntentStatus.Pending
        });
    }

    /// <summary>Ödemeyi başarılı işaretler (idempotent). Zaten Succeeded ise no-op; Failed/Expired'den hata.</summary>
    public ResultDomain MarkSucceeded(string? pgPaymentRef)
    {
        if (Status == PaymentIntentStatus.Succeeded)
            return ResultDomain.Ok(); // çift callback → no-op

        if (Status is PaymentIntentStatus.Failed or PaymentIntentStatus.Expired)
            return ResultDomain.Error(new MessageItem
                { Property = nameof(Status), Code = PaymentResourceConstants.PAYMENT_INTENT_INVALID_TRANSITION });

        Status = PaymentIntentStatus.Succeeded;
        if (!string.IsNullOrWhiteSpace(pgPaymentRef))
            PgPaymentRef = pgPaymentRef;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Ödemeyi başarısız işaretler; yalnız Pending'den. Succeeded'den hata (para alındı); Failed/Expired no-op.</summary>
    public ResultDomain MarkFailed(string reason)
    {
        if (Status is PaymentIntentStatus.Failed or PaymentIntentStatus.Expired)
            return ResultDomain.Ok(); // no-op

        if (Status == PaymentIntentStatus.Succeeded)
            return ResultDomain.Error(new MessageItem
                { Property = nameof(Status), Code = PaymentResourceConstants.PAYMENT_INTENT_INVALID_TRANSITION });

        Status = PaymentIntentStatus.Failed;
        FailureReason = reason;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Terk-timeout'ta girişimi süresi-doldu işaretler; yalnız Pending'den, diğer durumlar no-op.</summary>
    public ResultDomain Expire()
    {
        if (Status != PaymentIntentStatus.Pending)
            return ResultDomain.Ok(); // timer geç geldi (callback kazandı) → no-op

        Status = PaymentIntentStatus.Expired;
        FailureReason = "ABANDONED";
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Girişim canlı mı: Pending ve oluşturulmasından bu yana timeout dolmamış (re-use eşleşmesi).</summary>
    public bool IsLive(int timeoutSeconds, DateTime now) =>
        Status == PaymentIntentStatus.Pending && CreatedTime.AddSeconds(timeoutSeconds) > now;
}

public enum PaymentIntentStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Expired = 4
}
