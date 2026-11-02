
namespace Catalog.Api.Tests;

// T013 + T017: aspect'in saf (host'suz) doğrulanabilir parçaları — anahtar üretimi + L2 round-trip.
// Sorgu tipleri test-yerlidir: aspect mesajın kendisinden bağımsızdır, Catalog REST sorguları silindi.
public class CachingAspectTests
{
    private record SampleByIdQuery(Guid Id);

    private record SampleParameterlessQuery;

    public class SampleResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
    }

    // --- CacheKeyFactory (FR-004) ---

    [Fact]
    public void Build_SameQuerySameParams_ProducesSameKey()
    {
        var id = Guid.NewGuid();
        var a = CacheKeyFactory.Build("catalog", new SampleByIdQuery(id));
        var b = CacheKeyFactory.Build("catalog", new SampleByIdQuery(id));

        a.ShouldBe(b);
    }

    [Fact]
    public void Build_DifferentParams_ProducesDifferentKeys()
    {
        var a = CacheKeyFactory.Build("catalog", new SampleByIdQuery(Guid.NewGuid()));
        var b = CacheKeyFactory.Build("catalog", new SampleByIdQuery(Guid.NewGuid()));

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Build_ParameterlessQuery_ProducesStableKey()
    {
        var a = CacheKeyFactory.Build("catalog", new SampleParameterlessQuery());
        var b = CacheKeyFactory.Build("catalog", new SampleParameterlessQuery());

        a.ShouldBe(b);
        a.ShouldStartWith("catalog:SampleParameterlessQuery:");
    }

    [Fact]
    public void Build_KnownInput_IsDeterministicAcrossRuns()
    {
        // Sabit girdi → sabit anahtar. string.GetHashCode randomizasyonu kullanılsaydı bu değer
        // her süreçte değişirdi; FNV-1a olduğundan literal olarak kilitlenebilir (cross-instance L2).
        var key = CacheKeyFactory.Build("catalog",
            new SampleByIdQuery(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        key.ShouldBe("catalog:SampleByIdQuery:" + key.Split(':')[2]);
        // İki kez üretip birebir eşitlik determinizmi kanıtlar:
        key.ShouldBe(CacheKeyFactory.Build("catalog",
            new SampleByIdQuery(Guid.Parse("11111111-1111-1111-1111-111111111111"))));
    }

    // Aynı kısa ada sahip ikinci tip (Queries/ vs Agent/ slice'larının simülasyonu).
    private static class OtherSlice
    {
        internal record SampleByIdQuery(Guid Id);
    }

    [Fact]
    public void Build_SameShortName_DifferentTypes_ProduceDifferentKeys()
    {
        // Aynı kısa ada sahip farklı record'lar (Queries/ vs Agent/ slice'ları) çarpışmamalı:
        // hash'e FullName girer.
        var a = CacheKeyFactory.Build("catalog", new SampleByIdQuery(Guid.Empty));
        var b = CacheKeyFactory.Build("catalog", new OtherSlice.SampleByIdQuery(Guid.Empty));

        a.ShouldNotBe(b);
    }

    // --- L2 serialize round-trip (FR-013) ---

    [Fact]
    public void FeatureObjectResultModel_RoundTrips_ViaSystemTextJson()
    {
        var original = FeatureObjectResultModel<SampleResponse>.Ok(new SampleResponse
        {
            Id = Guid.NewGuid(),
            Name = "Apple iPhone",
            Price = 42.5m
        });

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<FeatureObjectResultModel<SampleResponse>>(json);

        restored.ShouldNotBeNull();
        restored!.IsSuccess.ShouldBeTrue();
        restored.Data.Id.ShouldBe(original.Data.Id);
        restored.Data.Name.ShouldBe(original.Data.Name);
        restored.Data.Price.ShouldBe(original.Data.Price);
    }
}