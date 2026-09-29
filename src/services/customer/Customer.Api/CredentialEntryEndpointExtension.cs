using System.Collections.Concurrent;
using System.Reflection;
using Customer.Api.Domains.MerchantInformations.Features.Commands;

namespace Customer.Api;

// 078 D1/US3: hosted credential-giriş ekranı — tek sayfalık gömülü HTML (Razor/SPA yok). ANONİM:
// token = yetki (İlke V v1.11.1 capability-link istisnası; scope zorlaması link üretiminde).
// Bilinmeyen/tüketilmiş/süresi geçmiş token her iki uçta da NÖTR 404 (token doğruluğu sızdırılmaz).
// Ekran yazma-only: mevcut MerchantId/Key ASLA gösterilmez (FR-005).
// HTML/CSS gövdesi Pages/CredentialEntry/*.html gömülü resource'larında; C# yalnız okur (bir kez
// cache'ler) + {{placeholder}} doldurur + veri değerlerini HTML-metakarakter kaçışıyla encode eder + servis eder.
public static class CredentialEntryEndpointExtension
{
    private const string PageTitle = "Merchant Credential Girişi";

    // Veri değerlerini (token/error/title) fragment'a basmadan önce kaçış. HtmlEncoder.Default non-ASCII'yi
    // (Türkçe harfler) VE '+' gibi ASCII'yi numeric entity'ye çevirip render'ı BYTE düzeyinde bozardı; bu
    // refactor'ın mutlak kısıtı çıktı-eşitliği. UnicodeRanges.All Türkçe'yi korur ama '+' yine kaçardı →
    // gerçek XSS metakarakterlerini (& < > " ') kaçıran, kalan her baytı OLDUĞU GİBİ bırakan dar kaçış:
    // hem güvenli hem çıktı-koruyucu (token base64url ve error sunucu-sabitleri zaten metakarakter içermez).
    private static string Encode(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");

    public static void MapCredentialEntryEndpoints(this WebApplication app)
    {
        // GET: form. Oturumu TÜKETMEZ (form açıp vazgeçmek linki öldürmez, süre öldürür — D2).
        app.MapGet("/merchant-credentials/{token}", async (
            string token, IQuerySession session, CancellationToken ct) =>
        {
            var entry = await session.Query<Domains.MerchantInformations.CredentialEntrySession>()
                .FirstOrDefaultAsync(x => x.Token == token, ct);

            return entry is not null && entry.IsUsable(DateTimeOffset.UtcNow)
                ? Html(FormPage(token, error: null), StatusCodes.Status200OK)
                : Html(NotFoundPage(), StatusCodes.Status404NotFound);
        });

        // POST: doğrula + kaydet. Başarı token'ı tüketir; geçersiz ikili oturumu YAŞATIR (düzeltilebilir).
        app.MapPost("/merchant-credentials/{token}", async (
            string token, HttpRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<SubmitMerchantCredentials.SubmitMerchantCredentialsResponse>>(
                new SubmitMerchantCredentials.SubmitMerchantCredentialsCommand(
                    token,
                    form["merchantId"].ToString(),
                    form["merchantKey"].ToString()), ct);

            if (result.IsSuccess)
                return Html(SuccessPage(result.Data!.Verified), StatusCodes.Status200OK);

            var messages = result.Messages ?? [];
            if (messages.Any(m => m.Code == CustomerResourceConstants.RECORD_NOT_FOUND))
                return Html(NotFoundPage(), StatusCodes.Status404NotFound);

            var error = messages.Any(m => m.Code == CustomerResourceConstants.MERCHANT_CREDENTIALS_INVALID)
                ? "Girilen MerchantId + MerchantKey ikilisi ödeme sağlayıcısında doğrulanamadı. Değerleri kontrol edip yeniden deneyin."
                : "Alanları kontrol edin: MerchantId geçerli bir GUID, MerchantKey boş olmayan bir değer olmalı.";
            return Html(FormPage(token, error), StatusCodes.Status400BadRequest);
        });
    }

    private static IResult Html(string html, int statusCode) =>
        Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);

    // Gömülü resource'u bir kez oku + cache'le. Ad = "<default-namespace>.<klasör noktalı>.<dosya>".
    private static readonly Assembly ResourceAssembly = typeof(CredentialEntryEndpointExtension).Assembly;
    private static readonly ConcurrentDictionary<string, string> Templates = new();

    private static string Template(string name) => Templates.GetOrAdd(name, static key =>
    {
        var resourceName = $"Customer.Api.Pages.CredentialEntry.{key}";
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Gömülü resource bulunamadı: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    // Layout raw (fragment o HTML olarak, encode EDİLMEZ); title = sunucu-sabiti, encode çıktıyı korur.
    private static string Layout(string body) => Template("layout.html")
        .Replace("{{title}}", Encode(PageTitle))
        .Replace("{{body}}", body);

    // Veri değerleri (token, error) fragment'a encode edilerek basılır; fragment layout'a RAW gider ({{body}}).
    private static string FormPage(string token, string? error) => Layout(Template("form.html")
        .Replace("{{token}}", Encode(token))
        .Replace("{{error}}", error is null ? "" : $"""<div class="error">{Encode(error)}</div>"""));

    private static string SuccessPage(bool verified) =>
        Layout(Template(verified ? "success-verified.html" : "success-unverified.html"));

    // Kendi doctype'lı bağımsız sayfa (Layout kullanmaz).
    private static string NotFoundPage() => Template("notfound.html");
}