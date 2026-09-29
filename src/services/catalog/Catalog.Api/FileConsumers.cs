namespace Catalog.Api;

// US2/T021: File.Api'nin CoverIngested event'ini tüketir (kaynak=File.Api → conventions kaynak-adı
// kuralı; Catalog'un İLK integration-event tüketicisi). ISBN'den ürünü bulur → Product.SetImage(url) →
// ProductChangedEvent yayar (Storefront read-model kapağı yansıtır — FR-008). Ürün taslak olsa da kapak
// düşer; yayın ayrı yolla (publish_imported). Ürün yoksa no-op (yarış: kapak üründen önce gelmez normalde).
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

        // Fat-event mapping (AdminRepublishProducts emsali): künye adlarını yükle.
        var authors = (await session.LoadManyAsync<Catalog.Api.Domains.Authors.Author>(ct, product.AuthorIds.ToArray()))
            .ToDictionary(a => a.Id);
        var publisher = await session.LoadAsync<Catalog.Api.Domains.Publishers.Publisher>(product.PublisherId, ct);
        var categoryId = product.Categories.Select(c => c.CategoryId).FirstOrDefault();
        var category = categoryId != Guid.Empty
            ? await session.LoadAsync<Catalog.Api.Domains.Categories.Category>(categoryId, ct)
            : null;

        await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
            product.Id, product.Name, product.FullDescription, product.Price.Amount,
            product.AuthorIds.Where(authors.ContainsKey)
                .Select(id => new IntegrationEvents.AuthorRef(id, authors[id].Name)).ToList(),
            product.PublisherId, publisher?.Name ?? string.Empty,
            categoryId, category?.Name ?? string.Empty,
            product.ImageUrl, IsDeleted: false, OldPrice: null));
    }
}
