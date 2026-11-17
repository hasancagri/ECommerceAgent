using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace Customer.Api.Domains.AddressBooks.Features.Agents.Queries;

// 007 US2: kullanıcının adres defteri. Scope: customer.read. İç addressId (GUID) DÖNMEZ (İlke VIII /
// FR-006). YALNIZ çağıranın defteri (UserId token'dan).
public static class GetMyAddresses
{
    public record Query(Guid UserId);

    public class AddressItem
    {
        public string Province { get; set; } = default!;
        public string District { get; set; } = default!;
        public string Street { get; set; } = default!;
        public string Line { get; set; } = default!;
        public string ZipCode { get; set; } = default!;
        public bool IsDefault { get; set; }
    }

    public class Response
    {
        public IReadOnlyList<AddressItem> Addresses { get; set; } = [];
    }

    public class GetMyAddressesHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<Response>> Handle(Query query, CancellationToken ct)
        {
            if (query.UserId == Guid.Empty)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(b => b.UserId == query.UserId, ct);

            if (book is null)
                return FeatureObjectResultModel<Response>.Ok(new Response());

            var addresses = book.Addresses.Select(a => new AddressItem
            {
                Province = a.Value.Province,
                District = a.Value.District,
                Street = a.Value.Street,
                Line = a.Value.Line,
                ZipCode = a.Value.ZipCode,
                IsDefault = a.IsDefault,
            }).ToList();

            return FeatureObjectResultModel<Response>.Ok(new Response { Addresses = addresses });
        }
    }

    [McpServerToolType]
    public static class Tool
    {
        [McpServerTool(Name = "get_my_addresses")]
        [Description("Giriş yapmış kullanıcının kayıtlı adreslerini döner (il, ilçe, sokak, satır, posta " +
                     "kodu, varsayılan mı). YALNIZ çağıranın verisi; iç kimlik gösterilmez.")]
        public static async Task<Response> GetMyAddresses(
            IMessageBus bus, ICurrentUser currentUser, IHttpContextAccessor http,
            CancellationToken cancellationToken = default)
        {
            currentUser.Load(http.HttpContext!.User);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<Response>>(
                new Query(currentUser.Id), cancellationToken);
            return result.Data ?? new Response();
        }
    }
}
