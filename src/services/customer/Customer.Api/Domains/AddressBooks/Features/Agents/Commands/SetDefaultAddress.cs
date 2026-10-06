namespace Customer.Api.Domains.AddressBooks.Features.Agents.Commands;

// MCP yazma slice'ı — agent chat'ten varsayılan adresi belirler. İzole handler (bkz. AddAddress).
public static class SetDefaultAddress
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    [InvalidatesCache(CustomerCacheTags.Addresses)]
    public record SetDefaultAddressCommand(Guid UserId, Guid AddressId);

    public class SetDefaultAddressResponse
    {
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class SetDefaultAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetDefaultAddressResponse>> Handle(
            SetDefaultAddressCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureObjectResultModel<SetDefaultAddressResponse>.NotFound();

            var result = book.SetDefaultAddress(cmd.AddressId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SetDefaultAddressResponse>.Error(result.Messages);

            session.Store(book);
            return FeatureObjectResultModel<SetDefaultAddressResponse>.Ok(
                new SetDefaultAddressResponse { Message = "Varsayılan adres güncellendi." });
        }
    }
}

[McpServerToolType]
public static class SetDefaultAddressMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.SetDefaultAddress)]
    [Description(Shared.McpToolDescriptions.CustomerTools.SetDefaultAddress)]
    public static Task<FeatureObjectResultModel<SetDefaultAddress.SetDefaultAddressResponse>> SetDefaultAddressAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid addressId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<SetDefaultAddress.SetDefaultAddressResponse>>(
            new SetDefaultAddress.SetDefaultAddressCommand(userId, addressId), ct);
    }
}
