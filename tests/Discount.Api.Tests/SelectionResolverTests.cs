namespace Discount.Api.Tests;

// 079 İLKE VI: süzgeç → kitap seti saf çekirdeği test-first. Kategori/yazar/yayınevi doğru set;
// tek-kitap doğrudan; yayınlanmamış hariç; çözülmeyen süzgeç boş.
public class SelectionResolverTests
{
    private static ProductCatalogRef Ref(Guid pid, Guid cat, Guid pub, bool published, params Guid[] authors)
    {
        var r = ProductCatalogRef.Create(pid);
        r.Apply(cat, authors, pub, published);
        return r;
    }

    [Fact]
    public void CategoryFilter_Matches_ReturnsPublished()
    {
        var cat = Guid.NewGuid();
        var pub = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var other = Guid.NewGuid();
        var catalog = new[]
        {
            Ref(p1, cat, pub, true),
            Ref(p2, cat, pub, true),
            Ref(other, Guid.NewGuid(), pub, true),
        };

        var result = CampaignSelectionResolver.Resolve(ScopeType.Category, cat, catalog);

        result.ShouldBe(new[] { p1, p2 }, ignoreOrder: true);
    }

    [Fact]
    public void Unpublished_excluded()
    {
        var cat = Guid.NewGuid();
        var visible = Guid.NewGuid();
        var catalog = new[]
        {
            Ref(visible, cat, Guid.NewGuid(), true),
            Ref(Guid.NewGuid(), cat, Guid.NewGuid(), false),
        };

        var result = CampaignSelectionResolver.Resolve(ScopeType.Category, cat, catalog);

        result.ShouldBe(new[] { visible });
    }

    [Fact]
    public void AuthorFilter_Matches_Membership()
    {
        var author = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var catalog = new[]
        {
            Ref(p1, Guid.NewGuid(), Guid.NewGuid(), true, author, Guid.NewGuid()),
            Ref(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true, Guid.NewGuid()),
        };

        var result = CampaignSelectionResolver.Resolve(ScopeType.Author, author, catalog);

        result.ShouldBe(new[] { p1 });
    }

    [Fact]
    public void PublisherFilter_Matches_Membership()
    {
        var pub = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var catalog = new[]
        {
            Ref(p1, Guid.NewGuid(), pub, true),
            Ref(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true),
        };

        var result = CampaignSelectionResolver.Resolve(ScopeType.Publisher, pub, catalog);

        result.ShouldBe(new[] { p1 });
    }

    [Fact]
    public void Product_scope_returns_ref_directly_ignoring_catalog()
    {
        var productId = Guid.NewGuid();

        var result = CampaignSelectionResolver.Resolve(ScopeType.Product, productId, []);

        result.ShouldBe(new[] { productId });
    }

    [Fact]
    public void Unresolved_filter_returns_empty()
    {
        var catalog = new[] { Ref(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true) };

        var result = CampaignSelectionResolver.Resolve(ScopeType.Category, Guid.NewGuid(), catalog);

        result.ShouldBeEmpty();
    }
}
