namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Commands;

// US3/FR-005: credential-giriş ekranına süreli + TEK KULLANIMLIK link üretir. Link tabanı
// config'ten (CredentialEntryOptions.PublicBaseUrl) — HttpContext base KULLANILMAZ: MCP çağrısı
// platform MCP fasad proxy'sinden gelir, istek base'i Aspire iç adresidir, tarayıcıda çözülmez (D1).
// İz: credential_entry_link_created — token DEĞİL oturum Id loglanır.
public static class AdminRequestCredentialEntryLink
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminRequestCredentialEntryLinkCommand(Guid UserId);

    public class AdminRequestCredentialEntryLinkResponse
    {
        public string Url { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminRequestCredentialEntryLinkCommandHandler
    {
        public Task<FeatureObjectResultModel<AdminRequestCredentialEntryLinkResponse>> Handle(
            AdminRequestCredentialEntryLinkCommand cmd,
            IDocumentSession session,
            Options.CredentialEntryOptions options,
            CancellationToken ct)
        {
            var created = CredentialEntrySession.Create(cmd.UserId, options.LinkLifetime);
            if (!created.IsSuccess)
                return Task.FromResult(
                    FeatureObjectResultModel<AdminRequestCredentialEntryLinkResponse>.Error(created.Messages));

            var entrySession = created.Data!;
            session.Store(entrySession);

            return Task.FromResult(FeatureObjectResultModel<AdminRequestCredentialEntryLinkResponse>.Ok(
                new AdminRequestCredentialEntryLinkResponse
                {
                    Url = $"{options.PublicBaseUrl.TrimEnd('/')}/merchant-credentials/{entrySession.Token}",
                    ExpiresAt = entrySession.ExpiresAt,
                    Message = "Linki tarayicida acin; MerchantId + MerchantKey ekrana elle girilir. " +
                              "Link tek kullanimlik ve surelidir."
                }));
        }
    }
}

[McpServerToolType]
public static class AdminRequestCredentialEntryLinkMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.RequestCredentialEntryLink)]
    [Description(Shared.McpToolDescriptions.CustomerAdminTools.RequestCredentialEntryLink)]
    public static Task<FeatureObjectResultModel<AdminRequestCredentialEntryLink.AdminRequestCredentialEntryLinkResponse>> AdminRequestCredentialEntryLinkAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminRequestCredentialEntryLink.AdminRequestCredentialEntryLinkResponse>>(
            new AdminRequestCredentialEntryLink.AdminRequestCredentialEntryLinkCommand(userId), ct);
    }
}