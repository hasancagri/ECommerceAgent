using Catalog.Api.Domains.Products;
using Catalog.Api.Search;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

// 025 Dilim-1 (İlke VI test-first): embed-metni kurucu + hash + yeniden-embed kararı SAF birimler.
// Blob = Name + Description + Genre + Moods + Themes (hafif-etiketli). Fiyat/stok/dil/seviye GİRMEZ.
public class EmbeddingTextTests
{
    [Fact]
    public void Build_AllFields_ComposesLabeledBlob()
    {
        var text = EmbeddingText.Build(
            "Simyacı", "Bir çobanın hazine yolculuğu.",
            "roman", ["umutlu", "dingin"], ["yolculuk", "kader"]);

        text.ShouldBe(
            "Başlık: Simyacı\n" +
            "Tür: roman\n" +
            "Ruh hali: umutlu, dingin\n" +
            "Konular: yolculuk, kader\n" +
            "Açıklama: Bir çobanın hazine yolculuğu.");
    }

    [Fact]
    public void Build_UnknownGenre_OmitsGenreLine()
    {
        var text = EmbeddingText.Build("Kitap", "Açıklama", BookTaxonomy.Unknown, [], []);

        text.ShouldBe("Başlık: Kitap\nAçıklama: Açıklama");
    }

    [Fact]
    public void Build_EmptyMoodsAndThemes_OmitsThoseLines()
    {
        var text = EmbeddingText.Build("Kitap", "Açıklama", "roman", [], []);

        text.ShouldBe("Başlık: Kitap\nTür: roman\nAçıklama: Açıklama");
    }

    [Fact]
    public void Build_EmptyDescription_OmitsDescriptionLine()
    {
        var text = EmbeddingText.Build("Kitap", "", "roman", ["karanlık"], []);

        text.ShouldBe("Başlık: Kitap\nTür: roman\nRuh hali: karanlık");
    }

    [Fact]
    public void Build_TrimsValues()
    {
        var text = EmbeddingText.Build("  Kitap  ", "  Açıklama  ", "  roman  ", ["  karanlık  "], []);

        text.ShouldBe("Başlık: Kitap\nTür: roman\nRuh hali: karanlık\nAçıklama: Açıklama");
    }

    [Fact]
    public void Hash_SameText_SameHash()
    {
        EmbeddingText.Hash("abc").ShouldBe(EmbeddingText.Hash("abc"));
    }

    [Fact]
    public void Hash_DifferentText_DifferentHash()
    {
        EmbeddingText.Hash("abc").ShouldNotBe(EmbeddingText.Hash("abd"));
    }

    [Fact]
    public void Hash_IsHex64()
    {
        var h = EmbeddingText.Hash("herhangi bir metin");

        h.Length.ShouldBe(64);
        h.ShouldAllBe(c => "0123456789abcdef".Contains(c));
    }

    [Fact]
    public void ShouldGenerate_NoExistingHash_ReturnsTrue()
    {
        EmbeddingDecision.ShouldGenerate("yeni", null).ShouldBeTrue();
    }

    [Fact]
    public void ShouldGenerate_ChangedHash_ReturnsTrue()
    {
        EmbeddingDecision.ShouldGenerate("yeni", "eski").ShouldBeTrue();
    }

    [Fact]
    public void ShouldGenerate_SameHash_ReturnsFalse()
    {
        EmbeddingDecision.ShouldGenerate("ayni", "ayni").ShouldBeFalse();
    }
}
