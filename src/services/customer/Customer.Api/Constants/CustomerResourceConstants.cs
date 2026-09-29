namespace Customer.Api.Constants;

// Customer context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class CustomerResourceConstants
{
    public static readonly string INVALID_OPERATION_ERROR = "COMMON_MESSAGE_INVALID_OPERATION_ERROR";
    public static readonly string INVALID_VALUE = "COMMON_MESSAGE_INVALID_VALUE";
    public static readonly string VALUE_IS_REQUIRED = "COMMON_MESSAGE_VALUE_IS_REQUIRED";
    public static readonly string RECORD_NOT_FOUND = "COMMON_MESSAGE_RECORD_NOT_FOUND";

    // DropShop onboarding sarmalayıcısı — PG erişilemez/yanıt çözülemez ("şu an yapılamıyor").
    public static readonly string MERCHANT_ONBOARDING_UNAVAILABLE = "MERCHANT_ONBOARDING_UNAVAILABLE";

    // FR-013: ekrandan girilen MerchantId+Key ikilisi PG doğrulamasından geçemedi.
    public static readonly string MERCHANT_CREDENTIALS_INVALID = "MERCHANT_CREDENTIALS_INVALID";
}