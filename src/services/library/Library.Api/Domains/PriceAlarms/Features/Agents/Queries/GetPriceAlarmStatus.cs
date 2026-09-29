namespace Library.Api.Domains.PriceAlarms.Features.Agents.Queries;

// MCP okuma slice'ı — agent kullanıcının bu ürüne fiyat alarmı kurup kurmadığını sorar.
// İzole handler (konvansiyon). library.read scope.
public static class GetPriceAlarmStatus
{
    [RequiredScope(AuthorizationScopes.LibraryRead)]
    public record GetPriceAlarmStatusQuery(Guid UserId, Guid ProductId);

    public class PriceAlarmStatusResponse
    {
        public bool Exists { get; set; }
    }

    public class GetPriceAlarmStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<PriceAlarmStatusResponse>> Handle(
            GetPriceAlarmStatusQuery query, IQuerySession session, CancellationToken ct)
        {
            var exists = await session.Query<PriceAlarm>()
                .Where(x => x.UserId == query.UserId && x.ProductId == query.ProductId)
                .AnyAsync(ct);

            return FeatureObjectResultModel<PriceAlarmStatusResponse>.Ok(
                new PriceAlarmStatusResponse { Exists = exists });
        }
    }
}

[McpServerToolType]
public static class GetPriceAlarmStatusMcpTool
{
    [McpServerTool(Name = Shared.LibraryTools.GetPriceAlarm)]
    [Description(Shared.McpToolDescriptions.LibraryTools.GetPriceAlarm)]
    public static Task<FeatureObjectResultModel<GetPriceAlarmStatus.PriceAlarmStatusResponse>> GetPriceAlarmStatusAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetPriceAlarmStatus.PriceAlarmStatusResponse>>(
            new GetPriceAlarmStatus.GetPriceAlarmStatusQuery(userId, productId), ct);
    }
}
