namespace Storefront.Api.Constants;

// Storefront context'ine ozel hata kodu sabitleri (Result pattern: Code serbest metin degil, sabittir).
public static class StorefrontResourceConstants
{
    // arama aninda embedding servisi erisilemez — filtre-yalniz arama etkilenmez (SC-005).
    public static readonly string STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE = "STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE";

    // benzerlik referansinin temsili yok (urun yok ya da aciklamasi henuz embed edilmedi) —
    // beklenen durum, hata degil (SC-002); Found=false + bu kod doner.
    public static readonly string STOREFRONT_SIMILARITY_SOURCE_UNAVAILABLE = "STOREFRONT_SIMILARITY_SOURCE_UNAVAILABLE";

    // query_storefront bekçi/çalıştırma ret-hata kodları (makine-okur; asistanın düzeltme
    // döngüsü bu kodlarla çalışır, FR-007). Bekçi = çalıştırma ÖNCESİ; Permission/Timeout/Execution = DB katmanı.
    public static readonly string AgentSqlMultiStatement = "AGENT_SQL_MULTI_STATEMENT";
    public static readonly string AgentSqlNotReadOnly = "AGENT_SQL_NOT_READ_ONLY";
    public static readonly string AgentSqlForbiddenKeyword = "AGENT_SQL_FORBIDDEN_KEYWORD";
    public static readonly string AgentSqlUnknownRelation = "AGENT_SQL_UNKNOWN_RELATION";
    public static readonly string AgentSqlTooLong = "AGENT_SQL_TOO_LONG";
    public static readonly string AgentSqlBadEmbedPlaceholder = "AGENT_SQL_BAD_EMBED_PLACEHOLDER";
    public static readonly string AgentSqlPermissionDenied = "AGENT_SQL_PERMISSION_DENIED";
    public static readonly string AgentSqlTimeout = "AGENT_SQL_TIMEOUT";
    public static readonly string AgentSqlExecutionFailed = "AGENT_SQL_EXECUTION_FAILED";

    public static readonly string INVALID_RANGE = "COMMON_MESSAGE_INVALID_RANGE";
    public static readonly string INVALID_VALUE = "COMMON_MESSAGE_INVALID_VALUE";
    public static readonly string VALUE_IS_REQUIRED = "COMMON_MESSAGE_VALUE_IS_REQUIRED";
}