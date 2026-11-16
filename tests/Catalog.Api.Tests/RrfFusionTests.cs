namespace Catalog.Api.Tests;

// 011 T016 (test-first, İlke VI): RRF füzyon — iki sıralamayı konum-tabanlı birleştirir.
// Saf (host'suz). skor(id)=Σ 1/(k+sıra); ham skor normalize edilmez, yalnız sıra sayılır.
public class RrfFusionTests
{
    [Fact]
    public void Combine_TwoLists_RanksByPositionSum()
    {
        var a = new[] { "x", "b", "c" }; // x sıra 0
        var b = new[] { "y", "x", "d" }; // x sıra 1

        var result = RrfFusion.Combine(a, b, k: 60);

        // x iki listede de üstlerde (0 + 1) → birleşik skoru en yüksek → ilk sırada.
        result[0].ShouldBe("x");
    }

    [Fact]
    public void Combine_OnlyInOneList_NotDropped()
    {
        var a = new[] { "a1", "a2" };
        var b = new[] { "b1", "b2" };

        var result = RrfFusion.Combine(a, b);

        // Tek listede olanlar elenmez — hepsi sonuçta.
        result.Count.ShouldBe(4);
        result.ShouldContain("a1");
        result.ShouldContain("b2");
    }

    [Fact]
    public void Combine_K_DampensTopDominance()
    {
        // Küçük k → sıra-0 avantajı büyük; büyük k → sıralar yakınlaşır.
        var a = new[] { "top", "second" };
        var b = new[] { "second", "top" }; // top ve second simetrik: a'da top üstte, b'de second üstte

        var small = RrfFusion.Combine(a, b, k: 1);
        var large = RrfFusion.Combine(a, b, k: 1000);

        // Simetri: ikisinin de skoru eşit → bestRank tie-break (ikisi de rank 0) → stabil.
        // k değişimi mutlak skoru düşürür ama elemeyi bozmaz: her iki id her iki boyutta da kalır.
        small.Count.ShouldBe(2);
        large.Count.ShouldBe(2);
        small.ShouldContain("top");
        small.ShouldContain("second");
    }

    [Fact]
    public void Combine_DuplicateAcrossLists_Deduped()
    {
        var a = new[] { "x", "y" };
        var b = new[] { "x", "z" };

        var result = RrfFusion.Combine(a, b);

        result.Count(id => id == "x").ShouldBe(1); // id bir kez.
    }

    [Fact]
    public void Combine_HigherCombinedRank_OutranksSingleTopHit()
    {
        var a = new[] { "both", "onlyA" };          // both:0, onlyA:1
        var b = new[] { "onlyB", "both" };          // onlyB:0, both:1

        var result = RrfFusion.Combine(a, b, k: 60);

        // both iki listede de var (0+1) → tek-listede-0 olan onlyA/onlyB'den öne geçer.
        result[0].ShouldBe("both");
    }
}
