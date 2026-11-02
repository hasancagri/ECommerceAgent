
namespace Common.Tests;

// Backplane'in DI varsayımını kilitler: Redis (IConnectionMultiplexer) KAYITSIZKEN opsiyonel
// ctor parametresi null düşmeli ve tüm kayıt zinciri (decorator + invalidator + subscriber)
// çözülebilir kalmalı — aksi açılış anında patlar (tek-instance / L1-only mod).
public class CachingAspectRegistrationTests
{
    [Fact]
    public void AddCachingAspect_WithoutRedis_ResolvesDecoratorInvalidatorAndSubscriber()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageBus>(new FakeMessageBus());
        services.AddCachingAspect("test");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IMessageBus>().ShouldBeOfType<CachingMessageBus>();
        provider.GetRequiredService<CacheInvalidator>().ShouldNotBeNull();
        provider.GetServices<IHostedService>().ShouldContain(s => s is CacheBackplaneSubscriber);
    }

    [Fact]
    public async Task CacheInvalidator_WithoutRedis_InvalidatesLocallyWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageBus>(new FakeMessageBus());
        services.AddCachingAspect("test");
        await using var provider = services.BuildServiceProvider();

        var invalidator = provider.GetRequiredService<CacheInvalidator>();

        await Should.NotThrowAsync(invalidator.InvalidateAsync("some-tag"));
    }

    // Yalnız kayıt zincirini çözmek için: hiçbir üyesi çağrılmaz.
    private sealed class FakeMessageBus : IMessageBus
    {
        public string? TenantId { get; set; }

        public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default,
            TimeSpan? timeout = null) => throw new NotSupportedException();

        public Task<T> InvokeAsync<T>(object message, DeliveryOptions deliveryOptions,
            CancellationToken cancellation = default, TimeSpan? timeout = null) => throw new NotSupportedException();

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

        public ValueTask PublishAsync<T>(T message, DeliveryOptions? deliveryOptions = null)
            => throw new NotSupportedException();

        public ValueTask BroadcastToTopicAsync(string topicName, object message,
            DeliveryOptions? deliveryOptions = null) => throw new NotSupportedException();
    }
}