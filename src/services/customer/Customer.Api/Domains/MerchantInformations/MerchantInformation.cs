namespace Customer.Api.Domains.MerchantInformations;

/// <summary>
/// ECommerce'in DropShop gateway'indeki merchant kimliği (tekil kayıt): OAuth client_credentials için
/// <c>merchantId</c> + <c>MerchantKey</c>. Vault tokenizer bu kayıttan merchant token'ı mint eder
/// (scope <c>cards.write</c>). MerchantKey aktivasyonda bir kez verilir; operatör buraya yazar
/// (<c>SetMerchantInformation</c>). WebApp'in in-memory credential store'unun kalıcı karşılığı.
/// </summary>
public class MerchantInformation : AggregateRoot
{
    private MerchantInformation()
    {
    }

    /// <summary>DropShop merchant kimliği = OAuth client_id.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>OAuth client_secret (aktivasyon key'i); yalnız connect/token'a gider.</summary>
    public string MerchantKey { get; private set; } = string.Empty;

    /// <summary>Kayıt durumu (dev: Active).</summary>
    public string Status { get; private set; } = "Active";

    /// <summary>078 FR-013: ikili kayıt anında PG'ye doğrulandıysa true; PG erişilemezken kayıtta false ("doğrulanamadı").</summary>
    public bool CredentialsVerified { get; private set; }

    /// <summary>087: makine-handoff kayıt yaşam döngüsü — None/Pending/Active.</summary>
    public RegistrationStatus Registration { get; private set; } = RegistrationStatus.None;

    /// <summary>087: aktif kayıt çağrısının correlation'ı; callback eşleme + çift-callback idempotency. Aktivasyondan sonra da korunur (idempotent eşleme için).</summary>
    public Guid? PendingCorrelationId { get; private set; }

    /// <summary>Yeni merchant kimliği oluşturur; merchantId + key zorunlu. Verified = kayıt anı PG doğrulaması sonucu.</summary>
    public static ResultDomain<MerchantInformation> Create(Guid merchantId, string merchantKey, bool credentialsVerified = false)
    {
        var messages = new List<MessageItem>();

        if (merchantId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(MerchantId), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        if (string.IsNullOrWhiteSpace(merchantKey))
            messages.Add(new MessageItem { Property = nameof(MerchantKey), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        if (messages.Count > 0)
            return ResultDomain<MerchantInformation>.Error(messages);

        return ResultDomain<MerchantInformation>.Ok(new MerchantInformation
        {
            MerchantId = merchantId,
            MerchantKey = merchantKey.Trim(),
            Status = "Active",
            CredentialsVerified = credentialsVerified
        });
    }

    /// <summary>MerchantKey'i günceller (rotate / yeniden set); boş RET. Verified = kayıt anı PG doğrulaması sonucu.</summary>
    public ResultDomain UpdateKey(string merchantKey, bool credentialsVerified = false)
    {
        if (string.IsNullOrWhiteSpace(merchantKey))
        {
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(MerchantKey),
                Code = CustomerResourceConstants.VALUE_IS_REQUIRED
            });
        }

        MerchantKey = merchantKey.Trim();
        CredentialsVerified = credentialsVerified;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>087: henüz kayıtsız (credential'sız) yeni merchant kaydı — makine-handoff yolu için tohum. Store tek-merchant.</summary>
    public static MerchantInformation NewUnregistered() => new();

    /// <summary>087: kayıt/yenileme çevrimi başlatır (store→PG tetiği). Correlation set + Pending. Boş
    /// correlation RET. Overwrite-safe (087 reissue yarış fix'i): bir Pending yeni correlation'la ÜZERİNE
    /// yazılabilir — reissue PG'den ÖNCE pending'i commit eder, PG başarısızsa sonraki çevrim bayat
    /// pending'i ezer (kilitlenme yok). Tek first-party merchant → eşzamanlı çift-kayıt riski yok.</summary>
    public ResultDomain StartRegistration(Guid correlationId)
    {
        if (correlationId == Guid.Empty)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(PendingCorrelationId), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        Registration = RegistrationStatus.Pending;
        PendingCorrelationId = correlationId;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>087: PG'den HMAC-callback ile gelen credential'ı uygular. Correlation eşleşmezse nötr RET; zaten Active + aynı correlation ise idempotent no-op Ok (çift-callback yutulur); eşleşir + Pending ise MerchantId/Key set + Active.</summary>
    public ResultDomain ApplyCredentialsFromCallback(Guid correlationId, Guid merchantId, string merchantKey)
    {
        // Eşleşmeyen/bilinmeyen correlation → nötr red (persist yok).
        if (PendingCorrelationId is null || PendingCorrelationId != correlationId)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(PendingCorrelationId), Code = CustomerResourceConstants.INVALID_OPERATION_ERROR });

        // Çift/gecikmeli callback: zaten Active + aynı correlation → no-op Ok.
        if (Registration == RegistrationStatus.Active)
            return ResultDomain.Ok();

        var messages = new List<MessageItem>();
        if (merchantId == Guid.Empty)
            messages.Add(new MessageItem { Property = nameof(MerchantId), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });
        if (string.IsNullOrWhiteSpace(merchantKey))
            messages.Add(new MessageItem { Property = nameof(MerchantKey), Code = CustomerResourceConstants.VALUE_IS_REQUIRED });
        if (messages.Count > 0)
            return ResultDomain.Error(messages);

        MerchantId = merchantId;
        MerchantKey = merchantKey.Trim();
        CredentialsVerified = true;
        Registration = RegistrationStatus.Active;
        Status = "Active";
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}

/// <summary>087: makine-handoff kayıt yaşam döngüsü durumu (aggregate dosyasında — İlke II/CLAUDE.md).</summary>
public enum RegistrationStatus
{
    None,
    Pending,
    Active
}
