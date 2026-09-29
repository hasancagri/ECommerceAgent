namespace Customer.Api.Domains.MerchantInformations.Features.Commands;

// US3/FR-006+FR-013: hosted credential-giriş ekranının POST'u. Token = yetki (İlke V v1.11.1
// capability-link istisnası; scope zorlaması link ÜRETİMİNDE yapıldı). Akış: token'lı oturumu yükle →
// ikiliyi PG'ye anında doğrula (geçersiz=RET, oturum yaşar — admin aynı ekranda düzeltir; PG-yok=
// CredentialsVerified:false ile kaydet) → başarıda oturumu TÜKET → MerchantInformation upsert →
// iz (merchant_credentials_submitted; MerchantKey ASLA yazılmaz — FR-007).
public static class SubmitMerchantCredentials
{
    public record SubmitMerchantCredentialsCommand(string Token, string MerchantId, string MerchantKey);

    public class SubmitMerchantCredentialsResponse
    {
        public bool Verified { get; set; }
        public Guid MerchantId { get; set; }
    }

    [Transactional]
    public class SubmitMerchantCredentialsCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitMerchantCredentialsResponse>> Handle(
            SubmitMerchantCredentialsCommand cmd,
            IDocumentSession session,
            Onboarding.PgOnboardingClient gateway,
            CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var entrySession = await session.Query<CredentialEntrySession>()
                .FirstOrDefaultAsync(x => x.Token == cmd.Token, ct);

            // Bilinmeyen/tüketilmiş/süresi geçmiş token → nötr NotFound (token doğruluğu sızdırılmaz).
            if (entrySession is null || !entrySession.IsUsable(now))
                return FeatureObjectResultModel<SubmitMerchantCredentialsResponse>.NotFound();

            var messages = new List<MessageItem>();
            if (!Guid.TryParse(cmd.MerchantId?.Trim(), out var merchantId) || merchantId == Guid.Empty)
                messages.Add(new MessageItem { Property = "MerchantId", Code = CustomerResourceConstants.INVALID_VALUE });
            if (string.IsNullOrWhiteSpace(cmd.MerchantKey))
                messages.Add(new MessageItem { Property = "MerchantKey", Code = CustomerResourceConstants.VALUE_IS_REQUIRED });
            if (messages.Count > 0)
                return FeatureObjectResultModel<SubmitMerchantCredentialsResponse>.Error(messages);

            var merchantKey = cmd.MerchantKey.Trim();

            // FR-013: kayıt anında PG doğrulaması. false=geçersiz ikili; null=PG erişilemedi.
            var valid = await gateway.ValidateCredentialsAsync(merchantId, merchantKey, ct);
            if (valid is false)
                return FeatureObjectResultModel<SubmitMerchantCredentialsResponse>.Error(new MessageItem
                { Code = CustomerResourceConstants.MERCHANT_CREDENTIALS_INVALID });

            var verified = valid is true;

            // Tek kullanım: yalnız BAŞARILI kayıt tüketir (yanlış ikili aynı ekranda düzeltilebilir).
            var consumed = entrySession.Consume(now);
            if (!consumed.IsSuccess)
                return FeatureObjectResultModel<SubmitMerchantCredentialsResponse>.NotFound();
            session.Store(entrySession);

            var existing = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (existing is not null && existing.MerchantId == merchantId)
            {
                var updated = existing.UpdateKey(merchantKey, verified);
                if (!updated.IsSuccess)
                    return FeatureObjectResultModel<SubmitMerchantCredentialsResponse>.Error(updated.Messages);
                session.Update(existing);
            }
            else
            {
                var created = MerchantInformation.Create(merchantId, merchantKey, verified);
                if (!created.IsSuccess)
                    return FeatureObjectResultModel<SubmitMerchantCredentialsResponse>.Error(created.Messages);
                if (existing is not null)
                    session.Delete(existing);
                session.Store(created.Data!);
            }

            return FeatureObjectResultModel<SubmitMerchantCredentialsResponse>.Ok(
                new SubmitMerchantCredentialsResponse { Verified = verified, MerchantId = merchantId });
        }
    }
}