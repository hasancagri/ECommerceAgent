using System.Net;
using System.Text;
using Payment.Api.Constants;
using Payment.Api.Grpc;
using Payment.Api.Options;

namespace Payment.Api.Tests.Domains;

// 094 US3 + istemci sağlamlığı: PgPaymentLinkClient PG yanıt→Result eşlemesi. Host/PG yok — sahte
// HttpMessageHandler canned yanıt döner (hafif, DI yok). STORE_NOT_LIVE ayrımı + fail-closed gateway.
public class PgPaymentLinkClientTests
{
    private static PgPaymentLinkOptions Options() => new()
    {
        BaseUrl = "http://pg.test",
        StoreApiKey = "dev-ec-pg-key",
        CallbackSecret = "s",
        MerchantId = Guid.NewGuid(),
        StoreName = "Kitapçı",
        ReturnUrl = "http://ec.test/checkout/done"
    };

    private static PgPaymentLinkClient Client(StubHandler handler)
        => new(new HttpClient(handler), Options());

    [Fact]
    public async Task CreateLinkAsync_Success_ReturnsLink()
    {
        var handler = StubHandler.Json(HttpStatusCode.OK,
            "{\"linkUrl\":\"http://pg.test/payment-links/tok\",\"pgPaymentRef\":\"PGREF-1\"}");

        var result = await Client(handler).CreateLinkAsync("order-1", 100.50m, "1 kalem", default);

        result.IsSuccess.ShouldBeTrue();
        result.LinkUrl.ShouldBe("http://pg.test/payment-links/tok");
        result.PgPaymentRef.ShouldBe("PGREF-1");
    }

    [Fact]
    public async Task CreateLinkAsync_StoreNotLive_ReturnsStoreNotLiveCode()
    {
        var handler = StubHandler.Json(HttpStatusCode.BadRequest,
            "{\"isSuccess\":false,\"messages\":[{\"code\":\"STORE_ONBOARDING_STORE_NOT_LIVE\"}]}");

        var result = await Client(handler).CreateLinkAsync("order-1", 100.50m, "1 kalem", default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(PaymentResourceConstants.PAYMENT_STORE_NOT_LIVE);
    }

    [Fact]
    public async Task CreateLinkAsync_ServerError_ReturnsGatewayUnavailable()
    {
        var handler = StubHandler.Json(HttpStatusCode.InternalServerError, "{}");

        var result = await Client(handler).CreateLinkAsync("order-1", 100.50m, "1 kalem", default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(PaymentResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
    }

    [Fact]
    public async Task CreateLinkAsync_NetworkFailure_ReturnsGatewayUnavailable()
    {
        var handler = StubHandler.Throws();

        var result = await Client(handler).CreateLinkAsync("order-1", 100.50m, "1 kalem", default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(PaymentResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
    }

    [Fact]
    public async Task CreateLinkAsync_EmptyLinkUrl_ReturnsGatewayUnavailable()
    {
        var handler = StubHandler.Json(HttpStatusCode.OK, "{\"linkUrl\":\"\",\"pgPaymentRef\":\"\"}");

        var result = await Client(handler).CreateLinkAsync("order-1", 100.50m, "1 kalem", default);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(PaymentResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
    }

    [Fact]
    public async Task CreateLinkAsync_Always_SendsApiKeyHeaderToPaymentLinks()
    {
        var handler = StubHandler.Json(HttpStatusCode.OK,
            "{\"linkUrl\":\"http://pg.test/payment-links/tok\",\"pgPaymentRef\":\"PGREF-1\"}");

        await Client(handler).CreateLinkAsync("order-1", 100.50m, "1 kalem", default);

        handler.LastRequest!.RequestUri!.ToString().ShouldBe("http://pg.test/payment-links");
        handler.LastRequest.Headers.GetValues("X-Api-Key").ShouldContain("dev-ec-pg-key");
    }

    // Sahte transport — canned yanıt / atış; son isteği yakalar (başlık/URL doğrulaması).
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly bool _throw;
        public HttpRequestMessage? LastRequest { get; private set; }

        private StubHandler(HttpResponseMessage? response, bool shouldThrow)
        {
            _response = response;
            _throw = shouldThrow;
        }

        public static StubHandler Json(HttpStatusCode status, string body) =>
            new(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, false);

        public static StubHandler Throws() => new(null, true);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                await request.Content.LoadIntoBufferAsync(); // içerik okunabilir kalsın
            if (_throw)
                throw new HttpRequestException("network down");
            return _response!;
        }
    }
}
