
namespace Common.Utils.Caching;

/// <summary>
/// IMessageBus'ı şeffaf saran declarative caching aspect'i (AOP). Endpoint'ler ve handler'lar
/// AYNEN kalır; davranış yalnız query/command tipindeki <see cref="CachedAttribute"/> /
/// <see cref="InvalidatesCacheAttribute"/> ile sürülür.
///
/// - [Cached] query: çağrı iki katmanlı (L1→L2→kaynak) HybridCache.GetOrCreateAsync ile sarılır;
///   stampede koruması native. Negatif sonuç (IsSuccess=false) önbeklenmez. Anahtara tag'in epoch'u
///   gömülür (…:e{N}) — boşaltma epoch artırımıdır (native tag KULLANILMAZ, bkz. CacheEpochs).
/// - [InvalidatesCache] command: inner çağrı (yani yazma + commit) tamamlandıktan SONRA, sonuç
///   başarılıysa <see cref="CacheInvalidationRequested"/> durable LOCAL queue'ya yayınlanır;
///   boşaltmayı handler'ı at-least-once koşar (retry + ScheduleRetry merdiveni). Senkron boşaltma
///   BİLEREK yok: Redis hatası commit edilmiş yazmanın sonucunu kirletmemeli.
///
/// Diğer tüm IMessageBus üyeleri değişmeden inner'a forward edilir.
/// </summary>
public sealed class CachingMessageBus(
    IMessageBus inner,
    HybridCache cache,
    CacheAspectOptions options,
    CacheEpochs epochs)
    : IMessageBus
{
    // ---- Cache/invalidation uygulanan tek nokta: InvokeAsync<T> ----

    public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => InvokeCoreAsync(message, token => inner.InvokeAsync<T>(message, token, timeout), cancellation);

    public Task<T> InvokeAsync<T>(object message, DeliveryOptions deliveryOptions,
        CancellationToken cancellation = default, TimeSpan? timeout = null)
        => InvokeCoreAsync(message, token => inner.InvokeAsync<T>(message, deliveryOptions, token, timeout), cancellation);

    private async Task<T> InvokeCoreAsync<T>(object message, Func<CancellationToken, Task<T>> innerCall,
        CancellationToken ct)
    {
        var messageType = message.GetType();
        var cached = messageType.GetCustomAttribute<CachedAttribute>();
        if (cached is not null)
            return await GetOrCreateAsync(message, cached, innerCall, ct);

        var result = await innerCall(ct);

        var invalidates = messageType.GetCustomAttribute<InvalidatesCacheAttribute>();
        // Boşaltma commit SONRASI: innerCall döndüyse Wolverine [Transactional] handler commit'i tamamdır.
        // Başarısız yazmada (IsSuccess=false) boşaltma yapılmaz.
        // Publish durable local queue'ya gider (envelope Postgres'e yazılır) — asıl boşaltmayı
        // CacheInvalidationRequestedHandler koşar; Redis çökükse retry/DLQ orada işler, çağıran görmez.
        if (invalidates is not null && result is not BaseResultModel { IsSuccess: false })
            await inner.PublishAsync(new CacheInvalidationRequested(invalidates.Tag));

        return result;
    }

    private async Task<T> GetOrCreateAsync<T>(object message, CachedAttribute cached,
        Func<CancellationToken, Task<T>> innerCall, CancellationToken ct)
    {
        var epoch = await epochs.GetAsync(cached.Tag);
        var key = $"{CacheKeyFactory.Build(options.KeyPrefix, message)}:e{epoch}";
        var ttl = TimeSpan.FromSeconds(cached.TtlSeconds);
        // TUZAK: per-call options verilince global DefaultEntryOptions.LocalCacheExpiration MİRAS
        // ALINMAZ — L1 de Expiration'a (ör. 20dk) uzar. L1 burada AÇIKÇA kısılır (canlı doğrulama bulgusu).
        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = ttl,
            LocalCacheExpiration = options.L1Expiration < ttl ? options.L1Expiration : ttl
        };

        try
        {
            return await cache.GetOrCreateAsync(
                key,
                async token =>
                {
                    var value = await innerCall(token);
                    // Negatif sonuç önbeklenmez: sentinel ile factory'yi hataya düşür → HybridCache yazmaz.
                    if (value is BaseResultModel { IsSuccess: false })
                        throw new NegativeResultException(value!);
                    return value;
                },
                entryOptions,
                cancellationToken: ct);
        }
        catch (NegativeResultException ex)
        {
            return (T)ex.Result;
        }
    }

    // Negatif (NotFound/başarısız) sonucu önbeklememek için factory'den fırlatılan iç sentinel.
    private sealed class NegativeResultException(object result) : Exception
    {
        public object Result { get; } = result;
    }

    // ---- Değişmeden inner'a forward edilen üyeler ----

    public string? TenantId
    {
        get => inner.TenantId;
        set => inner.TenantId = value!;
    }

    public Task InvokeAsync(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => inner.InvokeAsync(message, cancellation, timeout);

    public Task InvokeAsync(object message, DeliveryOptions deliveryOptions, CancellationToken cancellation = default,
        TimeSpan? timeout = null)
        => inner.InvokeAsync(message, deliveryOptions, cancellation, timeout);

    public IAsyncEnumerable<T> StreamAsync<T>(object message, CancellationToken cancellation = default)
        => inner.StreamAsync<T>(message, cancellation);

    public IAsyncEnumerable<T> StreamAsync<T>(object message, DeliveryOptions deliveryOptions,
        CancellationToken cancellation = default)
        => inner.StreamAsync<T>(message, deliveryOptions, cancellation);

    public Task InvokeForTenantAsync(string tenantId, object message, CancellationToken cancellation = default,
        TimeSpan? timeout = null)
        => inner.InvokeForTenantAsync(tenantId, message, cancellation, timeout);

    public Task<T> InvokeForTenantAsync<T>(string tenantId, object message, CancellationToken cancellation = default,
        TimeSpan? timeout = null)
        => inner.InvokeForTenantAsync<T>(tenantId, message, cancellation, timeout);

    public IDestinationEndpoint EndpointFor(string endpointName) => inner.EndpointFor(endpointName);

    public IDestinationEndpoint EndpointFor(Uri uri) => inner.EndpointFor(uri);

    public IReadOnlyList<Envelope> PreviewSubscriptions(object message) => inner.PreviewSubscriptions(message);

    public IReadOnlyList<Envelope> PreviewSubscriptions(object message, DeliveryOptions deliveryOptions)
        => inner.PreviewSubscriptions(message, deliveryOptions);

    public ValueTask SendAsync<T>(T message, DeliveryOptions? deliveryOptions = null)
        => inner.SendAsync(message, deliveryOptions);

    public ValueTask PublishAsync<T>(T message, DeliveryOptions? deliveryOptions = null)
        => inner.PublishAsync(message, deliveryOptions);

    public ValueTask BroadcastToTopicAsync(string topicName, object message, DeliveryOptions? deliveryOptions = null)
        => inner.BroadcastToTopicAsync(topicName, message, deliveryOptions);
}