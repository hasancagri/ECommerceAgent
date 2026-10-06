using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

namespace Common.Utils.Caching;

/// <summary>
/// BC-İÇİ durable boşaltma isteği: <see cref="CachingMessageBus"/> başarılı [InvalidatesCache]
/// command'i sonrası yayınlar, handler'ı Redis'i temizler. Durable LOCAL queue'da taşınır —
/// RabbitMQ'ya BİLEREK çıkmaz (tag adları BC-içi detay; pod-yayılımını backplane yapar;
/// broker bağımlılığı eklemek garantiyi artırmaz).
/// </summary>
public record CacheInvalidationRequested(string Tag);

/// <summary>
/// Boşaltmayı at-least-once koşar: RemoveByTag idempotent olduğundan tekrar güvenli.
/// Redis kesintisinde DLQ'ya düşürmek yerine 2 dk arayla durable yeniden dener (ScheduleRetry =
/// Postgres'te zamanlanmış zarf, restart'a dayanır). Merdiven tavanı ≈ en uzun TTL (20 dk):
/// daha uzun denemek anlamsız — bayat girdi TTL'de zaten ölür. Merdiven biterse mesaj global
/// policy ile error queue'ya düşer: operatör için "Redis uzun süre çöktü" olay kaydı, replay gereksiz.
/// Keşif: Common assembly'si taranmaz → her servis Discovery.IncludeType ile açık kaydeder.
/// </summary>
public class CacheInvalidationRequestedHandler
{
    public static void Configure(HandlerChain chain)
        => chain.OnException<Exception>()
            .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15))
            .Then.ScheduleRetry(Enumerable.Repeat(TimeSpan.FromMinutes(2), 10).ToArray());

    public Task Handle(CacheInvalidationRequested message, CacheInvalidator invalidator, CancellationToken ct)
        => invalidator.InvalidateAsync(message.Tag, ct);
}