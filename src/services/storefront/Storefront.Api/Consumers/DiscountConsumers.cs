namespace Storefront.Api.Consumers;

// 086: Discount `ProductDiscountChanged` → ürün stream'ine append. "Satır yoksa no-op" mantığı KALKTI
// (append her zaman); fold pct=0'ı temizlik olarak uygular, etkin fiyatı projeksiyon yazım-anında hesaplar.
// Wolverine keşfi "Consumers" son-ekini taramaz → Program.cs IncludeType ZORUNLU.
public static class DiscountConsumers
{
    public static async Task Handle(IntegrationEvents.ProductDiscountChanged evt, IDocumentSession session, CancellationToken ct)
    {
        session.Events.Append(evt.ProductId, evt);
        await session.SaveChangesAsync(ct);
    }
}