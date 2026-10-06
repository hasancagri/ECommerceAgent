namespace Customer.Api.Domains.AddressBooks.Features.Agents.Commands;

// MCP yazma slice'ı — agent chat'ten adres ekler (ekransız). Agent slice İZOLE: Commands'i
// IMessageBus ile reuse ETMEZ (bilinçli tekrar, [[agent-features-folder-convention]]); kendi
// handler'ını taşır, aggregate metodunu doğrudan çağırır. customer.write scope zorunlu (061 demeti).
public static class AddAddress
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    [InvalidatesCache(CustomerCacheTags.Addresses)]
    public record AddAddressCommand(
        Guid UserId, string Province, string District, string Street, string ZipCode, string Line);

    public class AddAddressResponse
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class AddAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddAddressResponse>> Handle(
            AddAddressCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var address = Address.Create(cmd.Province, cmd.District, cmd.Street, cmd.ZipCode, cmd.Line);
            if (!address.IsSuccess)
                return FeatureObjectResultModel<AddAddressResponse>.Error(address.Messages);

            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            book ??= AddressBook.Create(cmd.UserId);

            var saved = book.AddAddress(address.Data!);
            if (!saved.IsSuccess)
                return FeatureObjectResultModel<AddAddressResponse>.Error(saved.Messages);

            session.Store(book);
            return FeatureObjectResultModel<AddAddressResponse>.Ok(
                new AddAddressResponse { Id = saved.Data!.Id, Message = "Adres eklendi." });
        }
    }
}

[McpServerToolType]
public static class AddAddressMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.AddAddress)]
    [Description(Shared.McpToolDescriptions.CustomerTools.AddAddress)]
    public static Task<FeatureObjectResultModel<AddAddress.AddAddressResponse>> AddAddressAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        string province,
        string district,
        string street,
        string zipCode,
        string line,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AddAddress.AddAddressResponse>>(
            new AddAddress.AddAddressCommand(userId, province, district, street, zipCode, line), ct);
    }
}
