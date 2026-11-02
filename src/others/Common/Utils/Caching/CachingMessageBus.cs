namespace Common.Utils.Caching;

/// <summary>
/// IMessageBus'ı şeffaf saran declarative caching aspect'i (AOP) — yalnız OKUMA yolu. Endpoint'ler
/// ve handler'lar AYNEN kalır; davranış yalnız query tipindeki <see cref="CachedAttribute"/> ile sürülür.
///
/// - [Cached] query: çağrı iki katmanlı (L1→L2→kaynak) HybridCache.GetOrCreateAsync ile sarılır;
///   stampede koruması native. Negatif sonuç (IsSuccess=false) önbeklenmez. Anahtara tag'in epoch'u
///   gömülür (…:e{N}) — boşaltma epoch artırımıdır (native tag KULLANILMAZ, bkz. CacheEpochs).
/// - Boşaltma bu sınıfın işi DEĞİL (090): yazma handler'ı <see cref="CacheInvalidationRequested"/>'ı
///   KENDİ içinden yayınlar — zarf [Transactional] commit'iyle AYNI outbox'ta kalıcılaşır (atomik;
///   decorator'ın eski commit-SONRASI publish'indeki kayıp penceresi yok).
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
    // ---- Cache uygulanan tek nokta: InvokeAsync<T> ----

    public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => InvokeCoreAsync(message, token => inner.InvokeAsync<T>(message, token, timeout), cancellation);

    public Task<T> InvokeAsync<T>(object message, DeliveryOptions deliveryOptions,
        CancellationToken cancellation = default, TimeSpan? timeout = null)
        => InvokeCoreAsync(message, token => inner.InvokeAsync<T>(message, deliveryOptions, token, timeout), cancellation);

    private async Task<T> InvokeCoreAsync<T>(object message, Func<CancellationToken, Task<T>> innerCall,
        CancellationToken ct)
    {
        var cached = message.GetType().GetCustomAttribute<CachedAttribute>();
        return cached is null
            ? await innerCall(ct)
            : await GetOrCreateAsync(message, cached, innerCall, ct);
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