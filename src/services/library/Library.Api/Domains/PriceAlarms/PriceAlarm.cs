namespace Library.Api.Domains.PriceAlarms;

// kullanıcının bir ürünün fiyat değişimini izleme kaydı — YAŞAYAN abonelik (FR-004):
// tetik aggregate'i DEĞİŞTİRMEZ, kullanıcı kaldırana dek her fiyat değişimi mail üretir.
// v1'de mutator'suz kayıt aggregate'i (bilinçli sınırda — favori/listeler gelince davranış büyür).
public class PriceAlarm : AggregateRoot
{
    public Guid UserId { get; private set; }

    // Kuruluş anında snapshot (R3); boş olabilir — worker "no-email" izi düşer, doğrulama yok.
    public string Email { get; private set; } = null!;

    public Guid ProductId { get; private set; }

    // Kuruluş anındaki ad + fiyat — mail/iz bağlamı (Catalog'a geri sorulmaz).
    public string ProductName { get; private set; } = null!;
    public decimal PriceAtCreation { get; private set; }

    private PriceAlarm()
    {
    }

    /// <summary>Yeni alarmı oluşturur; userId/productId boş Guid ve fiyat ≤ 0 reddedilir, email doğrulanmaz (snapshot).</summary>
    public static ResultDomain<PriceAlarm> Create(
        Guid userId, string email, Guid productId, string productName, decimal priceAtCreation)
    {
        var messages = new List<MessageItem>();

        if (userId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(UserId), Code = LibraryResourceConstants.PRICE_ALARM_INVALID });

        if (productId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(ProductId), Code = LibraryResourceConstants.PRICE_ALARM_INVALID });

        if (priceAtCreation <= 0)
            messages.Add(new MessageItem { Property = nameof(PriceAtCreation), Code = LibraryResourceConstants.PRICE_ALARM_INVALID });

        if (messages.Count > 0)
            return ResultDomain<PriceAlarm>.Error(messages);

        return ResultDomain<PriceAlarm>.Ok(new PriceAlarm
        {
            UserId = userId,
            Email = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim(),
            ProductId = productId,
            ProductName = productName,
            PriceAtCreation = priceAtCreation,
        });
    }
}