
namespace Catalog.Api.Tests;

// 095 T007: yayın-anı kategori zincir çözümü (CategoryNaming.ResolveChain) — leaf + ata (ad VE id),
// leaf-önce sıra. Seed Faz B + FileConsumers paylaşır (fat-event tek kaynak). Related zincire GİRMEZ
// (çağıran yalnız primary leaf verir — FR-005 editoryal ayrım).
public class CategoryChainResolverTests
{
    private static Category Cat(string name, Guid? parentId = null) =>
        Category.Create(name, parentId).Data!;

    private static Dictionary<Guid, Category> Map(params Category[] categories) =>
        categories.ToDictionary(c => c.Id);

    [Fact]
    public void ResolveChain_LeafWithParent_ReturnsLeafThenAncestor()
    {
        var parent = Cat("Kültür");
        var leaf = Cat("Popüler Kültür", parent.Id);

        var (ids, names) = CategoryNaming.ResolveChain(leaf.Id, Map(parent, leaf));

        ids.ShouldBe(new[] { leaf.Id, parent.Id });
        names.ShouldBe(new[] { "Popüler Kültür", "Kültür" });
    }

    [Fact]
    public void ResolveChain_RootLeaf_ReturnsSingleElement()
    {
        var root = Cat("Genel");

        var (ids, names) = CategoryNaming.ResolveChain(root.Id, Map(root));

        ids.ShouldBe(new[] { root.Id });
        names.ShouldBe(new[] { "Genel" });
    }

    [Fact]
    public void ResolveChain_UnknownLeaf_ReturnsEmpty()
    {
        var root = Cat("Kültür");

        var (ids, names) = CategoryNaming.ResolveChain(Guid.NewGuid(), Map(root));

        ids.ShouldBeEmpty();
        names.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveChain_ParentMissingFromMap_StopsAtLeaf()
    {
        var leaf = Cat("Popüler Kültür", Guid.NewGuid()); // ebeveyn haritada yok (savunma)

        var (ids, names) = CategoryNaming.ResolveChain(leaf.Id, Map(leaf));

        ids.ShouldBe(new[] { leaf.Id });
        names.ShouldBe(new[] { "Popüler Kültür" });
    }
}
