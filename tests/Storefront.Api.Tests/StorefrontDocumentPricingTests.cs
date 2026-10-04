namespace Storefront.Api.Tests;

// 086: effective_price türetimi — indirim penceresi içi (indirimli) / dışı (liste fiyatı). YAZIM
// anında hesaplanır (SQL view-guard'ın yerini alır); test "now" parametresiyle pencereyi zorlar.
public class StorefrontDocumentPricingTests
{
    private static readonly Guid Pid = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static StorefrontDocument WithPriceAndDiscount(decimal price, int pct, DateTime? startsAt, DateTime? endsAt)
    {
        var doc = new StorefrontDocument();
        doc.ApplyCatalog(new IntegrationEvents.ProductChangedEvent(
            Pid, "Kitap", "açıklama", price, [], Guid.NewGuid(), "Yayınevi",
            Guid.NewGuid(), "fiction", null, false));
        doc.ApplyDiscount(new IntegrationEvents.ProductDiscountChanged(Pid, pct, startsAt, endsAt));
        return doc;
    }

    [Fact]
    public void Indirim_yoksa_etkin_fiyat_liste_fiyatidir()
    {
        var doc = new StorefrontDocument();
        doc.ApplyCatalog(new IntegrationEvents.ProductChangedEvent(
            Pid, "Kitap", "x", 100m, [], Guid.NewGuid(), "Y", Guid.NewGuid(), "fiction", null, false));

        doc.EffectivePrice(Now).ShouldBe(100m);
        doc.IsDiscountActive(Now).ShouldBeFalse();
    }

    [Fact]
    public void Pencere_icinde_etkin_fiyat_indirimlidir()
    {
        var doc = WithPriceAndDiscount(100m, 20,
            Now.AddDays(-1).UtcDateTime, Now.AddDays(1).UtcDateTime);

        doc.EffectivePrice(Now).ShouldBe(80m);
        doc.IsDiscountActive(Now).ShouldBeTrue();
    }

    [Fact]
    public void Pencere_disinda_bitmis_etkin_fiyat_liste_fiyatidir()
    {
        var doc = WithPriceAndDiscount(100m, 20,
            Now.AddDays(-5).UtcDateTime, Now.AddDays(-1).UtcDateTime);

        doc.EffectivePrice(Now).ShouldBe(100m);
        doc.IsDiscountActive(Now).ShouldBeFalse();
    }

    [Fact]
    public void Pencere_baslamadan_etkin_fiyat_liste_fiyatidir()
    {
        var doc = WithPriceAndDiscount(100m, 20,
            Now.AddDays(1).UtcDateTime, Now.AddDays(5).UtcDateTime);

        doc.EffectivePrice(Now).ShouldBe(100m);
    }

    [Fact]
    public void Suresiz_indirim_basladiysa_aktiftir()
    {
        var doc = WithPriceAndDiscount(200m, 50, Now.AddDays(-1).UtcDateTime, null);

        doc.EffectivePrice(Now).ShouldBe(100m);
        doc.IsDiscountActive(Now).ShouldBeTrue();
    }
}