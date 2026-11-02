
namespace Common.Utils.Authorization;

public static class ScopeAuthorizationMiddleware
{
    public static void Before(Envelope envelope, IHttpContextAccessor http)
    {
        var scope = envelope.Message?.GetType()
            .GetCustomAttribute<RequiredScopeAttribute>()?.Scope;
        
        if (scope is null)
            return;

        if (http.HttpContext?.User.HasClaim("scope", scope) != true)
            throw new UnauthorizedAccessException($"Required scope missing: {scope}");
    }
}