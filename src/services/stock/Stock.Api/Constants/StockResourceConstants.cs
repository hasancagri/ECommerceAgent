namespace Stock.Api.Constants;

// Stock context'ine ozel hata kodu sabitleri (Result pattern: Code serbest metin degil, sabittir).
public static class StockResourceConstants
{
    public static readonly string STOCK_QUANTITY_CANNOT_BE_NEGATIVE = "STOCK_QUANTITY_CANNOT_BE_NEGATIVE";

    // checkout dususu (rezervasyonsuz)
    public static readonly string STOCK_INSUFFICIENT = "STOCK_INSUFFICIENT";

    // admin artir/azalt (Adjust) — sifir delta anlamsiz.
    public static readonly string STOCK_ADJUST_INVALID = "STOCK_ADJUST_INVALID";

    public static readonly string RECORD_NOT_FOUND = "COMMON_MESSAGE_RECORD_NOT_FOUND";
    public static readonly string AMOUNT_MUST_BE_POSITIVE = "AMOUNT_MUST_BE_POSITIVE";

    // checkout stok dususu (092 koreografi — revert/telafi yok, atomik batch commit)
    public static readonly string STOCK_COMMIT_INVALID = "STOCK_COMMIT_INVALID";
}