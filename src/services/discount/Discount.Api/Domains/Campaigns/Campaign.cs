namespace Discount.Api.Domains.Campaigns;

// kampanya süzgeç tipi — indirimin hangi boyutta açıldığı. Enum aggregate dosyasında (konvansiyon).
public enum ScopeType
{
    Category,
    Author,
    Publisher,
    Product
}

// kampanya yaşam döngüsü. Cancelled TEK hard durum; Active/Scheduled/Ended büyük ölçüde
// pencere (now vs StartsAt/EndsAt) türevidir — fire recompute Status'a değil pencereye bakar (idempotent).
public enum CampaignStatus
{
    Scheduled,
    Active,
    Ended,
    Cancelled
}

// kampanya aggregate'i. Pencereyi + hangi süzgeçle açıldığını taşır (denetim + iptal + expiry).
// Discount.Api FİYAT TUTMAZ — Campaign yalnız yüzde + süzgeç + pencere otoritesidir. Edit YOK (v1):
// süzgeç snapshot + kitap-başı-tek-indirim modelinde düzenleme belirsiz; iptal-edip-yeniden-aç yeter.
public class Campaign : AggregateRoot
{
    private Campaign() { }

    public string Name { get; private set; } = default!;
    public ScopeType ScopeType { get; private set; }
    public Guid ScopeRef { get; private set; }
    public int Percentage { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }
    public CampaignStatus Status { get; private set; }

    /// <summary>
    /// Kampanya açar: invariant'ları doğrular (yüzde 1-99, bitiş > başlangıç, ad dolu, scopeRef dolu,
    /// scopeType tanımlı enum). Geçerli ama 0 kitaba çözülen süzgeç red DEĞİL (resolver kararı) — burada
    /// yalnız yapısal doğrulama. startsAt≤now ise Active doğar, aksi halde Scheduled.
    /// </summary>
    public static ResultDomain<Campaign> Create(
        string name, ScopeType scopeType, Guid scopeRef, int percentage,
        DateTime startsAt, DateTime? endsAt, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Campaign>.Error(new MessageItem { Code = DiscountResourceConstants.CAMPAIGN_NAME_REQUIRED });

        if (percentage < 1 || percentage > 99)
            return ResultDomain<Campaign>.Error(new MessageItem { Code = DiscountResourceConstants.CAMPAIGN_PERCENTAGE_INVALID });

        if (endsAt.HasValue && endsAt.Value <= startsAt)
            return ResultDomain<Campaign>.Error(new MessageItem { Code = DiscountResourceConstants.CAMPAIGN_WINDOW_INVALID });

        if (scopeRef == Guid.Empty)
            return ResultDomain<Campaign>.Error(new MessageItem { Code = DiscountResourceConstants.CAMPAIGN_SCOPE_REF_REQUIRED });

        if (!Enum.IsDefined(scopeType))
            return ResultDomain<Campaign>.Error(new MessageItem { Code = DiscountResourceConstants.CAMPAIGN_SCOPE_TYPE_INVALID });

        var campaign = new Campaign
        {
            Name = name.Trim(),
            ScopeType = scopeType,
            ScopeRef = scopeRef,
            Percentage = percentage,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = startsAt <= now ? CampaignStatus.Active : CampaignStatus.Scheduled
        };
        return ResultDomain<Campaign>.Ok(campaign);
    }

    /// <summary>Kampanyayı iptal eder (hard). Zaten iptalse hata. Kitap temizliği handler'da (push).</summary>
    public ResultDomain Cancel()
    {
        if (Status == CampaignStatus.Cancelled)
            return ResultDomain.Error(new MessageItem { Code = DiscountResourceConstants.CAMPAIGN_ALREADY_CANCELLED });

        Status = CampaignStatus.Cancelled;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Start-fire aktifleştirmesi (idempotent): iptal/bitmiş değilse Active. Kitap apply handler'da.</summary>
    public void MarkActive()
    {
        if (Status is CampaignStatus.Cancelled or CampaignStatus.Ended) return;
        Status = CampaignStatus.Active;
    }

    /// <summary>End-fire kapanışı (idempotent): iptal değilse Ended. Kitap temizliği handler'da.</summary>
    public void MarkEnded()
    {
        if (Status == CampaignStatus.Cancelled) return;
        Status = CampaignStatus.Ended;
    }

    /// <summary>Verilen an itibarıyla indirim etkin mi: iptal değil + pencere içinde.</summary>
    public bool IsEffectiveAt(DateTime now) =>
        Status != CampaignStatus.Cancelled
        && StartsAt <= now
        && (EndsAt is null || now < EndsAt.Value);
}
