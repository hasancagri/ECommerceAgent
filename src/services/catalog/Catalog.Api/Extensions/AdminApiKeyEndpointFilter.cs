namespace Catalog.Api.Extensions;

// 004 Dilim-1 İKİNCİ KAT (FR-014): gateway'e kör güven yok — Catalog admin uçları aynı anahtarı
// kendisi de doğrular. GEÇİCİ düzen: Dilim 2'de yerel kimlik JWT'si + [RequiredScope] bu filtrenin
// yerini alır. Anahtar değeri log/yanıt/ekrana yazılmaz (Anayasa İlke VIII); kıyas sabit-zamanlı.
public class AdminApiKeyOptions
{
    [Required(AllowEmptyStrings = false)]
    public string AdminApiKey { get; set; } = default!;
}

public class AdminApiKeyEndpointFilter(AdminApiKeyOptions options) : IEndpointFilter
{
    public const string HeaderName = "X-Admin-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(provided) || !SecretCompare.FixedTimeEquals(provided, options.AdminApiKey))
            return Results.Unauthorized();

        return await next(context);
    }
}
