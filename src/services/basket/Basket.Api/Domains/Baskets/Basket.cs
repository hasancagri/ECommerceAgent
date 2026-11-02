namespace Basket.Api.Domains.Baskets;

public class Basket : AggregateRoot
{
    private Basket()
    {
    }

    /// <summary>Verilen kullanici icin bos yeni bir sepet olusturur.</summary>
    public static Basket Create(Guid userId)
    {
        return new Basket { UserId = userId };
    }

    // bir sepet satirinin sabit ust siniri. 056: stok tarafi siniri yok — tek otorite bu sabit.
    public const int MaxItemQuantity = 5;

    public Guid UserId { get; private set; }

    [JsonProperty("Items")] private List<BasketItem> _items = new();

    [JsonIgnore] public IReadOnlyList<BasketItem> Items => _items.AsReadOnly();

    /// <summary>Sepetteki tum satirlarin fiyat*adet toplamini doner.</summary>
    public decimal GetTotalPrice()
    {
        return _items.Sum(x => x.Price * x.Quantity);
    }

    /// <summary>Ayni Id'li satiri degistirip sepete kalemi ekler (upsert).</summary>
    public ResultDomain AddItem(BasketItem item)
    {
        var existing = _items.FirstOrDefault(x => x.Id == item.Id);
        if (existing is not null)
            _items.Remove(existing);

        _items.Add(item);
        return ResultDomain.Ok();
    }

    /// <summary>Verilen urunun sepetteki mevcut adedini doner (yoksa 0).</summary>
    public int GetItemQuantity(Guid productId) =>
        _items.FirstOrDefault(x => x.Id == productId)?.Quantity ?? 0;

    // sepet kalicidir — stok tutmaz, sure baslatmaz; stok gercegi checkout anindadir.
    /// <summary>Urunu verilen mutlak adede getirir (upsert).</summary>
    public ResultDomain SetItem(Guid id, string name, string? imageUrl, decimal price, int quantity)
    {
        var existing = _items.FirstOrDefault(x => x.Id == id);
        if (existing is null)
        {
            existing = new BasketItem(id, name, imageUrl, price);
            _items.Add(existing);
        }

        existing.SetQuantity(quantity);
        return ResultDomain.Ok();
    }

    // login aninda anonim sepet hesaba tasinir; adetler toplanir, tavan asilamaz.
    /// <summary>Diger sepetin kalemlerini bu sepete katar: ortak urunde adetler toplanir, MaxItemQuantity'ye sabitlenir.</summary>
    public ResultDomain MergeFrom(Basket other)
    {
        foreach (var incoming in other.Items)
        {
            var existing = _items.FirstOrDefault(x => x.Id == incoming.Id);
            if (existing is null)
            {
                existing = new BasketItem(incoming.Id, incoming.Name, incoming.ImageUrl, incoming.Price);
                existing.SetQuantity(Math.Min(incoming.Quantity, MaxItemQuantity));
                _items.Add(existing);
                continue;
            }

            existing.SetQuantity(Math.Min(existing.Quantity + incoming.Quantity, MaxItemQuantity));
        }

        return ResultDomain.Ok();
    }

    /// <summary>Satiri siler; yoksa NotFound doner.</summary>
    public FeatureResultModel RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return FeatureResultModel.NotFound();
        _items.Remove(item);
        return FeatureResultModel.Ok();
    }
}