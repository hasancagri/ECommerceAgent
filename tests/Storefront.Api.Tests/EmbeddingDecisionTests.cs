namespace Storefront.Api.Tests;

// 086 (067'den taşındı) İLKE VI: yeniden-embedding kararı — boş→Clear, değişti→Generate,
// aynı+var→Keep, aynı+yok→Generate. Projeksiyon önceki açıklama/vektörü ES'teki mevcut doc'tan okur.
public class EmbeddingDecisionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Yeni_aciklama_bos_ise_temsil_temizlenir(string? newDescription)
    {
        StorefrontDocument.DecideEmbedding(newDescription, "eski açıklama", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Clear);
    }

    [Fact]
    public void Aciklama_degismedi_ve_temsil_var_ise_dokunulmaz()
    {
        StorefrontDocument.DecideEmbedding("aynı metin", "aynı metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Keep);
    }

    [Fact]
    public void Aciklama_degismedi_ama_temsil_yok_ise_uretilir()
    {
        StorefrontDocument.DecideEmbedding("aynı metin", "aynı metin", hasEmbedding: false)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Aciklama_degisti_ise_temsil_yeniden_uretilir()
    {
        StorefrontDocument.DecideEmbedding("yeni metin", "eski metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Aciklama_bostan_doluya_gecince_uretilir()
    {
        StorefrontDocument.DecideEmbedding("yeni gelen açıklama", null, hasEmbedding: false)
            .ShouldBe(EmbeddingDecision.Generate);
    }

    [Fact]
    public void Karar_ordinal_case_duyarli()
    {
        StorefrontDocument.DecideEmbedding("Metin", "metin", hasEmbedding: true)
            .ShouldBe(EmbeddingDecision.Generate);
    }
}