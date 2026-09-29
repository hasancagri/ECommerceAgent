namespace Payment.Api.Constants;

// Payment context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class PaymentResourceConstants
{
    public static readonly string PAYMENT_USER_ID_REQUIRED = "PAYMENT_USER_ID_REQUIRED";
    public static readonly string PAYMENT_AMOUNT_INVALID = "PAYMENT_AMOUNT_INVALID";

    // hosted-CF PaymentIntent aggregate + akış kodları (049 charge kodları söküldü).
    public static readonly string PAYMENT_ORDER_ID_REQUIRED = "PAYMENT_ORDER_ID_REQUIRED";
    public static readonly string PAYMENT_INTENT_TXREF_REQUIRED = "PAYMENT_INTENT_TXREF_REQUIRED";
    public static readonly string PAYMENT_INTENT_HOSTED_URL_REQUIRED = "PAYMENT_INTENT_HOSTED_URL_REQUIRED";
    public static readonly string PAYMENT_INTENT_INVALID_TRANSITION = "PAYMENT_INTENT_INVALID_TRANSITION";
    public static readonly string PAYMENT_INTENT_NOT_FOUND = "PAYMENT_INTENT_NOT_FOUND";
    public static readonly string PAYMENT_MERCHANT_KEY_UNAVAILABLE = "PAYMENT_MERCHANT_KEY_UNAVAILABLE";
    public static readonly string PAYMENT_GATEWAY_UNAVAILABLE = "PAYMENT_GATEWAY_UNAVAILABLE";
}
