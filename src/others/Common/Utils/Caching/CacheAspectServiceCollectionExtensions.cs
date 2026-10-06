using Common.Utils.Caching;

// ReSharper disable once CheckNamespace — DI uzantıları keşfedilebilir olsun diye MS namespace'inde.
namespace Microsoft.Extensions.DependencyInjection;

public static class CacheAspectServiceCollectionExtensions
{
    /// <summary>
    /// Declarative caching aspect'ini kaydeder: HybridCache (L1 in-memory + varsa L2 IDistributedCache)
    /// + IMessageBus'ı <see cref="CachingMessageBus"/> ile şeffaf sarar. <b>UseWolverine'den SONRA</b>
    /// çağrılmalı (IMessageBus kaydı önce gelsin). L2 (Redis) ayrıca kaydedilir (opsiyonel); yoksa
    /// HybridCache yalnız L1 ile çalışır.
    /// </summary>
    /// <param name="keyPrefix">Bounded-context anahtar öneki (ör. "catalog").</param>
    /// <param name="l1Expiration">L1 (yerel) TTL — cross-instance bayatlığı sınırlar; varsayılan 5sn.</param>
    public static IServiceCollection AddCachingAspect(this IServiceCollection services, string keyPrefix,
        TimeSpan? l1Expiration = null)
    {
        services.AddSingleton(new CacheAspectOptions
        {
            KeyPrefix = keyPrefix,
            L1Expiration = l1Expiration ?? TimeSpan.FromSeconds(5)
        });

        // L1 TTL'i decorator per-call AÇIKÇA uygular (CacheAspectOptions.L1Expiration) — global
        // DefaultEntryOptions per-call options verilince miras alınmıyor (tuzak, canlı doğrulama bulgusu).
        services.AddHybridCache();

        // Epoch sayacı + boşaltmanın tek kapısı + backplane dinleyicisi. Redis kayıtlı değilse
        // üçü de yerel/no-op moddadır.
        services.AddSingleton<CacheEpochs>();
        services.AddSingleton<CacheInvalidator>();
        services.AddHostedService<CacheBackplaneSubscriber>();

        services.Decorate<IMessageBus, CachingMessageBus>();
        return services;
    }
}