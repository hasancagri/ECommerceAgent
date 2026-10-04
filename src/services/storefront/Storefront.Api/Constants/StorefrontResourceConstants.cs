namespace Storefront.Api.Constants;

// Storefront context'ine özel hata kodu sabitleri (Result pattern: Code serbest metin değil, sabittir).
public static class StorefrontResourceConstants
{
    // Arama anında embedding servisi erişilemez ({{EMBED}} çözülemedi) — çökme YOK; asistan dürüst
    // hata kodunu görüp semantik-siz sorguyu yeniden kurar (C3, spec edge).
    public static readonly string STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE = "STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE";

    // query_storefront ES DSL ret/hata kodları (makine-okur; asistanın düzeltme döngüsü, FR-009).
    // BadQuery = çalışmadan ret (bozuk JSON / bozuk {{EMBED}}); Timeout/ExecutionFailed = ES katmanı.
    public static readonly string STOREFRONT_SEARCH_BAD_QUERY = "STOREFRONT_SEARCH_BAD_QUERY";
    public static readonly string STOREFRONT_SEARCH_TIMEOUT = "STOREFRONT_SEARCH_TIMEOUT";
    public static readonly string STOREFRONT_SEARCH_EXECUTION_FAILED = "STOREFRONT_SEARCH_EXECUTION_FAILED";

    public static readonly string INVALID_RANGE = "COMMON_MESSAGE_INVALID_RANGE";
    public static readonly string INVALID_VALUE = "COMMON_MESSAGE_INVALID_VALUE";
    public static readonly string VALUE_IS_REQUIRED = "COMMON_MESSAGE_VALUE_IS_REQUIRED";
}