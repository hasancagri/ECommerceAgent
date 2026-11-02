
namespace Common.Tests;

public class ApiKeyAuthenticationHandlerTests
{
    [Fact]
    public async Task NoHeader_ReturnsNoResult()
    {
        var result = await RunAsync(headerValue: null, HttpStatusCode.OK, body: null);

        result.None.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidKey_Resolve200_SucceedsWithScopeClaims()
    {
        var body = """{"userId":"u1","email":"a@b.c","scopes":["basket.write"]}""";

        var result = await RunAsync("umk_x", HttpStatusCode.OK, body);

        result.Succeeded.ShouldBeTrue();
        result.Principal!.FindFirst("sub")!.Value.ShouldBe("u1");
        result.Principal!.FindFirst("email")!.Value.ShouldBe("a@b.c");
        result.Principal!.HasClaim("scope", "basket.write").ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidKey_Resolve401_Fails()
    {
        var result = await RunAsync("umk_bad", HttpStatusCode.Unauthorized, body: null);

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
    }

    private static async Task<AuthenticateResult> RunAsync(string? headerValue, HttpStatusCode status, string? body)
    {
        var ctx = new DefaultHttpContext();
        if (headerValue is not null)
            ctx.Request.Headers[ApiKeyAuthenticationOptions.HeaderName] = headerValue;

        var client = new HttpClient(new StubMessageHandler(status, body)) { BaseAddress = new Uri("https://id.local") };
        var handler = new ApiKeyAuthenticationHandler(
            new StubOptionsMonitor(new ApiKeyAuthenticationOptions { InternalSecret = "s" }),
            NullLoggerFactory.Instance, UrlEncoder.Default, new StubHttpClientFactory(client));

        await handler.InitializeAsync(
            new AuthenticationScheme(
                ApiKeyAuthenticationDefaults.Scheme, ApiKeyAuthenticationDefaults.Scheme,
                typeof(ApiKeyAuthenticationHandler)),
            ctx);

        return await handler.AuthenticateAsync();
    }

    private sealed class StubMessageHandler(HttpStatusCode status, string? body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var resp = new HttpResponseMessage(status);
            if (body is not null)
                resp.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return Task.FromResult(resp);
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubOptionsMonitor(ApiKeyAuthenticationOptions value) : IOptionsMonitor<ApiKeyAuthenticationOptions>
    {
        public ApiKeyAuthenticationOptions CurrentValue => value;
        public ApiKeyAuthenticationOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ApiKeyAuthenticationOptions, string?> listener) => null;
    }
}