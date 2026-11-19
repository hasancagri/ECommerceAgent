using Catalog.Api.Search;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

// 018 (İlke VI test-first): BM25 terim çıkarma saf fonksiyon. Özel karakter/apostrof temizlenir,
// ≤2 harf + Türkçe dolgu/soru kelimeleri elenir (doğal-cümle sorgu BM25'i kirletmesin — FR-009).
public class Bm25TermSanitizeTests
{
    [Fact]
    public void Extract_PlainWords_ReturnsTerms()
    {
        var terms = Bm25Terms.Extract("George Orwell");
        terms.ShouldBe(new[] { "George", "Orwell" });
    }

    [Fact]
    public void Extract_ApostropheAndSpecials_AreStripped()
    {
        var terms = Bm25Terms.Extract("Dostoyevski'nin kitabı!");
        terms.ShouldContain("Dostoyevski");
        terms.ShouldNotContain("kitabı");          // stopword elendi
        terms.ShouldAllBe(t => !t.Contains('\''));  // apostrof yok
        terms.ShouldAllBe(t => !t.Contains('!'));   // özel karakter yok
    }

    [Fact]
    public void Extract_FillerAndQuestionWords_ReturnEmpty()
    {
        var terms = Bm25Terms.Extract("bana bir kitap misin");
        terms.ShouldBeEmpty();
    }

    [Fact]
    public void Extract_ShortTerms_AreDropped()
    {
        // ≤2 harf elenir (TR ekleri "in/ın" gibi gürültü).
        var terms = Bm25Terms.Extract("ab cd Orwell");
        terms.ShouldBe(new[] { "Orwell" });
    }

    [Fact]
    public void Extract_BlankOrNonWord_ReturnsEmpty()
    {
        Bm25Terms.Extract("   ").ShouldBeEmpty();
        Bm25Terms.Extract("!!! ??? ...").ShouldBeEmpty();
    }
}
