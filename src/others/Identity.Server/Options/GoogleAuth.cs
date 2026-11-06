namespace Identity.Server.Options;

// 006: Google external login ayarı ("Authentication:Google" section'ı; ClientId/Secret user-secrets'tan —
// İlke VIII: değer config dosyasına/git'e/log'a girmez). Secret yoksa şema kaydedilmez ve login
// sayfasındaki buton render edilmez (zarif eksilme) — sistem Google'sız tam çalışır.
public sealed class GoogleAuth
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
