namespace Library.Api.Constants;

// Library context'ine ozel hata kodu sabitleri (Result pattern: Code kaynak sabitidir).
public static class LibraryResourceConstants
{
    // Kaldırılacak/sorgulanacak alarm bulunamadı.
    public const string PRICE_ALARM_NOT_FOUND = "PRICE_ALARM_NOT_FOUND";

    // Guard: UserId/ProductId boş Guid veya fiyat ≤ 0.
    public const string PRICE_ALARM_INVALID = "PRICE_ALARM_INVALID";
}