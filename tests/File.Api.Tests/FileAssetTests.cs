using FileApi.Domains.FileAsset;
using Shouldly;
using Xunit;

namespace File.Api.Tests;

public class FileAssetTests
{
    private static FileAsset NewAsset(StorageType type = StorageType.R2, string path = "9783161484100")
        => FileAsset.Create("9783161484100", "image/jpeg", 1024, type, path).Data!;

    [Fact]
    public void Create_WithFirstLocation_Succeeds_And_HasOneLocation()
    {
        var result = FileAsset.Create("9783161484100", "image/jpeg", 2048, StorageType.R2, "9783161484100");

        result.IsSuccess.ShouldBeTrue();
        var asset = result.Data!;
        asset.ImageName.ShouldBe("9783161484100");
        asset.Locations.Count.ShouldBe(1);
        asset.Locations[0].StorageType.ShouldBe(StorageType.R2);
    }

    [Theory]
    [InlineData("../etc/passwd")]   // traversal
    [InlineData("foo/bar")]         // ayraç
    [InlineData("")]                // boş
    public void Create_WithUnsafeImageName_Fails(string imageName)
    {
        var result = FileAsset.Create(imageName, "image/jpeg", 10, StorageType.R2, "key");

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == FileApi.Constants.FileApiResourceConstants.FILE_IMAGENAME_INVALID);
    }

    [Fact]
    public void Create_WithEmptyStoragePath_Fails()
    {
        var result = FileAsset.Create("9783161484100", "image/jpeg", 10, StorageType.R2, "  ");

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void AddOrReplaceLocation_SameStorageType_UpsertsPath_NoSecondRow()
    {
        var asset = NewAsset(StorageType.R2, "old-key");

        var r = asset.AddOrReplaceLocation(StorageType.R2, "new-key");

        r.IsSuccess.ShouldBeTrue();
        asset.Locations.Count(l => l.StorageType == StorageType.R2).ShouldBe(1);
        asset.Locations.Single(l => l.StorageType == StorageType.R2).StorageFilePath.ShouldBe("new-key");
    }

    [Fact]
    public void AddOrReplaceLocation_DifferentStorageType_Adds()
    {
        var asset = NewAsset(StorageType.R2, "r2-key");

        asset.AddOrReplaceLocation(StorageType.Local, "local-key");

        asset.Locations.Count.ShouldBe(2);
        asset.Locations.ShouldContain(l => l.StorageType == StorageType.Local && l.StorageFilePath == "local-key");
    }

    [Fact]
    public void RemoveLocation_LastLocation_IsRejected()
    {
        var asset = NewAsset();

        var r = asset.RemoveLocation(StorageType.R2);

        r.IsSuccess.ShouldBeFalse();
        r.Messages!.ShouldContain(m => m.Code == FileApi.Constants.FileApiResourceConstants.FILE_LOCATION_LAST_CANNOT_REMOVE);
        asset.Locations.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveLocation_NonLast_Removes()
    {
        var asset = NewAsset(StorageType.R2, "r2-key");
        asset.AddOrReplaceLocation(StorageType.Local, "local-key");

        var r = asset.RemoveLocation(StorageType.Local);

        r.IsSuccess.ShouldBeTrue();
        asset.Locations.Count.ShouldBe(1);
        asset.Locations[0].StorageType.ShouldBe(StorageType.R2);
    }

    [Fact]
    public void PreferredLocation_ReturnsDefaultType_WhenPresent()
    {
        var asset = NewAsset(StorageType.Local, "local-key");
        asset.AddOrReplaceLocation(StorageType.R2, "r2-key");

        asset.PreferredLocation(StorageType.R2).StorageFilePath.ShouldBe("r2-key");
    }

    [Fact]
    public void PreferredLocation_FallsBackToFirst_WhenDefaultTypeAbsent()
    {
        var asset = NewAsset(StorageType.Local, "local-key");

        asset.PreferredLocation(StorageType.R2).StorageType.ShouldBe(StorageType.Local);
    }
}