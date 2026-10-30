namespace Catalog.Api;

// File.Api'nin CoverIngested event'ini tüketir (kaynak=File.Api → conventions kaynak-adı kuralı).
// Temiz-sayfa sökümünde (7c641d9) yanlışlıkla silinmişti — 095'te yeni modele göre geri yazıldı
// (FR-011). ISBN'den ürünü bulur → Product.SetImage(url) → fat ProductChangedEvent (kategori ad+id
// zinciri yayın anında ağaçtan — CategoryNaming.ResolveChain, seed ile ortak tek kaynak). Ürün yoksa
// no-op (yarış: kapak üründen önce gelmez normalde). Keşif: sınıf *Consumers → IncludeType ZORUNLU
// (MessagingExtensions; yoksa mesaj sessizce yutulur).
public class FileConsumers(IDocumentSession session, IMessageBus bus)
{
    [Transactional]
    public async Task Handle(IntegrationEvents.CoverIngested message, CancellationToken ct)
    {
        var product = await session.Query<Product>().FirstOrDefaultAsync(p => p.Gtin == message.Isbn, ct);
        if (product is null)
            return;

        product.SetImage(message.Url);
        session.Store(product);

        // Fat-event mapping: künye adlarını + kategori zincirini (leaf + ata) yükle.
        var authors = (await session.LoadManyAsync<Catalog.Api.Domains.Authors.Author>(ct, product.AuthorIds.ToArray()))
            .ToDictionary(a => a.Id);
        var publisher = await session.LoadAsync<Catalog.Api.Domains.Publishers.Publisher>(product.PublisherId, ct);

        var chainMap = new Dictionary<Guid, Category>();
        var leaf = await session.LoadAsync<Category>(product.CategoryId, ct);
        if (leaf is not null)
        {
            chainMap[leaf.Id] = leaf;
            if (leaf.ParentCategoryId is Guid parentId
                && await session.LoadAsync<Category>(parentId, ct) is { } parent)
                chainMap[parentId] = parent;
        }
        var (categoryIds, categoryNames) = CategoryNaming.ResolveChain(product.CategoryId, chainMap);

        await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
            product.Id, product.Name, product.Description, product.Price.Amount,
            product.AuthorIds.Where(authors.ContainsKey)
                .Select(id => new IntegrationEvents.AuthorRef(id, authors[id].Name)).ToList(),
            product.PublisherId, publisher?.Name ?? string.Empty,
            product.CategoryId, leaf?.Name ?? string.Empty,
            product.ImageUrl, IsDeleted: false, OldPrice: null,
            Categories: categoryNames,
            CategoryIds: categoryIds));
    }
}
