namespace Storefront.Api.AgentSql;

// R6: sorgu izi — aggregate DEĞİL (davranışsız iz dokümanı; read-model/iz istisnası).
// Ret DAHİL her çağrıda bir satır; sahip bağlantıyla yazılır (kısıtlı rol buraya yazamaz — yapısal).
public class AgentQueryLog
{
    private AgentQueryLog()
    {
    }

    public Guid Id { get; private set; }

    // Ham metin, {{EMBED}} İKAMESİZ — asistanın yazdığı hal (vektör log'a sızmaz, R3).
    public string Sql { get; private set; } = null!;

    public AgentQueryVerdict Verdict { get; private set; }

    // StorefrontResourceConstants.AgentSql* sabiti (Rejected/Failed'da dolu).
    public string? RejectCode { get; private set; }

    // Tam Postgres/SQLSTATE metni (yalnız iz; asistana budanmış döner, R5).
    public string? ErrorDetail { get; private set; }

    public int RowCount { get; private set; }
    public bool Truncated { get; private set; }
    public int DurationMs { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AgentQueryLog Executed(string sql, int rowCount, bool truncated, int durationMs) => new()
    {
        Id = Guid.NewGuid(),
        Sql = sql,
        Verdict = AgentQueryVerdict.Executed,
        RowCount = rowCount,
        Truncated = truncated,
        DurationMs = durationMs,
        CreatedAt = DateTimeOffset.UtcNow
    };

    public static AgentQueryLog Rejected(string sql, string rejectCode, string? errorDetail, int durationMs) => new()
    {
        Id = Guid.NewGuid(),
        Sql = sql,
        Verdict = AgentQueryVerdict.Rejected,
        RejectCode = rejectCode,
        ErrorDetail = errorDetail,
        DurationMs = durationMs,
        CreatedAt = DateTimeOffset.UtcNow
    };

    public static AgentQueryLog Failed(string sql, string rejectCode, string? errorDetail, int durationMs) => new()
    {
        Id = Guid.NewGuid(),
        Sql = sql,
        Verdict = AgentQueryVerdict.Failed,
        RejectCode = rejectCode,
        ErrorDetail = errorDetail,
        DurationMs = durationMs,
        CreatedAt = DateTimeOffset.UtcNow
    };
}

// Rejected = bekçi/rol reddi (çalışmadan); Failed = DB hatası/timeout (çalışırken).
public enum AgentQueryVerdict
{
    Executed,
    Rejected,
    Failed
}
