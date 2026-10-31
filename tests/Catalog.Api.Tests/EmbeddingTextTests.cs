using Catalog.Api.Embeddings;

namespace Catalog.Api.Tests;

// 096 T005: saf compose/kırpma/hash çekirdeği (İlke VI test-first). Satır formatı data-model.md'de:
// {Name} / "Yazarlar: a, b" / "Kategori: Leaf > Parent" / {Description}; boş parça satır üretmez;
// MaxChars aşımında sondan kırpılır; hash KIRPILMIŞ metinden.
public class EmbeddingTextTests
{
    private const int MaxChars = 6000;

    [Fact]
    public void Compose_FullMetadata_ProducesExpectedLines()
    {
        var text = EmbeddingText.Compose(
            "Kürk Mantolu Madonna",
            "Raif Efendi'nin hikâyesi.",
            ["Sabahattin Ali"],
            ["Roman", "Edebiyat"],
            MaxChars);

        text.ShouldBe(
            "Kürk Mantolu Madonna\n" +
            "Yazarlar: Sabahattin Ali\n" +
            "Kategori: Roman > Edebiyat\n" +
            "Raif Efendi'nin hikâyesi.");
    }

    [Fact]
    public void Compose_MultipleAuthors_JoinsWithComma()
    {
        var text = EmbeddingText.Compose("Ad", "", ["Yazar Bir", "Yazar İki"], [], MaxChars);

        text.ShouldContain("Yazarlar: Yazar Bir, Yazar İki");
    }

    [Fact]
    public void Compose_EmptyParts_ProduceNoLines()
    {
        var text = EmbeddingText.Compose("Sadece Ad", "", [], [], MaxChars);

        text.ShouldBe("Sadece Ad");
    }

    [Fact]
    public void Compose_WhitespaceDescription_ProducesNoLine()
    {
        var text = EmbeddingText.Compose("Ad", "   ", ["Yazar"], [], MaxChars);

        text.ShouldBe("Ad\nYazarlar: Yazar");
    }

    [Fact]
    public void Compose_OverMaxChars_TruncatesFromEnd()
    {
        var longDescription = new string('x', 10000);

        var text = EmbeddingText.Compose("Ad", longDescription, [], [], MaxChars);

        text.Length.ShouldBe(MaxChars);
        text.ShouldStartWith("Ad\n");
    }

    [Fact]
    public void Hash_TruncatedText_HashesTruncatedNotOriginal()
    {
        var longDescription = new string('x', 10000);
        var truncated = EmbeddingText.Compose("Ad", longDescription, [], [], MaxChars);

        var hash = EmbeddingText.Hash(truncated);

        // Aynı kırpılmış metin → aynı hash (hash compose çıktısından, kaynak metinden değil).
        hash.ShouldBe(EmbeddingText.Hash(truncated[..MaxChars]));
    }

    [Fact]
    public void Hash_SameInput_SameHash()
    {
        EmbeddingText.Hash("abc").ShouldBe(EmbeddingText.Hash("abc"));
    }

    [Fact]
    public void Hash_DifferentInput_DifferentHash()
    {
        EmbeddingText.Hash("abc").ShouldNotBe(EmbeddingText.Hash("abd"));
    }
}
