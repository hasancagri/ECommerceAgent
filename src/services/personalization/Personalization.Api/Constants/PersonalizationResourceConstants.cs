namespace Personalization.Api.Constants;

// Personalization context'ine özel hata kodu sabitleri (Result pattern: Code kaynak sabitidir).
public static class PersonalizationResourceConstants
{
    // Guard: UserId boş Guid (login şart — anonim kayıt yok).
    public const string SEARCH_USER_REQUIRED = "SEARCH_USER_REQUIRED";

    // Guard: aranan metin boş/whitespace.
    public const string SEARCH_QUERY_EMPTY = "SEARCH_QUERY_EMPTY";

    // Guard: sonuç sayısı negatif olamaz (0 geçerli — sonuçsuz arama kaydedilir).
    public const string SEARCH_RESULT_COUNT_INVALID = "SEARCH_RESULT_COUNT_INVALID";
}
