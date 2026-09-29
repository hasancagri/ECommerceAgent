namespace Reviews.Moderation;

// worker'in tek isi — ReviewModerationRequested tuket, LLM ile denetle, ReviewModerated yayinla.
// Cascade return: donen mesaj Wolverine PublishMessage kurali ile exchange'e routelanir.
// Fail-open: LLM hatasi → ModerationException → retry → error queue (yorum Reviews'te Visible kalir).
public class ReviewModerationEventHandlers
{
    public async Task<IntegrationEvents.ReviewModerated> Handle(
        IntegrationEvents.ReviewModerationRequested msg,
        ModerationAgent agent,
        CancellationToken ct)
    {
        // Metinsiz istek normalde gelmez (Reviews yollamaz); savunma: temiz say.
        if (string.IsNullOrWhiteSpace(msg.Text))
            return new IntegrationEvents.ReviewModerated(msg.ReviewId, false, "none", string.Empty);

        ModerationAgent.ModerationOutput output;
        try
        {
            output = await agent.ModerateAsync(msg.Text, msg.Rating, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ModerationException("Moderasyon LLM cagrisi basarisiz", ex);
        }

        // Sema-disi karar savunmasi: ihlal iken gercek kategori zorunlu.
        if (output.Violation && (string.IsNullOrWhiteSpace(output.Category)
                || string.Equals(output.Category, "none", StringComparison.OrdinalIgnoreCase)))
            throw new ModerationException($"Sema disi moderasyon karari (category='{output.Category}')");

        return new IntegrationEvents.ReviewModerated(
            msg.ReviewId, output.Violation, output.Category, output.Reason);
    }
}
