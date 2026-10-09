namespace Storefront.Api.Tests;

// 086 FR-008: satılabilirlik kararı — doc ES'e YALNIZ satılabilirse yazılır; aksi halde SİLİNİR.
// IsDeleted=true ya da Price yok (Catalog raporlamadı) → satılamaz.
public class StorefrontDocumentSellabilityTests
{
    private static readonly Guid Pid = Guid.NewGuid();

    private static IntegrationEvents.ProductChangedEvent Catalog(decimal price = 50m, bool isDeleted = false) =>
        new(Pid, "Kitap", "açıklama", price, [], Guid.NewGuid(), "Yayınevi",
            Guid.NewGuid(), "fiction", null, isDeleted);

    [Fact]
    public void Dolu_catalog_satir_satilabilir()
    {
        var doc = new StorefrontDocument();
        doc.ApplyCatalog(Catalog());
        doc.IsSellable.ShouldBeTrue();
    }

    [Fact]
    public void IsDeleted_true_satilamaz()
    {
        var doc = new StorefrontDocument();
        doc.ApplyCatalog(Catalog(isDeleted: true));
        doc.IsSellable.ShouldBeFalse();
    }

    [Fact]
    public void IsSellable_PriceAbsentWhenOnlyStockArrived_NotSellable()
    {
        // Catalog hiç raporlamadı → Price null → satılamaz (index'te hiç bulunmaz).
        var doc = new StorefrontDocument();
        doc.ApplyStock(new IntegrationEvents.StockChangedEvent(Pid, 3));
        doc.IsSellable.ShouldBeFalse();
    }

    [Fact]
    public void Yalniz_review_geldi_satilamaz()
    {
        var doc = new StorefrontDocument();
        doc.ApplyReviewSummary(new IntegrationEvents.ReviewSummaryChanged(Pid, 4m, 2));
        doc.IsSellable.ShouldBeFalse();
    }
}