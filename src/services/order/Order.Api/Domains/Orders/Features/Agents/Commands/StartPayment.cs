using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api.Domains.Orders.Features.Agents.Commands;

// US1: "ödeme yap" → hosted ödeme linki. LLM yalnız start_payment'i seçer (parametre yok); GERİSİ
// SUNUCU (LLM'siz): sepet kalemi (gRPC, sunucu-otoritesi) + varsayılan adres (Customer S2S) + order Pending
// + Payment S2S link isteği. Agent slice İZOLE: Features/Commands'i IMessageBus ile ÇAĞIRMAZ
// ([[agent-features-folder-convention]]); order oluşturmayı Domains davranışıyla doğrudan yapar. Re-use
// (Q1/A2): canlı intent varsa order OLUŞTURMADAN mevcut linki döner. Boş sepet = dostça mesaj (FR-018, exception yok).
public static class StartPayment
{
    // order.write: müşteri eylemi (Pending order oluşturur) — kullanıcı token'ı taşır, Wolverine
    // ScopeAuthorizationMiddleware zorlar. payment.write S2S/makine yetkisidir (Order→Payment link
    // isteği, SagaTokenHandler + Payment intents/live) → müşteri demetinde YOK, burada kullanılmaz.
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

    [Transactional]
    public class StartPaymentCommandHandler(
        IDocumentSession session,
        BasketItemsClientProxy basket,
        AddressClient addresses,
        PaymentIntentClient payments,
        DiscountClient discounts)
    {
        public async Task<FeatureObjectResultModel<StartPaymentResponse>> Handle(
            StartPaymentCommand cmd, CancellationToken ct)
        {
            // 1) Sepet kalemleri — sunucu-otoritesi (gRPC). Fail-closed: erişilemez → link yok.
            var snapshot = await basket.GetItemsAsync(cmd.UserId, ct);
            if (!snapshot.Reachable)
                return FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
                {
                    Outcome = "rejected",
                    Message = "Şu an ödeme başlatılamıyor, lütfen sonra tekrar dene."
                });
            if (snapshot.IsEmpty)
                return FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
                {
                    Outcome = "empty_basket",
                    Message = "Lütfen sepete ürün ekleyiniz."
                });

            var basketRef = BasketItemsClientProxy.ComputeBasketRef(snapshot.Items);

            // aktif indirim yüzdeleri (canlı; grace yok). Fail-closed → indirim yok = liste fiyatı.
            // Ödenecek tutar vitrin snapshot'ına DEĞİL bu canlı cevaba dayanır (SC-004).
            var discountMap = await discounts.GetActiveAsync(snapshot.Items.Select(i => i.ProductId), ct);
            decimal DiscountedUnit(OrderDtos.OrderItemDto i) =>
                discountMap.TryGetValue(i.ProductId, out var pct) && pct is > 0 and < 100
                    ? Math.Round(i.UnitPrice * (1 - pct / 100m), 2)
                    : i.UnitPrice;
            var amount = snapshot.Items.Sum(i => DiscountedUnit(i) * i.Quantity);

            // 2) Re-use: aynı kullanıcı+sepet için canlı intent varsa order oluşturmadan mevcut linki dön.
            var live = await payments.GetLiveAsync(cmd.UserId, basketRef, ct);
            if (live is not null)
                return FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
                {
                    Outcome = "ready",
                    HostedUrl = live.HostedUrl,
                    OrderId = live.OrderId,
                    Amount = amount,
                    Message = $"Ödemeni tamamlamak için bu bağlantıyı aç: {live.HostedUrl}"
                });

            // 3) Varsayılan adres (Customer S2S). Yoksa reddet (FR-001b: sipariş kullanıcının adresine gider).
            var address = await addresses.GetDefaultAsync(cmd.UserId, ct);
            if (address is null)
                return FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
                {
                    Outcome = "rejected",
                    Message = "Ödeme için kayıtlı bir varsayılan adres bulunamadı. Önce adres ekleyin."
                });

            // 4) Order Pending oluştur (Domains davranışı; kalem fiyat/adet sunucudan).
            var order = OrderAggregate.Create(cmd.UserId,
                new Address(address.Province, address.District, address.Street, address.ZipCode, address.Line),
                Guid.NewGuid());
            foreach (var item in snapshot.Items)
            {
                var add = order.AddOrderItem(item.ProductId, item.ProductName, DiscountedUnit(item), item.Quantity);
                if (!add.IsSuccess)
                    return FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
                    {
                        Outcome = "rejected",
                        Message = "Sepetteki bir ürün ödeme için uygun değil, lütfen sepeti kontrol et."
                    });
            }
            session.Store(order);

            // 5) TxRef üret + Payment link iste. Başarısız → order iptal (stok değmedi) + reddet.
            var txRef = Guid.NewGuid().ToString("N");
            var created = await payments.CreateAsync(order.Id, cmd.UserId, basketRef, amount, txRef, ct);
            if (created is null)
            {
                order.Cancel(OrderResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
                session.Store(order);
                return FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
                {
                    Outcome = "rejected",
                    Message = "Ödeme başlatılamadı, lütfen biraz sonra tekrar dene."
                });
            }

            return FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
            {
                Outcome = "ready",
                HostedUrl = created.HostedUrl,
                OrderId = order.Id,
                Amount = amount,
                Message = $"Ödemeni tamamlamak için bu bağlantıyı aç: {created.HostedUrl}"
            });
        }
    }
}

// kullanıcı ödemeyi başlatmak istediğinde sepet için hosted ödeme linki üretir. LLM yalnız bunu
// seçer (parametre YOK); tutar/adres/kalem SUNUCU tarafında belirlenir. Yanıttaki 'message' aynen iletilir.
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
