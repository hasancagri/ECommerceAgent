namespace NotificationAgent;

// akisin sahibi — PriceAlarmTriggered tuket: email bos → gonderimsiz iz; degilse MailAgent
// maili yazip gonderir (her LLM/MCP/SMTP hatasi NotificationException → retry 10s/30s/60s → error queue).
// Cascade return: donen NotificationSent, Wolverine PublishMessage kurali ile exchange'e routelanir;
// exception yolunda cascade HIC yayinlanmaz (error-queue mesaji izsizdir — kabul, FR-008).
public class PriceAlarmEventHandlers
{
    public async Task<IntegrationEvents.NotificationSent> Handle(
        IntegrationEvents.PriceAlarmTriggered evt,
        MailAgent mailAgent,
        CancellationToken ct)
    {
        // E-posta bos → gonderim atlanir, iz "no-email" ile DUSER (R9).
        if (string.IsNullOrWhiteSpace(evt.Email))
            return new IntegrationEvents.NotificationSent(
                evt.UserId, evt.ProductId, evt.Email, Success: false, Detail: NotificationDetails.NoEmail);

        // WebApp (UI) söküldü → mail'de ürün linki yok (agent-only; ürün sayfası kalmadı).
        await mailAgent.SendPriceAlarmMailAsync(evt, ct);

        return new IntegrationEvents.NotificationSent(
            evt.UserId, evt.ProductId, evt.Email, Success: true, Detail: NotificationDetails.Sent);
    }
}