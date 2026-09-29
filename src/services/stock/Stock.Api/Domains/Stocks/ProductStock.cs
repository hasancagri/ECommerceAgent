
namespace Stock.Api.Domains.Stocks;

public class ProductStock : AggregateRoot
{
    public Guid ProductId { get; private set; }

    // kalici alan adi Quantity (OnHand = fiziksel stok). Feed/Commit/restock disinda degismez.
    public int Quantity { get; private set; }

    // Domain OnHand semantigi (I1); ayni deger, okunabilirlik icin.
    [JsonIgnore] public int OnHand => Quantity;

    // islenmis saga operasyon anahtarlari ("orderId:commit" / "orderId:revert").
    // At-least-once teslimatta mukerrer Commit/RevertCommit'i no-op yapar. Bounded (son 100).
    [JsonProperty("ProcessedOps")] private List<string> _processedOps = new();
    private const int ProcessedOpsLimit = 100;

    private static string CommitKey(Guid orderId) => $"{orderId}:commit";
    private static string RevertKey(Guid orderId) => $"{orderId}:revert";

    private void MarkProcessed(string key)
    {
        _processedOps.Add(key);
        if (_processedOps.Count > ProcessedOpsLimit)
            _processedOps.RemoveRange(0, _processedOps.Count - ProcessedOpsLimit);
    }

    private ProductStock()
    {
    }

    /// <summary>Yeni bir ProductStock aggregate'i verilen urun ve baslangic adediyle olusturur.</summary>
    public static ProductStock Create(Guid productId, int quantity)
    {
        return new ProductStock
        {
            ProductId = productId,
            Quantity = quantity
        };
    }

    /// <summary>Stok adedini verilen miktar kadar artirir (restock).</summary>
    public ResultDomain Increase(int amount)
    {
        Quantity += amount;
        return ResultDomain.Ok();
    }

    /// <summary>Stok adedini verilen miktar kadar azaltir.</summary>
    public ResultDomain Decrease(int amount)
    {
        Quantity -= amount;
        return ResultDomain.Ok();
    }

    // 005-supplier-ingestion: feed mutlak adet verir; set semantigi Increase/Decrease'ten ayridir.
    // Invariant: stok adedi negatif olamaz — kural handler'da degil aggregate'te korunur.
    /// <summary>Feed mutlak adedini set eder; negatif adedi reddeder (invariant).</summary>
    public ResultDomain SetQuantity(int quantity)
    {
        if (quantity < 0)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Quantity),
                Code = StockResourceConstants.STOCK_QUANTITY_CANNOT_BE_NEGATIVE
            });

        Quantity = quantity;
        return ResultDomain.Ok();
    }

    // admin artir/azalt tek delta metodu — invariant (OnHand >= 0) aggregate'te; sifir delta anlamsiz.
    /// <summary>Stok adedini delta kadar oynatir (+/-); negatife dusus ve sifir delta reddedilir.</summary>
    public ResultDomain Adjust(int delta)
    {
        if (delta == 0)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Quantity),
                Code = StockResourceConstants.STOCK_ADJUST_INVALID
            });

        if (Quantity + delta < 0)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Quantity),
                Code = StockResourceConstants.STOCK_QUANTITY_CANNOT_BE_NEGATIVE
            });

        Quantity += delta;
        return ResultDomain.Ok();
    }

    // rezervasyon kalkti — sepet stok tutmaz; stok gercegi checkout anidir.
    // Commit = dogrudan dusum. Invariant'lar: yeterlilik (OnHand >= quantity, eksiye inmez) +
    // orderId idempotency (at-least-once teslimatta mukerrer Commit no-op).
    /// <summary>Checkout dususu: OnHand'den dogrudan duser; yetersizse hata; orderId ile idempotent.</summary>
    public ResultDomain Commit(int quantity, Guid orderId)
    {
        if (orderId == Guid.Empty || quantity <= 0)
            return ResultDomain.Error(new MessageItem
                { Code = StockResourceConstants.STOCK_COMMIT_INVALID });

        if (_processedOps.Contains(CommitKey(orderId)))
            return ResultDomain.Ok();

        if (quantity > Quantity)
            return ResultDomain.Error(new MessageItem
                { Code = StockResourceConstants.STOCK_INSUFFICIENT });

        Quantity -= quantity;
        MarkProcessed(CommitKey(orderId));
        return ResultDomain.Ok();
    }

    // saga telafisi — commit edilmis adedi stoga geri ekler. orderId ile idempotent;
    // yalniz daha once commit edilmis siparis geri alinabilir (kacak artis engellenir).
    /// <summary>Saga telafisi: commit edilmis adedi stoga geri ekler; orderId ile idempotent.</summary>
    public ResultDomain RevertCommit(int quantity, Guid orderId)
    {
        if (orderId == Guid.Empty || quantity <= 0)
            return ResultDomain.Error(new MessageItem
                { Code = StockResourceConstants.STOCK_REVERT_INVALID });

        if (_processedOps.Contains(RevertKey(orderId)))
            return ResultDomain.Ok();

        if (!_processedOps.Contains(CommitKey(orderId)))
            return ResultDomain.Error(new MessageItem
                { Code = StockResourceConstants.STOCK_REVERT_WITHOUT_COMMIT });

        Quantity += quantity;
        MarkProcessed(RevertKey(orderId));
        return ResultDomain.Ok();
    }
}