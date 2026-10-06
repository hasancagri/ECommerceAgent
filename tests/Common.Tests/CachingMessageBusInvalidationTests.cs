using Common.Results.BaseClasses;

namespace Common.Tests;

// Durable invalidation sözleşmesini kilitler: [InvalidatesCache] command başarılıysa decorator
// CacheInvalidationRequested YAYINLAR (senkron RemoveByTag değil); başarısız yazmada ve işaretsiz
// mesajda yayın YOK. Asıl boşaltma handler'da — burada yalnız publish kapısı test edilir.
public class CachingMessageBusInvalidationTests
{
    [InvalidatesCache("test-tag")]
    private record MarkedCommand;

    private record UnmarkedCommand;

    [Cached("epoch-tag", 1200)]
    private record CachedQuery;

    private sealed class TestResult : BaseResultModel;

    [Fact]
    public async Task InvalidateAsync_BumpsEpoch_NextReadMissesCache()
    {
        var (bus, inner) = BuildBus(new TestResult { IsSuccess = true });

        await bus.InvokeAsync<TestResult>(new CachedQuery());
        await bus.InvokeAsync<TestResult>(new CachedQuery());
        inner.InvokeCount.ShouldBe(1); // ikinci çağrı cache hit

        // Redis'siz mod: epoch lokal artar; uzun TTL'e (1200sn) rağmen sonraki okuma yeni anahtara düşer.
        await inner.Provider!.GetRequiredService<CacheInvalidator>().InvalidateAsync("epoch-tag");

        await bus.InvokeAsync<TestResult>(new CachedQuery());
        inner.InvokeCount.ShouldBe(2); // epoch bump → miss → kaynak yeniden
    }

    [Fact]
    public async Task SuccessfulMarkedCommand_PublishesInvalidationMessage()
    {
        var (bus, inner) = BuildBus(new TestResult { IsSuccess = true });

        await bus.InvokeAsync<TestResult>(new MarkedCommand());

        var published = inner.Published.ShouldHaveSingleItem()
            .ShouldBeOfType<CacheInvalidationRequested>();
        published.Tag.ShouldBe("test-tag");
    }

    [Fact]
    public async Task FailedMarkedCommand_DoesNotPublish()
    {
        var (bus, inner) = BuildBus(new TestResult { IsSuccess = false });

        await bus.InvokeAsync<TestResult>(new MarkedCommand());

        inner.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnmarkedCommand_DoesNotPublish()
    {
        var (bus, inner) = BuildBus(new TestResult { IsSuccess = true });

        await bus.InvokeAsync<TestResult>(new UnmarkedCommand());

        inner.Published.ShouldBeEmpty();
    }

    private static (IMessageBus Bus, RecordingMessageBus Inner) BuildBus(object invokeResult)
    {
        var inner = new RecordingMessageBus { InvokeResult = invokeResult };
        var services = new ServiceCollection();
        services.AddSingleton<IMessageBus>(inner);
        services.AddCachingAspect("test");
        var provider = services.BuildServiceProvider();
        inner.Provider = provider;
        return (provider.GetRequiredService<IMessageBus>(), inner);
    }

    // InvokeAsync<T> sabit sonuç döner + çağrı sayar, PublishAsync yayınları biriktirir; kalanı kullanılmaz.
    private sealed class RecordingMessageBus : IMessageBus
    {
        public required object InvokeResult { get; init; }
        public List<object> Published { get; } = [];
        public int InvokeCount { get; private set; }
        public ServiceProvider? Provider { get; set; }

        public string? TenantId { get; set; }

        public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default,
            TimeSpan? timeout = null)
        {
            InvokeCount++;
            return Task.FromResult((T)InvokeResult);
        }

        public Task<T> InvokeAsync<T>(object message, DeliveryOptions deliveryOptions,
            CancellationToken cancellation = default, TimeSpan? timeout = null)
        {
            InvokeCount++;
            return Task.FromResult((T)InvokeResult);
        }

        public ValueTask PublishAsync<T>(T message, DeliveryOptions? deliveryOptions = null)
        {
            Published.Add(message!);
            return ValueTask.CompletedTask;
        }

        public Task InvokeAsync(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
            => throw new NotSupportedException();

        public Task InvokeAsync(object message, DeliveryOptions deliveryOptions,
            CancellationToken cancellation = default, TimeSpan? timeout = null) => throw new NotSupportedException();

        public IAsyncEnumerable<T> StreamAsync<T>(object message, CancellationToken cancellation = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<T> StreamAsync<T>(object message, DeliveryOptions deliveryOptions,
            CancellationToken cancellation = default) => throw new NotSupportedException();

        public Task InvokeForTenantAsync(string tenantId, object message, CancellationToken cancellation = default,
            TimeSpan? timeout = null) => throw new NotSupportedException();

        public Task<T> InvokeForTenantAsync<T>(string tenantId, object message,
            CancellationToken cancellation = default, TimeSpan? timeout = null) => throw new NotSupportedException();

        public IDestinationEndpoint EndpointFor(string endpointName) => throw new NotSupportedException();

        public IDestinationEndpoint EndpointFor(Uri uri) => throw new NotSupportedException();

        public IReadOnlyList<Envelope> PreviewSubscriptions(object message) => throw new NotSupportedException();

        public IReadOnlyList<Envelope> PreviewSubscriptions(object message, DeliveryOptions deliveryOptions)
            => throw new NotSupportedException();

        public ValueTask SendAsync<T>(T message, DeliveryOptions? deliveryOptions = null)
            => throw new NotSupportedException();

        public ValueTask BroadcastToTopicAsync(string topicName, object message,
            DeliveryOptions? deliveryOptions = null) => throw new NotSupportedException();
    }
}