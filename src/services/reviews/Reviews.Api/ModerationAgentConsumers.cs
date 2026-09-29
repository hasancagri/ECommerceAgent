namespace Reviews.Api;

// Reviews.Moderation worker'inin karari (ReviewModerated) tuketilir. Review.ApplyModeration
// uygulanir; Visible→Hidden olduysa MUTLAK ozet yeniden hesaplanip ReviewSummaryChanged yayinlanir.
// Eski ModerateReview handler mantigi (LLM cagrisi haric) buraya tasindi.
[Transactional]
public class ModerationAgentConsumers
{
    public async Task Handle(
        IntegrationEvents.ReviewModerated evt,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken ct)
    {
        var review = await session.LoadAsync<Review>(evt.ReviewId, ct);
        if (review is null)
            return; // silinmis/bilinmeyen id: sessiz no-op

        if (review.ModeratedAtUtc is not null)
            return; // at-least-once: zaten denetlenmis (idempotent)

        // Sema-disi karar (normalde worker garanti eder): fail-open — temiz say, yorum Visible kalir.
        var verdictResult = ModerationVerdict.Create(evt.Violation, evt.Category, evt.Reason);
        var verdict = verdictResult.IsSuccess
            ? verdictResult.Data!
            : ModerationVerdict.Create(false, "none", string.Empty).Data!;

        var wasVisible = review.Status == ReviewStatus.Visible;
        var applied = review.ApplyModeration(verdict, DateTimeOffset.UtcNow);
        if (!applied.IsSuccess)
            return;

        session.Store(review);

        // Ihlalde ozet duser: MUTLAK yeni ozet yayinlanir (Count=0 ⇒ tuketici temizler).
        if (wasVisible && review.Status == ReviewStatus.Hidden)
        {
            var remaining = await session.Query<Review>()
                .Where(x => x.ProductId == review.ProductId
                            && x.Status == ReviewStatus.Visible
                            && x.Id != review.Id)
                .Select(x => x.Rating)
                .ToListAsync(ct);

            var count = remaining.Count;
            var average = count == 0 ? 0m : Math.Round((decimal)remaining.Average(), 2);
            await bus.PublishAsync(new IntegrationEvents.ReviewSummaryChanged(
                review.ProductId, average, count));
        }
    }
}
