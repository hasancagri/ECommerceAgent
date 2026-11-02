using FileApi.Domains.FileAsset;
using FileApi.Options;
using FileApi.UrlResolution;
using Shouldly;
using Xunit;

namespace File.Api.Tests;

public class CoverUrlResolverTests
{
    private static CoverUrlResolver Resolver(StorageType defaultType = StorageType.R2)
        => new(new StorageBaseUrlsOptions
        {
            DefaultStorageType = defaultType,
            Bases = new Dictionary<string, string>
            {
                ["R2"] = "https://cdn.example.com/covers",       // sonda / yok
                ["Local"] = "https://host.local/files/v1/covers/" // sonda / var
            }
        });

    [Fact]
    public void Resolve_R2_ProducesUrl()
    {
        Resolver().Resolve(StorageType.R2, "9783161484100")
            .ShouldBe("https://cdn.example.com/covers/9783161484100");
    }

    [Fact]
    public void Resolve_TrimsDuplicateSlashes()
    {
        // base sonda /, path başta / → tek slash.
        Resolver().Resolve(StorageType.Local, "/9783161484100")
            .ShouldBe("https://host.local/files/v1/covers/9783161484100");
    }

    [Fact]
    public void Resolve_UnknownStorageType_ReturnsNull()
    {
        Resolver().Resolve(StorageType.S3, "key").ShouldBeNull();   // S3 base map'te yok
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsNull()
    {
        Resolver().Resolve(StorageType.R2, "  ").ShouldBeNull();
    }

    [Fact]
    public void DefaultStorageType_ReflectsConfig()
    {
        Resolver(StorageType.Local).DefaultStorageType.ShouldBe(StorageType.Local);
    }
}