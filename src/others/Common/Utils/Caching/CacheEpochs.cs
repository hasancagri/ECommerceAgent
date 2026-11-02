using System.Collections.Concurrent;

namespace Common.Utils.Caching;

/// <summary>
/// Tag-başına jenerasyon (epoch) sayacı — boşaltmanın mekanizması. Cache anahtarına epoch gömülür
/// (…:e{N}); boşaltma = sayacı artırmak. Eski girdiler SİLİNMEZ, erişilmez kalıp TTL'de ölür.
/// HybridCache'in native tag'leri (RemoveByTagAsync) KULLANILMAZ: 10.0.0'da tag zaman damgası
/// proses-içinde bayat çözülüp kalıcı stale üretir (dotnet/extensions#7771 — canlı doğrulamada
/// birebir gözlendi). Gerçek sayaç Redis'te (INCR, atomik + instance'lar arası ortak); Redis yoksa
/// yalnız lokal (tek-instance mod). Lokal map süreç-ömürlü cache'tir; diğer instance'ların bump'ı
/// backplane'den <see cref="Apply"/> ile düşer.
/// </summary>
public sealed class CacheEpochs(CacheAspectOptions options, IConnectionMultiplexer? redis = null)
{
    private readonly ConcurrentDictionary<string, long> _epochs = new();

    public static string RedisKeyFor(string keyPrefix, string tag) => $"cache-epoch:{keyPrefix}:{tag}";

    /// <summary>Okuma yolu: lokal map → yoksa Redis'ten yükle (ilk kullanım, tag-başına 1 kez) → 0.</summary>
    public async ValueTask<long> GetAsync(string tag)
    {
        if (_epochs.TryGetValue(tag, out var cached))
            return cached;

        long value = 0;
        if (redis is not null)
        {
            var raw = await redis.GetDatabase().StringGetAsync(RedisKeyFor(options.KeyPrefix, tag));
            if (raw.TryParse(out long parsed))
                value = parsed;
        }

        // Yarış: await sırasında backplane daha YENİ epoch yazdıysa onu ez-ME (GetOrAdd mevcut değeri korur).
        return _epochs.GetOrAdd(tag, value);
    }

    /// <summary>Boşaltma: sayaç artır (Redis INCR = atomik; yoksa lokal). Yeni epoch'u döner.</summary>
    public async ValueTask<long> BumpAsync(string tag)
    {
        long next;
        if (redis is not null)
            next = await redis.GetDatabase().StringIncrementAsync(RedisKeyFor(options.KeyPrefix, tag));
        else
            next = _epochs.AddOrUpdate(tag, 1, (_, current) => current + 1);

        _epochs[tag] = next;
        return next;
    }

    /// <summary>Backplane'den gelen bump'ı uygular (asla geri sarmaz — Max).</summary>
    public void Apply(string tag, long epoch)
        => _epochs.AddOrUpdate(tag, epoch, (_, current) => Math.Max(current, epoch));
}
