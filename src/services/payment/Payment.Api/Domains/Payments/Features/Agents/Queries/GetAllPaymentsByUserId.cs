namespace Payment.Api.Domains.Payments.Features.Agents.Queries;

// 077: get_my_payments artık PaymentIntent kayıtlarını listeler (mock Payment aggregate söküldü).
public static class GetAllPaymentsByUserId
{
    public record GetAllPaymentsByUserIdQuery(Guid UserId);

    public class GetAllPaymentsByUserIdResponse
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedTime { get; set; }
        public PaymentIntentStatus Status { get; set; }

        public static GetAllPaymentsByUserIdResponse From(PaymentIntent intent) => new()
        {
            Id = intent.Id,
            Amount = intent.Amount,
            CreatedTime = intent.CreatedTime,
            Status = intent.Status
        };
    }

    public class GetAllPaymentsByUserIdQueryHandler
    {
        public async Task<FeatureListResultModel<GetAllPaymentsByUserIdResponse>> Handle(
            GetAllPaymentsByUserIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var payments = await session.Query<PaymentIntent>()
                .Where(x => x.UserId == query.UserId)
                .ToListAsync(ct);

            if (!payments.Any())
            {
                return FeatureListResultModel<GetAllPaymentsByUserIdResponse>.NotFound();
            }

            return FeatureListResultModel<GetAllPaymentsByUserIdResponse>.Ok(
                payments.Select(GetAllPaymentsByUserIdResponse.From).ToList());
        }
    }
}

[McpServerToolType]
public static class GetAllPaymentsByUserIdMcpTool
{
    [McpServerTool(Name = Shared.PaymentTools.GetMyPayments)]
    [Description(Shared.McpToolDescriptions.PaymentTools.GetMyPayments)]
    public static Task<FeatureListResultModel<GetAllPaymentsByUserId.GetAllPaymentsByUserIdResponse>> GetAllPaymentsByUserIdAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureListResultModel<GetAllPaymentsByUserId.GetAllPaymentsByUserIdResponse>>(
            new GetAllPaymentsByUserId.GetAllPaymentsByUserIdQuery(userId), ct);
    }
}