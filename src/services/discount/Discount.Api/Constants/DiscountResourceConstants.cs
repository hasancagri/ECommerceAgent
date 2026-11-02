namespace Discount.Api.Constants;

// Discount context'ine özel hata kodu sabitleri (Result pattern: Code kaynak sabitidir).
public static class DiscountResourceConstants
{
    // Guard: ProductId boş Guid.
    public const string CAMPAIGN_PRODUCT_REQUIRED = "DISCOUNT_CAMPAIGN_PRODUCT_REQUIRED";

    // Guard: oran %1-%90 aralığı dışında.
    public const string CAMPAIGN_RATE_INVALID = "DISCOUNT_CAMPAIGN_RATE_INVALID";

    // Guard: zaman penceresi geçersiz (bitiş ≤ başlangıç).
    public const string CAMPAIGN_WINDOW_INVALID = "DISCOUNT_CAMPAIGN_WINDOW_INVALID";
}
