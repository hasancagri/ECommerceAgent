namespace Personalization.Api.Domains.Taste;

// 028 Dilim-4 (İlke VI test-first): taste-vektör saf hesap. Average = sinyal embedding'lerinin boyut-başı
// (ağırlıklı) ortalaması = kullanıcının embedding uzayındaki "zevk noktası". IsColdStart = sinyal eşiğin
// altında mı (altındaysa kişisel yerine küratörlü). MVP tek centroid, decay yok. Ağırlık: gelecekte
// satın-alma/yorum kitap-embed'i aramadan yüksek (weights ile; şimdi arama-only, eşit ağırlık).
public static class TasteVector
{
    public const int ColdStartThreshold = 3;

    public static float[] Average(IReadOnlyList<float[]> embeddings, IReadOnlyList<float>? weights = null)
    {
        if (embeddings.Count == 0) return [];
        var dim = embeddings[0].Length;
        var acc = new double[dim];
        double totalWeight = 0;
        for (var i = 0; i < embeddings.Count; i++)
        {
            var w = weights is not null && i < weights.Count ? weights[i] : 1f;
            var e = embeddings[i];
            for (var d = 0; d < dim; d++) acc[d] += e[d] * w;
            totalWeight += w;
        }
        if (totalWeight == 0) return new float[dim];
        var result = new float[dim];
        for (var d = 0; d < dim; d++) result[d] = (float)(acc[d] / totalWeight);
        return result;
    }

    public static bool IsColdStart(int signalCount, int threshold = ColdStartThreshold) =>
        signalCount < threshold;
}
