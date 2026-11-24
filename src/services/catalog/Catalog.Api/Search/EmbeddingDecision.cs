namespace Catalog.Api.Search;

// 025 Dilim-1 (İlke VI test-first): bir ürünü yeniden embed etmeli mi? SAF. V1 sade (silme yok → Clear
// yok): embedding hiç yoksa (existingHash null) VEYA girdi metni değiştiyse (hash farkı) → üret. Aynı
// hash → atla (OpenAI çağrısı yok). Not: v1'de import INSERT-ONLY, worker yalnız "embedding yok"u yakalar;
// hash karşılaştırması ileride ProductChangedEvent tetiği açıldığında değişim-algılamayı hazır tutar.
public static class EmbeddingDecision
{
    public static bool ShouldGenerate(string newHash, string? existingHash) =>
        existingHash is null || !string.Equals(existingHash, newHash, StringComparison.Ordinal);
}
