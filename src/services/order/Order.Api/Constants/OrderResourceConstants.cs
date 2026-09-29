namespace Order.Api.Constants;

// Order context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class OrderResourceConstants
{
    public static readonly string ORDER_ITEM_PRODUCT_NAME_REQUIRED = "ORDER_ITEM_PRODUCT_NAME_REQUIRED";
    public static readonly string ORDER_ITEM_UNIT_PRICE_INVALID = "ORDER_ITEM_UNIT_PRICE_INVALID";
    public static readonly string ORDER_ITEM_QUANTITY_INVALID = "ORDER_ITEM_QUANTITY_INVALID";
    public static readonly string ORDER_PAYMENT_ALREADY_USED = "ORDER_PAYMENT_ALREADY_USED";

    // checkout saga.
    public static readonly string ORDER_INVALID_STATUS_TRANSITION = "ORDER_INVALID_STATUS_TRANSITION";
    public static readonly string ORDER_TIMEOUT = "ORDER_TIMEOUT";
    public static readonly string ORDER_STOCK_STEP_FAILED = "ORDER_STOCK_STEP_FAILED";

    // chat uzerinden uctan uca siparis tamamlama (place_order + PaymentAttempt).
    public static readonly string ORDER_PAYMENT_CHARGE_FAILED = "ORDER_PAYMENT_CHARGE_FAILED";
    public static readonly string ORDER_PAYMENT_VERIFY_FAILED = "ORDER_PAYMENT_VERIFY_FAILED";
    public static readonly string ORDER_PAYMENT_PENDING = "ORDER_PAYMENT_PENDING";
    public static readonly string ORDER_BASKET_EMPTY = "ORDER_BASKET_EMPTY";
    public static readonly string ORDER_PAYMENT_CONTEXT_MISSING = "ORDER_PAYMENT_CONTEXT_MISSING";

    // hosted-CF ödeme başlat (start_payment) — dış PG/link üretimi başarısız.
    public static readonly string PAYMENT_GATEWAY_UNAVAILABLE = "PAYMENT_GATEWAY_UNAVAILABLE";
    public static readonly string PAYMENT_BASKET_EMPTY = "PAYMENT_BASKET_EMPTY";
}