namespace Storefront.Api.Tests;

// 086 İLKE VI: event dizisi → StorefrontDocument fold. Her kaynak YALNIZ kendi alan grubunu yazar;
// kısmi satır geçerli; mutlak değer son-yazan-kazanır (R3). Saf, mock'suz.
public class StorefrontDocumentFoldTests
{
    private static readonly Guid Pid = Guid.NewGuid();

    private static IntegrationEvents.ProductChangedEvent Catalog(
        string name = "Dune", string description = "çöl gezegeni", decimal price = 100m,
        bool isDeleted = false, string? familyCode = null) =>
        new(Pid, name, description, price,
            [new IntegrationEvents.AuthorRef(Guid.NewGuid(), "Herbert")],
            Guid.NewGuid(), "Ithaki", Guid.NewGuid(), "science fiction",
            "http://img/dune.jpg", isDeleted,
            [new IntegrationEvents.ProductSpec("Cilt", "Ciltli")], familyCode);

    [Fact]
    public void Dort_kaynak_alan_gruplari_birlesir()
    {
        var doc = new StorefrontDocument();
        doc.ApplyCatalog(Catalog());
        doc.ApplyStock(new IntegrationEvents.StockChangedEvent(Pid, 7));
        doc.ApplyReviewSummary(new IntegrationEvents.ReviewSummaryChanged(Pid, 4.5m, 3));
        doc.ApplyDiscount(new IntegrationEvents.ProductDiscountChanged(Pid, 20, null, null));

        doc.Id.ShouldBe(Pid);
        doc.Name.ShouldBe("Dune");
        doc.Price.ShouldBe(100m);
        doc.Authors.ShouldContain("Herbert");
        doc.Publisher.ShouldBe("Ithaki");
        doc.Category.ShouldBe("science fiction");
        doc.Specs.ShouldContain(s => s.Attribute == "Cilt" && s.Option == "Ciltli");
        doc.Stock.ShouldBe(7);
        doc.RatingAverage.ShouldBe(4.5m);
        doc.RatingCount.ShouldBe(3);
        doc.DiscountPct.ShouldBe(20);
    }

    [Fact]
    public void ApplyStock_PartialRowWhenOnlyStockArrives_IsValid()
    {
        // Catalog henüz raporlamadı — yalnız stok event'i geldi. Doc kurulur, Name null kalır.
        var doc = new StorefrontDocument();
        doc.ApplyStock(new IntegrationEvents.StockChangedEvent(Pid, 5));

        doc.Id.ShouldBe(Pid);
        doc.Stock.ShouldBe(5);
        doc.Name.ShouldBeNull();
        doc.Price.ShouldBeNull();
    }

    [Fact]
    public void ApplyStock_AbsoluteValueLastWriterWins()
    {
        var doc = new StorefrontDocument();
        doc.ApplyStock(new IntegrationEvents.StockChangedEvent(Pid, 5));
        doc.ApplyStock(new IntegrationEvents.StockChangedEvent(Pid, 2));

        doc.Stock.ShouldBe(2);
    }

    [Fact]
    public void ApplyCatalog_RepeatWritesWholeFieldGroupWithLatestEvent()
    {
        var doc = new StorefrontDocument();
        doc.ApplyCatalog(Catalog(name: "Eski", price: 100m));
        doc.ApplyStock(new IntegrationEvents.StockChangedEvent(Pid, 9));
        doc.ApplyCatalog(Catalog(name: "Yeni", price: 120m));

        doc.Name.ShouldBe("Yeni");
        doc.Price.ShouldBe(120m);
        // stok Catalog alan grubunun DIŞINDA — Catalog tekrarı dokunmaz.
        doc.Stock.ShouldBe(9);
    }

    [Fact]
    public void ReviewSummary_count_sifir_ozeti_temizler()
    {
        var doc = new StorefrontDocument();
        doc.ApplyReviewSummary(new IntegrationEvents.ReviewSummaryChanged(Pid, 4.5m, 3));
        doc.ApplyReviewSummary(new IntegrationEvents.ReviewSummaryChanged(Pid, 0m, 0));

        doc.RatingAverage.ShouldBeNull();
        doc.RatingCount.ShouldBe(0);
    }

    [Fact]
    public void Discount_temizlik_pct_sifir_alanlari_nulllar()
    {
        var doc = new StorefrontDocument();
        doc.ApplyDiscount(new IntegrationEvents.ProductDiscountChanged(Pid, 25, DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));
        doc.ApplyDiscount(new IntegrationEvents.ProductDiscountChanged(Pid, 0, null, null));

        doc.DiscountPct.ShouldBeNull();
        doc.DiscountStartsAt.ShouldBeNull();
        doc.DiscountEndsAt.ShouldBeNull();
    }

    [Fact]
    public void FamilyCode_WhenArrivesEmpty_LeavesTheFamily()
    {
        var doc = new StorefrontDocument();
        doc.ApplyCatalog(Catalog(familyCode: "FAM-1"));
        doc.FamilyCode.ShouldBe("FAM-1");

        doc.ApplyCatalog(Catalog(familyCode: "   "));
        doc.FamilyCode.ShouldBeNull();
    }
}