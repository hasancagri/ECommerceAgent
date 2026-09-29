namespace Discount.Api.Process;

// US2: kampanya süre yönetimi — per-kampanya Wolverine scheduled message (BC'nin KENDİ dayanıklı
// süreci → Process/). start-fire aktifleştirir (resolve + apply-skip + push), end-fire kampanyanın
// kitaplarını temizler. GUARD'LI İDEMPOTENT: aggregate güncel duruma bakar — bayat/iptal mesaj no-op;
// restart'ta kaçan fire durable telafi edilir. Wolverine keşfi bu tipi tarasa da (Handler son-eki),
// Program.cs IncludeType ile de garanti kayıtlıdır.

// Scheduled mesajlar (in-proc durable local queue) — kampanya sayısıyla ölçeklenir (1 kampanya ≤2 mesaj).
public record CampaignActivated(Guid CampaignId);
public record CampaignEnded(Guid CampaignId);

public class CampaignScheduleHandler
{
    [Transactional]
    public async Task Handle(
        CampaignActivated msg, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var campaign = await session.LoadAsync<Campaign>(msg.CampaignId, ct);
        if (campaign is null)
            return;

        // Guard: iptal / pencere dışı (bayat fire) → no-op. Yalnız gerçekten aktif pencerede uygula.
        if (!campaign.IsEffectiveAt(DateTime.UtcNow))
            return;

        campaign.MarkActive();
        session.Store(campaign);
        await CampaignApplication.ActivateAsync(campaign, session, bus, ct);
    }

    [Transactional]
    public async Task Handle(
        CampaignEnded msg, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        var campaign = await session.LoadAsync<Campaign>(msg.CampaignId, ct);
        if (campaign is null)
            return;

        // İptal edilmişse zaten temizlenmiştir (ClearAsync no-op); değilse Ended işaretle + temizle.
        campaign.MarkEnded();
        session.Store(campaign);
        await CampaignApplication.ClearAsync(campaign.Id, session, bus, ct);
    }
}
