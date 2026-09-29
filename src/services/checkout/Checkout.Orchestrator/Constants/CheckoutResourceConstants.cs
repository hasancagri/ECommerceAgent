namespace Checkout.Orchestrator.Constants;

// Checkout orchestrator hata/sebep kodu sabitleri (Result pattern: Code sabittir).
public static class CheckoutResourceConstants
{
    public static readonly string CHECKOUT_STOCK_STEP_FAILED = "CHECKOUT_STOCK_STEP_FAILED";
    // CHECKOUT_PAYMENT_CHARGE_FAILED söküldü (saga charge çekmez; ödeme hosted-CF ile öncedendir).
    public static readonly string CHECKOUT_ORDER_CREATE_FAILED = "CHECKOUT_ORDER_CREATE_FAILED";
    public static readonly string CHECKOUT_TIMEOUT = "CHECKOUT_TIMEOUT";
    public static readonly string CHECKOUT_EMPTY_ITEMS = "CHECKOUT_EMPTY_ITEMS";
}