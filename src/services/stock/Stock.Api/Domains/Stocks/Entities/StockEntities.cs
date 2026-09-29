namespace Stock.Api.Domains.Stocks.Entities;

// barkod ↔ ProductId eşleme dokümanı (aggregate DEĞİL — read-model satırı gibi düz eşleme).
// ProductAdded handler'ı yazar (idempotent upsert); kanonik güncelleme tüketimi bu eşlemeden çözer.
public class BarcodeLink
{
    public string Id { get; private set; } = default!; // barkod
    public Guid ProductId { get; private set; }

    private BarcodeLink()
    {
    }

    public static BarcodeLink Create(string barcode, Guid productId)
        => new() { Id = barcode, ProductId = productId };
}