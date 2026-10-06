namespace Common.Utils.Caching;

/// <summary>
/// Caching aspect'inin servise-özel ayarları. KeyPrefix, anahtarları bounded-context düzeyinde
/// ayırır (ör. "catalog"); aynı Redis örneğinde farklı servislerin anahtarları çakışmaz.
/// </summary>
public sealed class CacheAspectOptions
{
    public required string KeyPrefix { get; init; }

    /// <summary>L1 (yerel) TTL — cross-instance bayatlık tavanı. Decorator her girdiye AÇIKÇA uygular
    /// (per-call HybridCacheEntryOptions global default'u miras almaz — tuzak).</summary>
    public TimeSpan L1Expiration { get; init; } = TimeSpan.FromSeconds(5);
}