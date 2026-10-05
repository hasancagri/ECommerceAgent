namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Commands;

// 087 US1/FR-004: merchant kaybettiği/sızdığından şüphelendiği MerchantKey yerine taze key alır. Store
// kayıtlı MerchantId ile PG'ye reissue tetikler (correlationId + callbackUrl ile); PG eski key'i anında
// öldürür + YENİ key'i aynı HMAC-callback yoluyla (ReceiveMerchantCredentials) store'a teslim eder.
// EKRANSIZ — reveal URL / elle-giriş adımı YOK. Yanıt sır-free: yalnız durum (key sohbete/dönüşe girmez).
public static class AdminReissueMerchantKey
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminReissueMerchantKeyCommand(Guid UserId, string? Reason);

    public class AdminReissueMerchantKeyResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminReissueMerchantKeyCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminReissueMerchantKeyResponse>> Handle(
            AdminReissueMerchantKeyCommand cmd,
            IDocumentSession session,
            Onboarding.PgOnboardingClient gateway,
            CancellationToken ct)
        {
            // Store tek-merchant: kayıtlı MerchantInformation'dan MerchantId alınır (key kayıp, Id değil).
            var info = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (info is null)
                return FeatureObjectResultModel<AdminReissueMerchantKeyResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.RECORD_NOT_FOUND });

            if (!gateway.IsConfigured)
                return FeatureObjectResultModel<AdminReissueMerchantKeyResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });

            // Yeni key için yeni kayıt çevrimi (callback onu Active'e çeker).
            var correlationId = Guid.NewGuid();
            var started = info.StartRegistration(correlationId);
            if (!started.IsSuccess)
                return FeatureObjectResultModel<AdminReissueMerchantKeyResponse>.Error(started.Messages);

            // 087 reissue YARIŞ FIX'i: yeni pending-correlation'ı PG'yi çağırmadan ÖNCE durable commit et.
            // PG reissue callback'i ANINDA döner; bu commit olmadan callback eski correlation'ı görüp nötr
            // reddeder (yeni key düşmez). StartRegistration overwrite-safe → PG başarısızsa kilitlenme yok.
            session.Store(info);
            await session.SaveChangesAsync(ct);

            var accepted = await gateway.ReissueAsync(info.MerchantId, cmd.Reason?.Trim(), correlationId, ct);
            if (accepted is null)
                return FeatureObjectResultModel<AdminReissueMerchantKeyResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });

            return FeatureObjectResultModel<AdminReissueMerchantKeyResponse>.Ok(new AdminReissueMerchantKeyResponse
            {
                Message = "Key yenileme baslatildi; eski key gecersiz. Yeni key store'a guvenli sekilde " +
                          "otomatik gelir (elle giris yok). Durumu admin_get_merchant_status ile dogrulayin."
            });
        }
    }
}

[McpServerToolType]
public static class AdminReissueMerchantKeyMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.ReissueMerchantKey)]
    [Description(Shared.McpToolDescriptions.CustomerAdminTools.ReissueMerchantKey)]
    public static Task<FeatureObjectResultModel<AdminReissueMerchantKey.AdminReissueMerchantKeyResponse>> AdminReissueMerchantKeyAsync(
        [Description("Yenileme nedeni (opsiyonel): ornegin 'unuttum' veya 'sizinti-suphesi'")] string? reason,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminReissueMerchantKey.AdminReissueMerchantKeyResponse>>(
            new AdminReissueMerchantKey.AdminReissueMerchantKeyCommand(userId, reason), ct);
    }
}