namespace Customer.Api.Constants;

/// <summary>
/// Customer'ın cache tag sabitleri — [Cached] ile [InvalidatesCache] AYNI const'u kullanır ki
/// typo'yla çift sessizce kopmasın (yanlış tag = boşaltma hiçbir şey bulamaz, hata da vermez).
/// Tag BC-İÇİ detaydır (Shared'a taşıma); DEĞERİ değiştirme — eski değerle yazılmış girdiler
/// TTL dolana dek yetim kalır.
/// </summary>
public static class CustomerCacheTags
{
    /// <summary>Adres defteri okuma/yazma yüzeyi (list_addresses + adres command'leri).</summary>
    public const string Addresses = "addresses";
}