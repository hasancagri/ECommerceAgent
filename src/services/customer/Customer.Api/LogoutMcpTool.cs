using System.Net.Http.Headers;

namespace Customer.Api;

// 061 logout: kullanıcı chat'ten "çıkış yap" dediğinde bu agent'ın (client) mağaza erişim yetkisini
// iptal eder. Kullanıcının Bearer'ını Identity.Server agent-logout ucuna forward eder (domain işi değil,
// auth proxy — bu yüzden Features/Agents slice YOK). 4 korumalı serviste birebir (bilinçli tekrar).
[McpServerToolType]
public static class LogoutMcpTool
{
    public class LogoutResponse
    {
        public string Message { get; set; } = default!;
    }

    [McpServerTool(Name = Shared.AuthTools.Logout)]
    [Description(Shared.McpToolDescriptions.AuthTools.Logout)]
    public static async Task<FeatureObjectResultModel<LogoutResponse>> LogoutAsync(
        IHttpContextAccessor http,
        IHttpClientFactory factory,
        CancellationToken ct)
    {
        var auth = http.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return FeatureObjectResultModel<LogoutResponse>.Ok(new LogoutResponse
            {
                Message = "Cikis yapilamadi: aktif bir oturum bulunamadi."
            });

        var client = factory.CreateClient(AgentLogoutClientExtension.HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, "connect/agent-logout");
        req.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);
        var res = await client.SendAsync(req, ct);

        return FeatureObjectResultModel<LogoutResponse>.Ok(new LogoutResponse
        {
            Message = res.IsSuccessStatusCode
                ? "Cikis yapildi. Magaza erisim yetkin iptal edildi; tekrar islem yapmak istersen yeniden " +
                  "baglanip onay vermen gerekir."
                : "Cikis su an tamamlanamadi, lutfen biraz sonra tekrar dene."
        });
    }
}