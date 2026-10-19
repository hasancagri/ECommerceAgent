using Common.Utils.Authorization;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Http;
using Wolverine;
using Shared;

namespace Basket.Api.Tests;

// 092 koreografi regresyonu: sepet temizliği artık OrderCompleted event'inden (Basket.OrderConsumers,
// HttpContext YOK) tetiklenir. Event üzerinde [RequiredScope] OLMAMALI — varsa ScopeAuthorizationMiddleware
// .Before, HttpContext null olduğundan UnauthorizedAccessException fırlatır → handler fırlar → sepet
// temizlenmez, mesaj DLQ'ya düşer. (077 bug #7'nin eski ClearBasketCommand karşılığı; komut → event.)
public class ClearBasketScopeGuardTests
{
    private sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    // Kontrast probu: [RequiredScope] tasiyan bir mesaj HttpContext yoksa engellenmeli.
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    private sealed record ScopedProbe;

    [Fact]
    public void OrderCompleted_HasNoScopeAttribute_SoBrokerPathDoesNotThrow()
    {
        var evt = new IntegrationEvents.OrderCompleted(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            new List<IntegrationEvents.OrderCompletedItem>());
        var envelope = new Envelope(evt);

        Should.NotThrow(() => ScopeAuthorizationMiddleware.Before(envelope, new NullHttpContextAccessor()));
    }

    [Fact]
    public void ScopedMessage_WithoutHttpContext_ThrowsUnauthorized()
    {
        var envelope = new Envelope(new ScopedProbe());

        Should.Throw<UnauthorizedAccessException>(
            () => ScopeAuthorizationMiddleware.Before(envelope, new NullHttpContextAccessor()));
    }
}
