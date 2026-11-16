namespace Catalog.Api.Tests;

// 009 T002/T011 (test-first, İlke VI): BookTag value object — tek alan LOWERCASE normalized Value
// (orijinal saklanmaz). trim + whitespace-collapse + ToLowerInvariant. Türkçe ı/İ ToUpper'da
// katlanmadığından lowercase + ASCII ikiz (AsciiVariant) deseni: "karanlık" ↔ "karanlik".
// Değer-eşitliği kitaplar-arası gruplamanın temeli. Boş → Error (VALUE_EMPTY).
public class BookTagTests
{
    [Fact]
    public void Create_ValidRaw_NormalizesLowercase()
    {
        var result = BookTag.Create("  Gözetim ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Value.ShouldBe("gözetim");
    }

    [Fact]
    public void Create_CollapsesWhitespace()
    {
        var result = BookTag.Create("karanlık   gelecek");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Value.ShouldBe("karanlık gelecek");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyRaw_ReturnsError(string? raw)
    {
        var result = BookTag.Create(raw!);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.VALUE_EMPTY);
        result.Data.ShouldBeNull();
    }

    [Fact]
    public void Create_SameConceptDifferentCase_AreEqual()
    {
        var a = BookTag.Create("Distopya").Data!;
        var b = BookTag.Create("DISTOPYA").Data!;

        a.ShouldBe(b);                 // record value-equality — gruplamanın temeli
        a.Value.ShouldBe("distopya");
    }

    // --- T011: ASCII ikiz (Türkçe karakter köprüsü) ---

    [Fact]
    public void AsciiVariant_TurkishChars_ReturnsAsciiTwin()
    {
        var tag = BookTag.Create("karanlık").Data!;

        tag.AsciiVariant()!.Value.ShouldBe("karanlik");
    }

    [Fact]
    public void AsciiVariant_NoTurkishChars_ReturnsNull()
    {
        var tag = BookTag.Create("distopya").Data!;

        tag.AsciiVariant().ShouldBeNull();
    }
}
