namespace Storefront.Api.Consumers;

// Discount `ProductDiscountChanged` → StorefrontView.ApplyDiscount (push read-model). Kaynak = Discount
// (dosya adı kuralı: kaynak + Consumers). pct=0 → indirim temizlenir. Wolverine keşfi "Consumers" son-ekini
// taramaz → Program.cs IncludeType ZORUNLU; binding'i TÜKETİCİ kurar (007). Etkin fiyat burada TUTULMAZ
// (sorgu-zamanı view-guard + liste fiyatından hesaplanır).
public static class DiscountConsumers
{
    public static async Task Handle(
        IntegrationEvents.ProductDiscountChanged evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var view = await session.LoadAsync<Domains.StorefrontView.StorefrontView>(evt.ProductId, ct);
        // İndirim yalnız var olan (Catalog'tan gelmiş) satıra uygulanır; yoksa temizlik no-op.
        if (view is null)
            return;

        view.ApplyDiscount(evt.DiscountPct, evt.StartsAt, evt.EndsAt);
        session.Store(view);
        await session.SaveChangesAsync(ct);
    }
}
