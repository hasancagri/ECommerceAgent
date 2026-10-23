using Order.Api.Grpc;
using Order.Api.Domains.Orders.ValueObjects;

namespace Order.Api.Domains.Orders.Features.Agents.Commands;

// 094 pay-by-link rewire: Ayşe "öde" dediğinde sepet kalemlerini (gRPC, kalem otoritesi sunucuda) +
// aktif indirimi (gRPC, canlı) alır, Order Pending yaratır, Payment BC'ye S2S ile ödeme niyeti + PG
// hosted link ister, hosted URL'yi döner. Kart alanı chat'e GİRMEZ (İlke VIII). Adres chat-only'de
// atlanır. Tutar = liste − aktif indirim. Başarısız yolda (boş sepet / PG kapalı / mağaza Live değil)
// Order KALICI OLMAZ (yalnız başarıda session.Store → [Transactional] commit). 092 koreografi
// PaymentSucceeded ile tetiklenir (bu handler yalnız Pending yaratır).
public static class StartPayment
{
    [RequiredScope(AuthorizationScopes.OrderWrite)]
    public record StartPaymentCommand(Guid UserId);

    public class StartPaymentResponse
    {
        // ready / empty_basket / rejected
        public string Outcome { get; set; } = default!;
        public string? HostedUrl { get; set; }
        public Guid? OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Message { get; set; } = default!;
    }

    // Saf: liste fiyatı − aktif indirim yüzdesi → etkin satır fiyatı (kuruş, yukarı yuvarla). Test-first.
    public static IReadOnlyList<(Guid ProductId, string Name, decimal UnitPrice, int Quantity)> ComputeLines(
        IReadOnlyList<OrderItemDto> items, IReadOnlyDictionary<Guid, int> discounts)
    {
        return items.Select(i =>
        {
            var pct = discounts.TryGetValue(i.ProductId, out var p) ? p : 0;
            var effective = Math.Round(i.UnitPrice * (100 - pct) / 100m, 2, MidpointRounding.AwayFromZero);
            return (i.ProductId, i.ProductName, effective, i.Quantity);
        }).ToList();
    }

    [Transactional]
    public class StartPaymentCommandHandler
    {
        private static readonly Address EmptyAddress = new("", "", "", "", "");

        public async Task<FeatureObjectResultModel<StartPaymentResponse>> Handle(
            StartPaymentCommand cmd, IDocumentSession session, BasketItemsClientProxy basket,
            DiscountClient discount, PaymentIntentClient payment, CancellationToken ct)
        {
            var snapshot = await basket.GetItemsAsync(cmd.UserId, ct);
            if (!snapshot.Reachable)
                return Err(OrderResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
            if (snapshot.IsEmpty)
                return Err(OrderResourceConstants.PAYMENT_BASKET_EMPTY);

            var discounts = await discount.GetActiveAsync(snapshot.Items.Select(i => i.ProductId), ct);
            var lines = ComputeLines(snapshot.Items, discounts);

            // Order kalemleri ETKİN (indirimli) fiyatla → Order.TotalPrice = ödenecek tutar.
            var order = Order.Create(cmd.UserId, EmptyAddress, Guid.NewGuid());
            foreach (var l in lines)
            {
                var add = order.AddOrderItem(l.ProductId, l.Name, l.UnitPrice, l.Quantity);
                if (!add.IsSuccess)
                    return FeatureObjectResultModel<StartPaymentResponse>.Error(add.Messages);
            }

            var amount = order.TotalPrice;
            if (amount <= 0)
                return Err(OrderResourceConstants.PAYMENT_BASKET_EMPTY);

            var summary = $"{snapshot.Items.Count} kalem";
            var link = await payment.CreateAsync(
                order.Id, cmd.UserId, cmd.UserId.ToString(), amount, summary, ct);
            if (!link.IsSuccess)
                return Err(link.ErrorCode ?? OrderResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);

            // Yalnız başarıda kalıcı — başarısız yolda hiç Order commit edilmez (kirli Pending yok).
            session.Store(order);

            return FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
            {
                Outcome = "ready",
                HostedUrl = link.HostedUrl,
                OrderId = order.Id,
                Amount = amount,
                Message = "Ödeme bağlantınız hazır. Kartınızı güvenli sayfada girip ödemeyi tamamlayabilirsiniz."
            });
        }

        private static FeatureObjectResultModel<StartPaymentResponse> Err(string code)
            => FeatureObjectResultModel<StartPaymentResponse>.Error(new MessageItem { Code = code });
    }
}

// kullanıcı ödemeyi başlatmak istediğinde çağrılır; sepet tutarı hesaplanır, güvenli PG ödeme sayfasının
// linki döner. Kart bilgisi bu akıştan geçmez (yalnız hosted sayfada). Agent linki kullanıcıya iletir.
[McpServerToolType]
public static class StartPaymentMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.StartPayment)]
    [Description(Shared.McpToolDescriptions.OrderTools.StartPayment)]
    public static Task<FeatureObjectResultModel<StartPayment.StartPaymentResponse>> StartPaymentAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<StartPayment.StartPaymentResponse>>(
            new StartPayment.StartPaymentCommand(userId), ct);
    }
}
