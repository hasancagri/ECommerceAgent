namespace ScoutAgent.Api.RateLimiting;

// 020-B: gün-başına kalıcı kullanım sayacı (Marten scoutDb). Id = "{partition}:{yyyyMMdd}" (UTC) —
// gün rollover Id'deki tarihten (yeni gün = yeni doc, sayaç sıfırdan). Burst (A) in-memory; bu kalıcı.
public sealed class ScoutUsageDaily
{
    public string Id { get; set; } = default!;
    public int Count { get; set; }
}
