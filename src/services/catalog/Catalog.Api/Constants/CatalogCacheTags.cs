namespace Catalog.Api.Constants;

/// <summary>
/// Catalog'un cache tag sabitleri — [Cached] ile [InvalidatesCache] AYNI const'u kullanır ki
/// typo'yla çift sessizce kopmasın (yanlış tag = boşaltma hiçbir şey bulamaz, hata da vermez).
/// Tag BC-İÇİ detaydır (Shared'a taşıma); DEĞERİ değiştirme — eski değerle yazılmış girdiler
/// TTL dolana dek yetim kalır.
/// </summary>
public static class CatalogCacheTags
{
    /// <summary>Yazar/yayınevi/kategori agent liste query'leri (get-or-create referans listeleri).</summary>
    public const string AgentLists = "agent-lists";
}