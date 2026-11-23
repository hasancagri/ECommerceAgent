namespace Catalog.Api.Tests;

// 021 T002 (test-first, İlke VI): BookTaxonomy kapalı sözlük — pipeline SYSTEM_PROMPT listeleri
// (tur 20 · ton 20 · seviye 9 · konu 49) + "belirsiz" + takma-ad kanonikleştirme + seviye uyum
// grupları (çocuk↔çocuk, yetişkin↔yetişkin, genç yetişkin köprü, belirsiz her yerde).
public class BookTaxonomyTests
{
    [Fact]
    public void Genres_ContainsCanonicalCount_Has20Values()
    {
        BookTaxonomy.Genres.Count.ShouldBe(20);
        BookTaxonomy.Genres.ShouldContain("roman");
        BookTaxonomy.Genres.ShouldContain("çocuk resimli kitap");
        BookTaxonomy.Genres.ShouldContain(BookTaxonomy.Unknown);
    }

    [Fact]
    public void Moods_ContainsCanonicalCount_Has20Values()
    {
        BookTaxonomy.Moods.Count.ShouldBe(20);
        BookTaxonomy.Moods.ShouldContain("mizahi/absürt");
        BookTaxonomy.Moods.ShouldContain("ağır/düşündürücü");
        BookTaxonomy.Moods.ShouldNotContain(BookTaxonomy.Unknown); // ton "belirsiz" almaz — boş liste yeter
    }

    [Fact]
    public void AudienceLevels_ContainsCanonicalCount_Has9Values()
    {
        BookTaxonomy.AudienceLevels.Count.ShouldBe(9);
        BookTaxonomy.AudienceLevels.ShouldContain("çocuk (7-10)");
        BookTaxonomy.AudienceLevels.ShouldContain("yetişkin-orta");
        BookTaxonomy.AudienceLevels.ShouldContain(BookTaxonomy.Unknown);
    }

    [Fact]
    public void Themes_ContainsCanonicalCount_Has49Values()
    {
        BookTaxonomy.Themes.Count.ShouldBe(49);
        BookTaxonomy.Themes.ShouldContain("aşk/romantizm");
        BookTaxonomy.Themes.ShouldContain("distopya/ütopya");
        BookTaxonomy.Themes.ShouldContain("kolonyalizm/sömürgecilik");
    }

