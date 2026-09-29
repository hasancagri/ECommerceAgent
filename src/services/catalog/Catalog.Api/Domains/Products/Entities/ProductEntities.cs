namespace Catalog.Api.Domains.Products.Entities;


/// <summary>
/// fiyat geçmişi satırı — aggregate DEĞİL, append-only audit kaydı (davranışsız; konvansiyon
/// istisnası: read-model/kayıt sınıfı BC içinde ayrı yaşayabilir). İlk satır import/oluşturma fiyatı
/// (OldPrice=null), sonraki satırlar gerçek fiyat değişimleri. Fiyatla AYNI session'da yazılır — kayıp yok.
/// Müşteri-yüzü gösterim: 059 detay sayfası fiyat geçmişi (anonim GetProductPriceHistory query'si).
/// </summary>
public class ProductPriceChange
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal? OldPrice { get; private set; }
    public decimal NewPrice { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    private ProductPriceChange()
    {
    }

    public static ProductPriceChange Create(Guid productId, decimal? oldPrice, decimal newPrice, DateTime changedAtUtc)
    {
        return new ProductPriceChange
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OldPrice = oldPrice,
            NewPrice = newPrice,
            ChangedAtUtc = changedAtUtc,
        };
    }
}