namespace Discount.Api.Domains.ProductDiscounts;

// materyalize read-model (aggregate DEĞİL): kampanya aktifleşince her kitap için BİR kayıt.
// PK = ProductId → kitapta TEK ETKİN indirim; yeni kampanya kitabın kaydını EZER (SON-GELEN-KAZANIR,
// atlama yok). Satır hangi kampanyaya aitse (CampaignId) o kampanya bitince/iptalde temizlenir; başka
// kampanya sonradan ezdiyse eski kampanyanın bitişi bu satıra dokunmaz. FİYAT TUTMAZ — yalnız yüzde +
// pencere (etkin fiyatı tüketici kendi liste fiyatından hesaplar).
public class ProductDiscount
{
    private ProductDiscount() { }

    public Guid ProductId { get; private set; }
    public Guid CampaignId { get; private set; }
    public int Percentage { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }

    public static ProductDiscount Create(Guid productId, Guid campaignId, int percentage,
        DateTime startsAt, DateTime? endsAt) =>
        new()
        {
            ProductId = productId,
            CampaignId = campaignId,
            Percentage = percentage,
            StartsAt = startsAt,
            EndsAt = endsAt
        };

    /// <summary>Verilen an itibarıyla pencere içinde mi (checkout canlı doğrulama + gRPC filtresi).</summary>
    public bool IsActiveAt(DateTime now) => StartsAt <= now && (EndsAt is null || now < EndsAt.Value);
}
