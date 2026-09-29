namespace Library.Api.Domains.PriceAlarms.Features.Agents.Commands;

// MCP yazma slice'ı — agent chat'ten fiyat alarmını kaldırır (hard delete). İzole handler.
public static class RemovePriceAlarm
{
    [RequiredScope(AuthorizationScopes.LibraryWrite)]
    public record RemovePriceAlarmCommand(Guid UserId, Guid ProductId);

    public class RemovePriceAlarmResponse
    {
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class RemovePriceAlarmCommandHandler
    {
        public async Task<FeatureObjectResultModel<RemovePriceAlarmResponse>> Handle(
            RemovePriceAlarmCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var alarm = await session.Query<PriceAlarm>()
                .Where(x => x.UserId == cmd.UserId && x.ProductId == cmd.ProductId)
                .FirstOrDefaultAsync(ct);
            if (alarm is null)
                return FeatureObjectResultModel<RemovePriceAlarmResponse>.Error(
                    new MessageItem { Code = LibraryResourceConstants.PRICE_ALARM_NOT_FOUND });

            session.Delete(alarm);
            return FeatureObjectResultModel<RemovePriceAlarmResponse>.Ok(
                new RemovePriceAlarmResponse { Message = "Fiyat alarmı kaldırıldı." });
        }
    }
}

[McpServerToolType]
public static class RemovePriceAlarmMcpTool
{
    [McpServerTool(Name = Shared.LibraryTools.RemovePriceAlarm)]
    [Description(Shared.McpToolDescriptions.LibraryTools.RemovePriceAlarm)]
    public static Task<FeatureObjectResultModel<RemovePriceAlarm.RemovePriceAlarmResponse>> RemovePriceAlarmAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<RemovePriceAlarm.RemovePriceAlarmResponse>>(
            new RemovePriceAlarm.RemovePriceAlarmCommand(userId, productId), ct);
    }
}
