using Common.Results.BaseClasses;

namespace Common.Tests;

// Cache aspect sözleşmesini kilitler (090 sonrası): decorator YALNIZ [Cached] okuma yolunu işler —
// epoch bump sonraki okumayı yeni anahtara düşürür (miss). Boşaltma publish'i decorator'ın işi
// DEĞİL: hiçbir command için CacheInvalidationRequested yayınlaMAZ (yayın yazma handler'ının
// içinden, commit'le atomik outbox üzerinden yapılır — orası Wolverine entegrasyonu, burada değil).
public class CachingMessageBusInvalidationTests
{
    private record SomeCommand;

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
    public async Task InvokeAsync_Command_NeverPublishesFromDecorator()
    {
        var (bus, inner) = BuildBus(new TestResult { IsSuccess = true });

        await bus.InvokeAsync<TestResult>(new SomeCommand());

        inner.Published.ShouldBeEmpty(); // boşaltma handler-içi (090) — decorator publish etmez
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
