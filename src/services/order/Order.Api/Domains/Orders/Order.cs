namespace Order.Api.Domains.Orders;

public class Order : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public Guid BuyerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalPrice { get; private set; }
    public Guid? PaymentId { get; private set; }

    // yalniz Cancelled'da dolu; resource kodu tasir (UI cevirir).
    public string? CancelReason { get; private set; }
    public Address Address { get; private set; } = null!;
    public List<OrderItem> OrderItems { get; private set; } = [];

    private Order()
    {
    }

    // siparis Pending dogar; PaymentId idempotency alani olarak dogumda atanir.
    /// <summary>Yeni siparisi Pending durumunda, sifir tutarla ve verilen adres/paymentId ile olusturur.</summary>
    public static Order Create(Guid buyerId, Address address, Guid paymentId)
    {
        return new Order
        {
            Code = GenerateCode(),
            BuyerId = buyerId,
            Status = OrderStatus.Pending,
            TotalPrice = 0,
            PaymentId = paymentId,
            Address = address,
            OrderItems = []
        };
    }

    /// <summary>Siparise kalem ekler; ad/fiyat/adet dogrulamasi yapar ve toplam tutari yeniden hesaplar.</summary>
    public FeatureResultModel AddOrderItem(Guid productId, string productName, decimal unitPrice, int quantity = 1)
    {
        var messages = new List<MessageItem>();

        if (string.IsNullOrEmpty(productName))
            messages.Add(new MessageItem { Code = OrderResourceConstants.ORDER_ITEM_PRODUCT_NAME_REQUIRED });

        if (unitPrice <= 0)
            messages.Add(new MessageItem { Code = OrderResourceConstants.ORDER_ITEM_UNIT_PRICE_INVALID });

        if (quantity <= 0)
            messages.Add(new MessageItem { Code = OrderResourceConstants.ORDER_ITEM_QUANTITY_INVALID });

        if (messages.Count > 0)
            return FeatureResultModel.Error(messages);

        OrderItems.Add(OrderItem.Create(productId, productName, unitPrice, quantity));
        RecalculateTotalPrice();
        return FeatureResultModel.Ok();
    }

    // durum gecisleri aggregate'te korunur — yalniz Pending'den ileri gidilir.
    /// <summary>Siparisi Pending'den Confirmed'e gecirir; baska durumda gecis hatasi doner.</summary>
    public FeatureResultModel Confirm()
    {
        if (Status != OrderStatus.Pending)
            return FeatureResultModel.Error(new MessageItem
                { Property = nameof(Status), Code = OrderResourceConstants.ORDER_INVALID_STATUS_TRANSITION });

        Status = OrderStatus.Confirmed;
        return FeatureResultModel.Ok();
    }

    /// <summary>Siparisi Pending'den Cancelled'e gecirir ve iptal sebep kodunu kaydeder.</summary>
    public FeatureResultModel Cancel(string reasonCode)
    {
        if (Status != OrderStatus.Pending)
            return FeatureResultModel.Error(new MessageItem
                { Property = nameof(Status), Code = OrderResourceConstants.ORDER_INVALID_STATUS_TRANSITION });

        Status = OrderStatus.Cancelled;
        CancelReason = reasonCode;
        return FeatureResultModel.Ok();
    }

    private void RecalculateTotalPrice()
    {
        TotalPrice = OrderItems.Sum(x => x.UnitPrice * x.Quantity);
    }

    private static string GenerateCode()
    {
        var random = new Random();
        return string.Concat(Enumerable.Range(0, 10).Select(_ => random.Next(0, 10)));
    }
}

// int degerler eski adlarla birebir (1/2/3) — mevcut dev kayitlari migration'siz okunur.
public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3
}