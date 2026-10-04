namespace Storefront.Api.Consumers;

// 086: Catalog `ProductChangedEvent` → ürün stream'ine append (eski upsert read-model SÖKÜLDÜ).
// Fold + embedding + ES yazımı async projeksiyonun işi (StorefrontProjection). Burada yalnız durable
// inbox'a yazım: event ürün stream'ine eklenir, gerçek-kaynak Postgres event-log. Wolverine keşfi
// "Consumers" son-ekini taramaz → Program.cs IncludeType ZORUNLU.
public static class CatalogConsumers
{
    public static async Task Handle(
        IntegrationEvents.ProductChangedEvent evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        session.Events.Append(evt.ProductId, evt);
        await session.SaveChangesAsync(ct);
    }
}