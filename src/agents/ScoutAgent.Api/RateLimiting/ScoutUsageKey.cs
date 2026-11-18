namespace ScoutAgent.Api.RateLimiting;

// 020: rate-limit partition key'i — hem burst (A, AddRateLimiter) hem günlük (B, DailyUsageGuard) aynı
// key'i kullanır. Login → kullanıcı (sub) başına; anonim → IP başına; ikisi de yoksa "unknown" (tek kova).
public static class ScoutUsageKey
{
    public static string Partition(Guid? userId, string? ip) =>
        userId is { } id && id != Guid.Empty
            ? $"u:{id}"
            : $"ip:{(string.IsNullOrWhiteSpace(ip) ? "unknown" : ip)}";
}
