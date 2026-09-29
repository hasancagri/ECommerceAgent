using Discount.Api.Domains.ProductCatalogRefs;

namespace Discount.Api.Domains.Campaigns;

// süzgeç → somut kitap seti (snapshot). Tek-kitap doğrudan (scopeRef = productId); kategori/yazar/
// yayınevi `ProductCatalogRef` izdüşümünden çözülür (yalnız yayınlı kitaplar). Saf `Resolve` çekirdeği
// (İLKE VI test-first) in-memory koleksiyonu süzer; `ResolveAsync` Marten'den hedefli okuyup ona delege
// eder (bilinçli tekrar: sorgu kaba önfiltre, saf çekirdek kararın tek kaynağı).
public static class CampaignSelectionResolver
{
    // Saf çekirdek: verilen katalog izdüşümünden süzgece uyan (yayınlı) kitap id'lerini döndürür.
    // Product süzgeci kataloğa BAKMAZ (scopeRef zaten productId). Çözülmeyen süzgeç → boş liste.
    public static IReadOnlyList<Guid> Resolve(
        ScopeType scopeType, Guid scopeRef, IReadOnlyCollection<ProductCatalogRef> catalog)
    {
        if (scopeType == ScopeType.Product)
            return [scopeRef];

        return catalog
            .Where(p => p.Published && Matches(p, scopeType, scopeRef))
            .Select(p => p.ProductId)
            .Distinct()
            .ToList();
    }

    private static bool Matches(ProductCatalogRef p, ScopeType scopeType, Guid scopeRef) => scopeType switch
    {
        ScopeType.Category => p.CategoryId == scopeRef,
        ScopeType.Author => p.AuthorIds.Contains(scopeRef),
        ScopeType.Publisher => p.PublisherId == scopeRef,
        _ => false
    };

    // Marten sarmalayıcı: hedefli sorgu ile aday izdüşümleri yükle, saf çekirdeğe delege et.
    public static async Task<IReadOnlyList<Guid>> ResolveAsync(
        ScopeType scopeType, Guid scopeRef, IQuerySession session, CancellationToken ct)
    {
        if (scopeType == ScopeType.Product)
            return [scopeRef];

        var candidates = scopeType switch
        {
            ScopeType.Category => await session.Query<ProductCatalogRef>()
                .Where(x => x.CategoryId == scopeRef).ToListAsync(ct),
            ScopeType.Author => await session.Query<ProductCatalogRef>()
                .Where(x => x.AuthorIds.Contains(scopeRef)).ToListAsync(ct),
            ScopeType.Publisher => await session.Query<ProductCatalogRef>()
                .Where(x => x.PublisherId == scopeRef).ToListAsync(ct),
            _ => []
        };

        return Resolve(scopeType, scopeRef, candidates);
    }
}
