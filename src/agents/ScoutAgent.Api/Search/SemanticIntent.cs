namespace ScoutAgent.Api.Search;

// 026 US2 (İlke VI test-first): sorgu semantik yola mı gitsin? SAF. LLM betimleyici sorguyu
// `SemanticQuery`'ye koyar. Guard yalnız KİMLİK DEMİRİ varsa (yazar/yayınevi/benzer-çapa) semantiği
// İPTAL eder — bunlar "spesifik niyet" sinyali, anlamla eğilmez. `Text.Query` demir DEĞİL (LLM betimleyici
// sorguda konu kelimelerini oraya sızdırabilir — canlı gözlendi); semantik dalda SemanticQuery'ye katlanır
// (sinyal kaybı yok). Fiyat/sayfa/stok FİLTRESİ de iptal etmez (filtre+anlam US3). SearchTyped dalı çağırır.
public static class SemanticIntent
{
    public static bool IsSemantic(BookSearchFilters f) =>
        !string.IsNullOrWhiteSpace(f.SemanticQuery)
        && string.IsNullOrWhiteSpace(f.Text?.Author)
        && string.IsNullOrWhiteSpace(f.Publisher?.Include)
        && string.IsNullOrWhiteSpace(f.Publisher?.Exclude)
        && string.IsNullOrWhiteSpace(f.SimilarToTitle);
}
