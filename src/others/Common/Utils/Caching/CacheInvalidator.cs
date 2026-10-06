namespace Common.Utils.Caching;

/// <summary>
/// Tag boşaltmanın TEK kapısı: epoch'u artırır (<see cref="CacheEpochs"/>) ve yeni epoch'u Redis
/// backplane kanalına yayınlar ki diğer instance'lar lokal epoch map'ini güncellesin. Girdi silme
/// YOK (epoch deseni — eski anahtarlar erişilmez kalır, TTL temizler); HybridCache.RemoveByTagAsync
/// bilinçli KULLANILMAZ (dotnet/extensions#7771). Pub/sub at-most-once'tır — kopuk abone mesajı
/// kaçırabilir; kısa L1 TTL + ilk-kullanımda Redis'ten epoch yükleme güvenlik ağıdır.
/// Decorator dışı elle boşaltmalar (event handler/process) da BUNU kullanmalı.
/// </summary>
public sealed class CacheInvalidator(
    CacheEpochs epochs,
    CacheAspectOptions options,
    IConnectionMultiplexer? redis = null)
{
    public static string ChannelFor(string keyPrefix) => $"cache-inv:{keyPrefix}";

    public async Task InvalidateAsync(string tag, CancellationToken ct = default)
    {
        var epoch = await epochs.BumpAsync(tag);

        if (redis is not null)
            await redis.GetSubscriber()
                .PublishAsync(RedisChannel.Literal(ChannelFor(options.KeyPrefix)), $"{tag}|{epoch}");
    }
}
