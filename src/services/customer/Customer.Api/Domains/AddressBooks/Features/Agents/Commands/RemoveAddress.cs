namespace Customer.Api.Domains.AddressBooks.Features.Agents.Commands;

// MCP yazma slice'ı — agent chat'ten adres siler. İzole handler (bkz. AddAddress).
public static class RemoveAddress
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    public record RemoveAddressCommand(Guid UserId, Guid AddressId);

    public class RemoveAddressResponse
    {
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class RemoveAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<RemoveAddressResponse>> Handle(
            RemoveAddressCommand cmd, IDocumentSession session, IMessageBus bus, CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureObjectResultModel<RemoveAddressResponse>.NotFound();

            var result = book.RemoveAddress(cmd.AddressId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RemoveAddressResponse>.Error(result.Messages);

            session.Store(book);
            // Boşaltma yazmayla ATOMİK: handler-içi publish [Transactional] outbox'una girer (090).
            await bus.PublishAsync(new CacheInvalidationRequested(CustomerCacheTags.Addresses));
            return FeatureObjectResultModel<RemoveAddressResponse>.Ok(
                new RemoveAddressResponse { Message = "Adres silindi." });
        }
    }
}

[McpServerToolType]
public static class RemoveAddressMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.RemoveAddress)]
    [Description(Shared.McpToolDescriptions.CustomerTools.RemoveAddress)]
    public static Task<FeatureObjectResultModel<RemoveAddress.RemoveAddressResponse>> RemoveAddressAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid addressId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<RemoveAddress.RemoveAddressResponse>>(
            new RemoveAddress.RemoveAddressCommand(userId, addressId), ct);
    }
}
