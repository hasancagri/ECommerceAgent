namespace Customer.Api.Domains.MerchantInformations.Features.Commands;

// 087 US1/FR-004: PG→store credential callback'inin yazma slice'ı (kontrat credential-callback.md).
// Dış-webhook'un tetiklediği ama BC'nin KENDİ credential-persist niyeti → Features/Commands (süreç-glue
// değil). HMAC imza endpoint'te doğrulanmış gelir; burada correlation eşle → ApplyCredentialsFromCallback
// → persist. İdempotent (çift-callback no-op). MerchantKey ASLA log/trace/dönüşe yazılmaz (FR-006).
public static class ReceiveMerchantCredentials
{
    public record ReceiveMerchantCredentialsCommand(Guid CorrelationId, Guid MerchantId, string MerchantKey);

    // Sır-free yanıt: yalnız kabul bayrağı — MerchantId/Key DÖNMEZ.
    public class ReceiveMerchantCredentialsResponse
    {
        public bool Applied { get; set; }
    }

    [Transactional]
    public class ReceiveMerchantCredentialsCommandHandler
    {
        public async Task<FeatureObjectResultModel<ReceiveMerchantCredentialsResponse>> Handle(
            ReceiveMerchantCredentialsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Store tek-merchant: aktif kayıt çevrimi olmadan callback gelirse (kayıt hiç başlamadıysa)
            // nötr red — persist yok, token/correlation doğruluğu sızdırılmaz.
            var info = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (info is null)
                return FeatureObjectResultModel<ReceiveMerchantCredentialsResponse>.Error(new MessageItem
                { Property = "CorrelationId", Code = CustomerResourceConstants.INVALID_OPERATION_ERROR });

            // Correlation eşleşmezse nötr red; eşleşir + Pending → set Active; zaten Active + aynı → no-op Ok.
            var applied = info.ApplyCredentialsFromCallback(cmd.CorrelationId, cmd.MerchantId, cmd.MerchantKey);
            if (!applied.IsSuccess)
                return FeatureObjectResultModel<ReceiveMerchantCredentialsResponse>.Error(applied.Messages);

            session.Store(info);
            return FeatureObjectResultModel<ReceiveMerchantCredentialsResponse>.Ok(
                new ReceiveMerchantCredentialsResponse { Applied = true });
        }
    }
}