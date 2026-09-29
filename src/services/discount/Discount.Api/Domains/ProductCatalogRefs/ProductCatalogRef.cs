namespace Discount.Api.Domains.ProductCatalogRefs;

// destek read-model (aggregate DEĞİL): Catalog `ProductChangedEvent`'inden beslenen izdüşüm;
// süzgeci (kategori/yazar/yayınevi) kitap setine çözmek için. FİYAT/İSİM TUTMAZ (BC izolasyonu —
// Discount.Api yalnız süzgeç çözümü için gereken ürün↔taksonomi bağını taşır). PK = ProductId.
public class ProductCatalogRef
{
    private ProductCatalogRef() { }

    public Guid ProductId { get; private set; }
    public Guid CategoryId { get; private set; }
    public List<Guid> AuthorIds { get; private set; } = [];
    public Guid PublisherId { get; private set; }
    public bool Published { get; private set; }

    public static ProductCatalogRef Create(Guid productId) => new() { ProductId = productId };

    public void Apply(Guid categoryId, IReadOnlyCollection<Guid> authorIds, Guid publisherId, bool published)
    {
        CategoryId = categoryId;
        AuthorIds = authorIds.ToList();
        PublisherId = publisherId;
        Published = published;
    }
}
