namespace ScoutAgent.Api.RateLimiting;

// 020-B: gün-başına kalıcı rate cap. Endpoint LLM çağrısından ÖNCE çağırır; cap aşılırsa false → 429.
// Tek-pod (in-proc) → load-modify-store yeter (dağıtık kilit YOK; scout replicas:1).
public sealed class DailyUsageGuard(IDocumentSession session) : IScopedDependency
{
    public async Task<bool> TryConsumeAsync(string partition, int dailyCap, CancellationToken ct)
    {
        var id = $"{partition}:{DateTime.UtcNow:yyyyMMdd}";
        var record = await session.LoadAsync<ScoutUsageDaily>(id, ct) ?? new ScoutUsageDaily { Id = id };
        if (record.Count >= dailyCap)
            return false;

        record.Count++;
        session.Store(record);
        await session.SaveChangesAsync(ct);
        return true;
    }
}
