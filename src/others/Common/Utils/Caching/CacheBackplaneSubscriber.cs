using Microsoft.Extensions.Hosting;

namespace Common.Utils.Caching;

/// <summary>
/// Backplane'in dinleme yarısı: kendi BC kanalına (cache-inv:{prefix}) gelen "tag|epoch" mesajında
/// lokal epoch map'ini günceller — sonraki okumalar yeni anahtara düşer, eski L1/L2 girdileri
/// erişilmez kalır. Yayıncı instance mesajı kendine de alır — Apply(Max) idempotent, zararsız.
/// Redis kayıtlı değilse hiç çalışmaz (tek-instance / lokal-epoch mod).
/// </summary>
public sealed class CacheBackplaneSubscriber(
    CacheEpochs epochs,
    CacheAspectOptions options,
    IConnectionMultiplexer? redis = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (redis is null) return;

        await redis.GetSubscriber()
            .SubscribeAsync(
                RedisChannel.Literal(CacheInvalidator.ChannelFor(options.KeyPrefix)),
                (_, message) =>
                {
                    var parts = message.ToString().Split('|');
                    if (parts.Length == 2 && long.TryParse(parts[1], out var epoch))
                        epochs.Apply(parts[0], epoch);
                });

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
