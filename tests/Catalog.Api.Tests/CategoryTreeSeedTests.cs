
namespace Catalog.Api.Tests;

// 095 T010: kapalı kategori ağacı kurucu saf çekirdeği (Faz A) — kanonik yollardan deterministik düğüm
// listesi. Ad ağaç-global unique varsayımı İHLALDE deterministik hata (sessiz çift kayıt yok); ≤2 seviye.
public class CategoryTreeSeedTests
{
    [Fact]
    public void Build_TwoLevelPaths_ReturnsRootsBeforeChildren()
    {
        var paths = new List<IReadOnlyList<string>>
        {
            new[] { "Kültür", "Popüler Kültür" },
            new[] { "Edebiyat", "Roman" },
            new[] { "Genel" },
        };

        var nodes = CategoryTreeSeed.Build(paths);

        nodes.Select(n => n.Name).ShouldBe(new[] { "Kültür", "Edebiyat", "Genel", "Popüler Kültür", "Roman" });
        nodes.First(n => n.Name == "Popüler Kültür").ParentName.ShouldBe("Kültür");
        nodes.First(n => n.Name == "Roman").ParentName.ShouldBe("Edebiyat");
        nodes.First(n => n.Name == "Genel").ParentName.ShouldBeNull();
    }

    [Fact]
    public void Build_DuplicateAndMessyPaths_DeduplicatesByNormalizedName()
    {
        var paths = new List<IReadOnlyList<string>>
        {
            new[] { "Kültür", "Popüler Kültür" },
            new[] { " kültür ", "popüler  kültür" }, // aynı ağaç — normalize dedup
            new[] { "Kültür" },
        };

        var nodes = CategoryTreeSeed.Build(paths);

        nodes.Count.ShouldBe(2);
        nodes[0].Name.ShouldBe("Kültür");
        nodes[1].Name.ShouldBe("Popüler Kültür");
    }

    [Fact]
    public void Build_SameChildUnderTwoParents_ThrowsDeterministicError()
    {
        var paths = new List<IReadOnlyList<string>>
        {
            new[] { "Kültür", "Deneme" },
            new[] { "Edebiyat", "Deneme" }, // ad ağaç-global unique varsayımı bozuldu
        };

        var ex = Should.Throw<InvalidOperationException>(() => CategoryTreeSeed.Build(paths));
        ex.Message.ShouldContain("Deneme");
    }

    [Fact]
    public void Build_NameBothRootAndChild_ThrowsDeterministicError()
    {
        var paths = new List<IReadOnlyList<string>>
        {
            new[] { "Deneme" },
            new[] { "Edebiyat", "Deneme" },
        };

        Should.Throw<InvalidOperationException>(() => CategoryTreeSeed.Build(paths));
    }

    [Fact]
    public void Build_MoreThanTwoLevels_ThrowsDeterministicError()
    {
        var paths = new List<IReadOnlyList<string>>
        {
            new[] { "Kültür", "Popüler Kültür", "Alt Alt" },
        };

        Should.Throw<InvalidOperationException>(() => CategoryTreeSeed.Build(paths));
    }

    [Fact]
    public void Build_LeafEqualsRoot_CollapsesToSingleRoot()
    {
        var paths = new List<IReadOnlyList<string>>
        {
            new[] { "Şiir", " şiir " }, // leaf==üst savunma guard'ı (kanonik veri üretmez)
        };

        var nodes = CategoryTreeSeed.Build(paths);

        nodes.Count.ShouldBe(1);
        nodes[0].Name.ShouldBe("Şiir");
        nodes[0].ParentName.ShouldBeNull();
    }

    [Fact]
    public void Build_EmptySegmentsAndPaths_AreSkipped()
    {
        var paths = new List<IReadOnlyList<string>>
        {
            new string[] { },
            new[] { "  " },
            new[] { "Kültür", "" },
        };

        var nodes = CategoryTreeSeed.Build(paths);

        nodes.Count.ShouldBe(1);
        nodes[0].Name.ShouldBe("Kültür");
    }
}
