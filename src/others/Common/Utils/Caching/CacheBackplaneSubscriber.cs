using Microsoft.Extensions.Hosting;

namespace Common.Utils.Caching;

/// <summary>
/// Backplane'in dinleme yarısı: kendi BC kanalına (cache-inv:{prefix}) gelen tag mesajında yerel
/// L1 + L2'yi boşaltır. Yayıncı instance mesajı kendine de alır — ikinci temizlik idempotent,
/// zararsız. Redis kayıtlı değilse hiç çalışmaz (tek-instance / L1-only mod).
/// </summary>
public sealed class CacheBackplaneSubscriber(
    HybridCache cache,
    CacheAspectOptions options,
    IConnectionMultiplexer? redis = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (redis is null) return;

        await redis.GetSubscriber()
            .SubscribeAsync(
                RedisChannel.Literal(CacheInvalidator.ChannelFor(options.KeyPrefix)),
                (channel, message) => { _ = cache.RemoveByTagAsync(message.ToString(), stoppingToken).AsTask(); });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // normal kapanış
        }
    }
}