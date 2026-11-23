namespace Catalog.Api.Tests;

// 021 T013 (test-first, İlke VI): WorkKey (eser kimliği) üretimi — aynı eserin farklı baskıları
// (farklı ISBN, cilt, çevirmen) TEK anahtara iner; benzerlik kendi baskılarını tekilleştirir.
// Üretim: normalize(sıralı yazarlar) + "|" + normalize(parantezsiz başlık). Türkçe fold → ASCII,
// lowercase invariant, çoklu boşluk tek.
public class WorkKeyNormalizerTests
{
    [Fact]
    public void Derive_SingleAuthorAndTitle_ProducesLowercaseAsciiKey()
    {
        var key = WorkKeyNormalizer.Derive(new[] { "Dostoyevski" }, "Suç ve Ceza");
        key.ShouldBe("dostoyevski|suc ve ceza");
    }

    [Fact]
    public void Derive_TitleWithParenthesesSuffix_StripsParentheses()
    {
        // "Suç ve Ceza" ve "Suç ve Ceza (Ciltli)" AYNI esere inmeli.
        var plain = WorkKeyNormalizer.Derive(new[] { "Dostoyevski" }, "Suç ve Ceza");
        var bound = WorkKeyNormalizer.Derive(new[] { "Dostoyevski" }, "Suç ve Ceza (Ciltli)");
        bound.ShouldBe(plain);
    }

    [Fact]
    public void Derive_MultipleParentheses_StripsAll()
    {
        var key = WorkKeyNormalizer.Derive(new[] { "Tolstoy" }, "Savaş ve Barış (Özel Baskı) (Ciltli)");
        key.ShouldBe("tolstoy|savas ve baris");
    }

    [Fact]
    public void Derive_AuthorOrderDiffers_ProducesSameKey()
    {
        // Yazar sırası baskıya göre değişse de eser aynı → sıralama determinizmi.
        var a = WorkKeyNormalizer.Derive(new[] { "Ahmet", "Zeynep" }, "Ortak Eser");
        var b = WorkKeyNormalizer.Derive(new[] { "Zeynep", "Ahmet" }, "Ortak Eser");
        a.ShouldBe(b);
        a.ShouldBe("ahmet,zeynep|ortak eser");
    }

    [Fact]
    public void Derive_TurkishCharacters_FoldsToAscii()
    {
        var key = WorkKeyNormalizer.Derive(new[] { "Çağrı Gür" }, "İçimizdeki Şeytan");
        key.ShouldBe("cagri gur|icimizdeki seytan");
    }

    [Fact]
    public void Derive_ExtraWhitespace_CollapsesToSingleSpace()
    {
        var key = WorkKeyNormalizer.Derive(new[] { "  George   Orwell " }, "  Bin Dokuz Yüz   Seksen Dört ");
        key.ShouldBe("george orwell|bin dokuz yuz seksen dort");
    }

    [Fact]
    public void Derive_NoAuthors_ProducesTitleOnlyKey()
    {
        var key = WorkKeyNormalizer.Derive(Array.Empty<string>(), "Anonim Eser");
        key.ShouldBe("|anonim eser");
    }

    [Fact]
    public void Derive_DuplicateAuthors_Dedupes()
    {
        var key = WorkKeyNormalizer.Derive(new[] { "Orwell", "Orwell" }, "1984");
        key.ShouldBe("orwell|1984");
    }

    [Fact]
    public void Derive_BlankAuthorEntries_Ignored()
    {
        var key = WorkKeyNormalizer.Derive(new[] { "Orwell", "  ", "" }, "1984");
        key.ShouldBe("orwell|1984");
    }
}
