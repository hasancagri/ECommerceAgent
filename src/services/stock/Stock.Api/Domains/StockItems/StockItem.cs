namespace Stock.Api.Domains.StockItems;

/// <summary>
/// Stok kaydı — Stock BC'nin kök aggregate'i (002, fiziki kitaba dönüş). Ürün başına TEK kayıt
/// (ProductId computed unique index — MartenExtensions); adet (OnHand) ≥0 invariant'ı her davranışta
/// korunur. Envanter kurgusaldır; rezervasyon/lokasyon kavramı YOK (056 kararı sürer — stok gerçeği
/// sipariş anında düşülür). Adedin TEK yazarı bu BC'dir; Catalog yalnız StockChanged event'iyle
/// beslenen kopyayı okur.
/// </summary>
public class StockItem : AggregateRoot
{
    public Guid ProductId { get; private set; }
    public int OnHand { get; private set; }

    private StockItem()
    {
    }

    /// <summary>Ürün için stok kaydı açar. Guard'lar: ProductId boş olamaz, adet negatif olamaz
    /// (0 geçerli — satış-dışı/tükenmiş kitap kayıtla doğabilir).</summary>
    public static ResultDomain<StockItem> Create(Guid productId, int onHand)
    {
        if (productId == Guid.Empty)
            return ResultDomain<StockItem>.Error(new MessageItem
            { Property = nameof(ProductId), Code = StockResourceConstants.STOCK_PRODUCT_REQUIRED });
        if (onHand < 0)
            return ResultDomain<StockItem>.Error(new MessageItem
            { Property = nameof(OnHand), Code = StockResourceConstants.STOCK_QUANTITY_INVALID });

        return ResultDomain<StockItem>.Ok(new StockItem
        {
            ProductId = productId,
            OnHand = onHand,
        });
    }

    /// <summary>Adedi MUTLAK değerle atar (admin girişi). Negatif reddedilir; geçersizde mevcut
    /// adet değişmez.</summary>
    public ResultDomain SetQuantity(int quantity)
    {
        if (quantity < 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(OnHand), Code = StockResourceConstants.STOCK_QUANTITY_INVALID });
        OnHand = quantity;
        return ResultDomain.Ok();
    }

    /// <summary>Satış düşümü. Miktar pozitif olmalı; eldeki yetersizse reddedilir (aşırı-satış
    /// engeli) ve mevcut adet değişmez.</summary>
    public ResultDomain Decrease(int amount)
    {
        if (amount <= 0)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(amount), Code = StockResourceConstants.STOCK_DECREASE_AMOUNT_INVALID });
        if (amount > OnHand)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(OnHand), Code = StockResourceConstants.STOCK_INSUFFICIENT });
        OnHand -= amount;
        return ResultDomain.Ok();
    }
}