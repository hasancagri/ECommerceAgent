namespace Order.Api.Domains.Orders.Features.Agents.Commands;

// 093 PG pivotu: hosted-CF ödeme yolu söküldü, yeni pay-by-link kontratı henüz yok. Tool adı dış
// sözleşme olduğundan yüzey KALIR; handler sepete/adrese/Payment'a hiç gitmeden PAYMENT_DISABLED
// döner (Pending order YARATILMAZ). Yeni PG kontratı gelince bu handler yeniden yazılır.
public static class StartPayment
{
    [RequiredScope(AuthorizationScopes.OrderWrite)]
    public record StartPaymentCommand(Guid UserId);

    public class StartPaymentResponse
    {
        // ready / empty_basket / rejected / disabled
        public string Outcome { get; set; } = default!;
        public string? HostedUrl { get; set; }
        public Guid? OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Message { get; set; } = default!;
    }

    public class StartPaymentCommandHandler
    {
        public Task<FeatureObjectResultModel<StartPaymentResponse>> Handle(
            StartPaymentCommand cmd, CancellationToken ct)
        {
            return Task.FromResult(FeatureObjectResultModel<StartPaymentResponse>.Error(new MessageItem
            {
                Code = OrderResourceConstants.PAYMENT_DISABLED
            }));
        }
    }
}

// kullanıcı ödemeyi başlatmak istediğinde çağrılır; ödeme altyapısı yenilenene dek geçici kapalıdır
// ve PAYMENT_DISABLED hatası döner. Agent kullanıcıya ödemenin geçici kapalı olduğunu iletir.
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