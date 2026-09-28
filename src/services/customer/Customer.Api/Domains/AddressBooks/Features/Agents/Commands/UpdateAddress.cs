namespace Customer.Api.Domains.AddressBooks.Features.Agents.Commands;

// 063: MCP yazma slice'ı — agent chat'ten mevcut bir adresi günceller. İzole handler (bkz. AddAddress).
// 070 canlı-test dersi: tam-alan güncelleme LLM'e değişmeyen alanları YENİDEN yazdırıyordu ve model
// alan uydurabiliyordu (posta kodu 34674→34000 drift'i). KISMİ güncellemeye çevrildi: yalnız verilen
// alan değişir, verilmeyen mevcut değerinde kalır — model değişmeyeni hiç göndermez, uyduramaz.
public static class UpdateAddress
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    [InvalidatesCache("addresses")]
    public record UpdateAddressCommand(
        Guid UserId, Guid AddressId,
        string? Province, string? District, string? Street, string? ZipCode, string? Line);

    public class UpdateAddressResponse
    {
        public string Message { get; set; } = default!;
        public string Province { get; set; } = default!;
        public string District { get; set; } = default!;
        public string Street { get; set; } = default!;
        public string ZipCode { get; set; } = default!;
        public string Line { get; set; } = default!;
    }

    [Transactional]
    public class UpdateAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateAddressResponse>> Handle(
            UpdateAddressCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureObjectResultModel<UpdateAddressResponse>.NotFound();

            var existing = book.Addresses.FirstOrDefault(x => x.Id == cmd.AddressId);
            if (existing is null)
                return FeatureObjectResultModel<UpdateAddressResponse>.NotFound();

            // Kısmi birleştirme: verilen alan yenisi, verilmeyen mevcut değeri.
            var address = Address.Create(
                cmd.Province ?? existing.Value.Province,
                cmd.District ?? existing.Value.District,
                cmd.Street ?? existing.Value.Street,
                cmd.ZipCode ?? existing.Value.ZipCode,
                cmd.Line ?? existing.Value.Line);
            if (!address.IsSuccess)
                return FeatureObjectResultModel<UpdateAddressResponse>.Error(address.Messages);

            var result = book.UpdateAddress(cmd.AddressId, address.Data!);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateAddressResponse>.Error(result.Messages);

            session.Store(book);
            // FR-003 deseni: yanıt kaydın GÜNCEL hâli — agent ek okuma çağrısı yapmadan gösterebilir.
            return FeatureObjectResultModel<UpdateAddressResponse>.Ok(new UpdateAddressResponse
            {
                Message = "Adres güncellendi.",
                Province = address.Data!.Province,
                District = address.Data!.District,
                Street = address.Data!.Street,
                ZipCode = address.Data!.ZipCode,
                Line = address.Data!.Line,
            });
        }
    }
}

[McpServerToolType]
public static class UpdateAddressMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.UpdateAddress)]
    [Description(Shared.McpToolDescriptions.CustomerTools.UpdateAddress)]
    public static Task<FeatureObjectResultModel<UpdateAddress.UpdateAddressResponse>> UpdateAddressAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid addressId,
        CancellationToken ct,
        // MCP optional param DEFAULT sart (nullable yetmez) — verilmeyen alan mevcut degerinde kalir.
        [Description("Yeni il (degistirmeyeceksen gonderme)")] string? province = null,
        [Description("Yeni ilce (degistirmeyeceksen gonderme)")] string? district = null,
        [Description("Yeni cadde/sokak (degistirmeyeceksen gonderme)")] string? street = null,
        [Description("Yeni posta kodu (degistirmeyeceksen gonderme)")] string? zipCode = null,
        [Description("Yeni acik adres (degistirmeyeceksen gonderme)")] string? line = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<UpdateAddress.UpdateAddressResponse>>(
            new UpdateAddress.UpdateAddressCommand(
                userId, addressId, province, district, street, zipCode, line), ct);
    }
}