    [Theory]
    [InlineData("roman", true)]
    [InlineData("belirsiz", true)]
    [InlineData("şarkı sözü", false)]
    [InlineData("", false)]
    public void IsValidGenre_VariousValues_MatchesDictionary(string value, bool expected)
    {
        BookTaxonomy.IsValidGenre(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData("karanlık", true)]
    [InlineData("duygusal/dokunaklı", false)] // Aşama-1 sapması — kanonik değil
    public void IsValidMood_VariousValues_MatchesDictionary(string value, bool expected)
    {
        BookTaxonomy.IsValidMood(value).ShouldBe(expected);
    }

    // --- Kanonikleştirme (takma-ad haritası: v11 verisindeki bariz sapmalar) ---

    [Theory]
    [InlineData("karanlık", "karanlık")]           // zaten kanonik
    [InlineData("  karanlık ", "karanlık")]        // trim
    [InlineData("mizahi", "mizahi/absürt")]        // en sık sapma (223 atom)
    [InlineData("absürt", "mizahi/absürt")]
    [InlineData("ironik/hicivli", "ironik/alaycı")]
    [InlineData("sakin/tefekkür", "pastoral/sakin")]
    [InlineData("samimi/sohbet tonu", "sıcak/samimi")]
    [InlineData("duygusal/dokunaklı", "hüzünlü/melankolik")] // gevşek ama kanonik
    [InlineData("gerçekçi/soğukkanlı", null)]      // karşılık yok — düşer
    [InlineData("hafif/akıcı", null)]
    [InlineData("umutlu/iyimser", null)]
    [InlineData("uydurma-ton", null)]
    public void CanonicalMood_AliasAndUnknown_MapsOrDrops(string raw, string? expected)
    {
        BookTaxonomy.CanonicalMood(raw).ShouldBe(expected);
    }

    [Theory]
    [InlineData("tarih", "tarih/bellek")]
    [InlineData("savaş", "savaş/barış")]
    [InlineData("suç", "suç/adalet")]
    [InlineData("dostluk", "arkadaşlık")]
    [InlineData("eğitim", "eğitim/öğrenme")]
    [InlineData("hayvanlar", "hayvan/doğa")]
    [InlineData("aşk/ilişkiler", "aşk/romantizm")]
    [InlineData("kimlik/arayış", "kimlik")]
    [InlineData("ekonomi", "para/ekonomi")]
    [InlineData("bellek/geçmiş", "tarih/bellek")]
    [InlineData("tarihi-kurgu", "tarih/bellek")]
    [InlineData("sosyoloji", "toplumsal yaşam")]
    [InlineData("sanat/edebiyat tarihi", "sanat/yaratıcılık")]
    [InlineData("yaşlılık/ölüm", "ölüm/kayıp")]
    [InlineData("sınıf/yoksulluk", "sınıf/tabakalaşma")]
    [InlineData("aile", "aile")]
    [InlineData("kent/İstanbul", null)]            // yer adı — düşer
    [InlineData("fantastik", null)]                // tür artığı — düşer
    [InlineData("polisiye/gizem", null)]           // tür/ton — düşer
    public void CanonicalTheme_AliasAndUnknown_MapsOrDrops(string raw, string? expected)
    {
        BookTaxonomy.CanonicalTheme(raw).ShouldBe(expected);
    }

    [Theory]
    [InlineData("yetişkin-orta", "yetişkin-orta")]
    [InlineData("yetişkin-ileri/akademik", "yetişkin-ileri")]
    [InlineData("genç (13–17)", "genç yetişkin")]
    [InlineData("çocuk (7–12)", "çocuk (7-10)")]   // en-dash varyantı
    [InlineData("okul öncesi (0–6)", "çocuk (4-6)")]
    [InlineData("bilinmeyen", null)]
    public void CanonicalAudienceLevel_AliasAndUnknown_MapsOrDrops(string raw, string? expected)
    {
        BookTaxonomy.CanonicalAudienceLevel(raw).ShouldBe(expected);
    }

    [Theory]
    [InlineData("roman", "roman")]
    [InlineData("uydurma-tur", null)]
    public void CanonicalGenre_KnownAndUnknown_MapsOrDrops(string raw, string? expected)
    {
        BookTaxonomy.CanonicalGenre(raw).ShouldBe(expected);
    }

    // --- Seviye uyum grupları (similar_books hard filtresi) ---

    [Fact]
    public void CompatibleAudienceLevels_ChildLevel_ReturnsChildGroupPlusBridgeAndUnknown()
    {
        var compatible = BookTaxonomy.CompatibleAudienceLevels("çocuk (7-10)");

        compatible.ShouldContain("çocuk (0-3)");
        compatible.ShouldContain("çocuk (4-6)");
        compatible.ShouldContain("çocuk (7-10)");
        compatible.ShouldContain("çocuk (10-12)");
        compatible.ShouldContain("genç yetişkin"); // köprü
        compatible.ShouldContain(BookTaxonomy.Unknown);
        compatible.ShouldNotContain("yetişkin-orta");
        compatible.ShouldNotContain("yetişkin-giriş");
    }

    [Fact]
    public void CompatibleAudienceLevels_AdultLevel_ReturnsAdultGroupPlusBridgeAndUnknown()
    {
        var compatible = BookTaxonomy.CompatibleAudienceLevels("yetişkin-giriş");

        compatible.ShouldContain("yetişkin-giriş");
        compatible.ShouldContain("yetişkin-orta");
        compatible.ShouldContain("yetişkin-ileri");
        compatible.ShouldContain("genç yetişkin"); // köprü
        compatible.ShouldContain(BookTaxonomy.Unknown);
        compatible.ShouldNotContain("çocuk (4-6)");
    }

    [Fact]
    public void CompatibleAudienceLevels_BridgeLevel_ReturnsAllLevels()
    {
        var compatible = BookTaxonomy.CompatibleAudienceLevels("genç yetişkin");

        compatible.Count.ShouldBe(BookTaxonomy.AudienceLevels.Count); // hepsiyle uyumlu
    }

    [Fact]
    public void CompatibleAudienceLevels_Unknown_ReturnsAllLevels()
    {
        var compatible = BookTaxonomy.CompatibleAudienceLevels(BookTaxonomy.Unknown);

        compatible.Count.ShouldBe(BookTaxonomy.AudienceLevels.Count);
    }
}