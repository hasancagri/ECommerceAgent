using Catalog.Api.Helpers;

namespace Catalog.Api.Tests;

// 004 T007 (test-first, İlke VI): satır → künye eşlemesi saf birimi. ClosedXML'e dokunmaz —
// girdisi "sütun adı → ham hücre değeri" sözlüğü (research R2 tablosu).
public class ExcelRowMapperTests
{
    private static Dictionary<string, object?> ValidCells(Action<Dictionary<string, object?>>? mutate = null)
    {
        var cells = new Dictionary<string, object?>
        {
            ["Kitap Adı"] = "1984",
            ["Yazar"] = "George Orwell",
            ["ISBN"] = "9789750718533",
            ["Çevirmen"] = "Celâl Üster",
            ["Kategori"] = "Kitap > Edebiyat > Roman (Çeviri)",
            ["İlgili Kategoriler"] = "Kitap > Edebiyat > Roman (Çeviri)",
            ["Sayfa Sayısı"] = 352,
            ["Dil"] = "TÜRKÇE",
            ["Yayın Tarihi"] = "25.04.2019",
            ["Cilt Tipi"] = "Karton Kapak",
            ["Liste Fiyatı (₺)"] = 195.0,
            ["Kitapyurdu Fiyatı (₺)"] = 141.38,
            ["Puan"] = 5.0,
            ["Puan Adedi"] = 19494,
            ["Yorum Sayısı"] = 8388,
            ["Satın Alma Adedi"] = 201805,
            ["Okuyacağım"] = 10,
            ["Okuyorum"] = 2,
            ["Okudum"] = 7,
            ["Favori"] = 3,
            ["Kapak URL"] = "https://img.kitapyurdu.com/v1/getImage/fn:1",
            ["Açıklama"] = "Distopya klasiği.",
            ["Yazar Açıklaması"] = "George Orwell: 1903 doğumlu.",
        };
        mutate?.Invoke(cells);
        return cells;
    }

    [Fact]
    public void Map_ValidRow_FillsAllMappedFields()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells());

        result.Error.ShouldBeNull();
        var row = result.Row!;
        row.Name.ShouldBe("1984");
        row.Isbn.ShouldBe("9789750718533");
        row.AuthorNames.ShouldBe(new[] { "George Orwell" });
        row.Translators.ShouldBe(new[] { "Celâl Üster" });
        row.ParentCategoryName.ShouldBe("Edebiyat");
        row.LeafCategoryName.ShouldBe("Roman (Çeviri)");
        row.PageCount.ShouldBe(352);
        row.Language.ShouldBe("TÜRKÇE");
        row.PublishedOn.ShouldBe(new DateOnly(2019, 4, 25));
        row.Binding.ShouldBe(Catalog.Api.Domains.Products.BindingType.KartonKapak);
        row.Price.ShouldBe(195.0m);
        row.Rating.ShouldBe(5);
        row.PurchaseCount.ShouldBe(201805);
        row.CoverUrl.ShouldBe("https://img.kitapyurdu.com/v1/getImage/fn:1");
        row.Description.ShouldBe("Distopya klasiği.");
        row.AuthorBio.ShouldBe("George Orwell: 1903 doğumlu.");
        row.PublisherName.ShouldBe("Can Yayınları"); // sayfa adı = yayınevi (R1)
    }

    [Fact]
    public void Map_MissingIsbn_ReturnsRowError()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 5, ValidCells(c => c["ISBN"] = "  "));

        result.Row.ShouldBeNull();
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    public void Map_MissingName_ReturnsRowError()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 6, ValidCells(c => c["Kitap Adı"] = null));

        result.Row.ShouldBeNull();
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    public void Map_ListPriceMissing_FallsBackToKitapyurduPrice()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c => c["Liste Fiyatı (₺)"] = null));

        result.Row!.Price.ShouldBe(141.38m);
    }

    [Fact]
    public void Map_BothPricesMissing_ProducesPricelessRow()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c =>
        {
            c["Liste Fiyatı (₺)"] = null;
            c["Kitapyurdu Fiyatı (₺)"] = null;
        }));

        result.Row!.Price.ShouldBeNull();
    }

    [Fact]
    public void Map_ZeroPrice_TreatedAsPriceless()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c =>
        {
            c["Liste Fiyatı (₺)"] = 0.0;
            c["Kitapyurdu Fiyatı (₺)"] = 0.0;
        }));

        result.Row!.Price.ShouldBeNull();
    }

    [Fact]
    public void Map_MultipleAuthors_SplitsOnCommaAndDedupes()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2,
            ValidCells(c => c["Yazar"] = "George Orwell, Aldous Huxley, George Orwell"));

        result.Row!.AuthorNames.ShouldBe(new[] { "George Orwell", "Aldous Huxley" });
    }

    [Fact]
    public void Map_SingleLevelCategory_LeafWithoutParent()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c => c["Kategori"] = "Kitap > Edebiyat"));

        result.Row!.ParentCategoryName.ShouldBeNull();
        result.Row.LeafCategoryName.ShouldBe("Edebiyat");
    }

    [Fact]
    public void Map_EmptyCategory_LeafNull()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c => c["Kategori"] = null));

        result.Row!.LeafCategoryName.ShouldBeNull(); // süreç "Genel" fallback'ine düşürür
    }

    [Fact]
    public void Map_InvalidDate_PublishedOnNull()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c => c["Yayın Tarihi"] = "bilinmiyor"));

        result.Row!.PublishedOn.ShouldBeNull();
        result.Error.ShouldBeNull(); // tarih bozuk diye satır ölmez
    }

    [Fact]
    public void Map_UnknownBinding_FallsBackToUnknown()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c => c["Cilt Tipi"] = "Deri Cilt"));

        result.Row!.Binding.ShouldBe(Catalog.Api.Domains.Products.BindingType.Unknown);
    }

    [Fact]
    public void Map_FractionalRating_RoundsToNearestInt()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c => c["Puan"] = 4.6));

        result.Row!.Rating.ShouldBe(5);
    }

    [Fact]
    public void Map_MissingPopularityCells_DefaultToZeroAndNullRating()
    {
        var result = ExcelRowMapper.Map("Can Yayınları", 2, ValidCells(c =>
        {
            c["Puan"] = null;
            c["Puan Adedi"] = null;
            c["Satın Alma Adedi"] = null;
            c["Okuyacağım"] = null;
            c["Okuyorum"] = null;
            c["Okudum"] = null;
            c["Favori"] = null;
        }));

        var row = result.Row!;
        row.Rating.ShouldBeNull();
        row.RatingCount.ShouldBe(0);
        row.PurchaseCount.ShouldBe(0);
        row.ToReadCount.ShouldBe(0);
    }
}
