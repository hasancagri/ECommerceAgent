using Discount.Api.Domains.ProductCatalogRefs;

namespace Discount.Api;

// Catalog `ProductChangedEvent` → `ProductCatalogRef` upsert (süzgeç çözümü izdüşümü). Kaynak = Catalog
// (dosya adı kuralı: kaynak + Consumers). FİYAT/İSİM ALINMAZ — yalnız ürün↔taksonomi bağı + yayın durumu.
// Wolverine keşfi "Consumers" son-ekini taramaz → Program.cs IncludeType ZORUNLU.
public static class CatalogConsumers
{
    public static async Task Handle(
        IntegrationEvents.ProductChangedEvent evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var reference = await session.LoadAsync<ProductCatalogRef>(evt.ProductId, ct)
                        ?? ProductCatalogRef.Create(evt.ProductId);

        reference.Apply(
            evt.CategoryId,
            evt.Authors.Select(a => a.Id).ToList(),
            evt.PublisherId,
            // Yayın görünürlüğü = silinmemiş (IsDeleted tersi). Yalnız yayınlı kitaplar süzgece girer.
            published: !evt.IsDeleted);

        session.Store(reference);
        await session.SaveChangesAsync(ct);
    }
}
