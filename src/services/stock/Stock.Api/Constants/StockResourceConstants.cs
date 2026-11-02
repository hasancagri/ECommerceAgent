namespace Stock.Api.Constants;

// Stock context'ine özel hata kodu sabitleri (Result pattern: Code kaynak sabitidir).
public static class StockResourceConstants
{
    // Guard: ProductId boş Guid.
    public const string STOCK_PRODUCT_REQUIRED = "STOCK_PRODUCT_REQUIRED";

    // Guard: adet negatif olamaz (Create/SetQuantity).
    public const string STOCK_QUANTITY_INVALID = "STOCK_QUANTITY_INVALID";

    // Guard: düşüm miktarı pozitif olmalı.
    public const string STOCK_DECREASE_AMOUNT_INVALID = "STOCK_DECREASE_AMOUNT_INVALID";

    // Guard: eldeki adet yetersiz — düşüm reddedilir (aşırı-satış engeli).
    public const string STOCK_INSUFFICIENT = "STOCK_INSUFFICIENT";
}
