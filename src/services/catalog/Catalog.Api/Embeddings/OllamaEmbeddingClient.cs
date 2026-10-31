using System.Net.Http.Json;
using Catalog.Api.Options;

namespace Catalog.Api.Embeddings;

// 096: Ollama embed API istemcisi (contracts/ollama-embed-api.md) — SDK'sız düz typed HttpClient
// (R4, kullanıcı tercihi: dolaylama yerine düz kod). Batch native: input dizisi ↔ embeddings dizisi
// sıra eşleşmeli. Hata/uyumsuzluk → istisna; süreç loglayıp batch'i sonraki döngüye bırakır
// (fail-soft FR-005 — yarım batch yazılmaz). Localhost, kimlik/sır taşımaz (İlke VIII N/A).
public class OllamaEmbeddingClient(HttpClient httpClient, EmbeddingOptions options)
{
    private record EmbedRequest(string Model, string[] Input);

    private record EmbedResponse(string? Model, float[][]? Embeddings);

    /// <summary>Metin batch'ini vektörleştirir. Yanıt sayısı/boyutu doğrulanır — uyuşmazsa batch reddi
    /// (InvalidOperationException). 404 = model çekilmemiş; çağıran "ollama pull {Model}" ipucuyla loglar.</summary>
    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/embed", new EmbedRequest(options.Model, texts.ToArray()), ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<EmbedResponse>(ct);
        var embeddings = body?.Embeddings
            ?? throw new InvalidOperationException("Ollama yanıtında embeddings alanı yok.");

        if (embeddings.Length != texts.Count)
            throw new InvalidOperationException(
                $"Ollama yanıt sayısı uyuşmuyor: {texts.Count} istek, {embeddings.Length} vektör.");

        var dimension = embeddings[0].Length;
        if (dimension == 0 || embeddings.Any(v => v.Length != dimension))
            throw new InvalidOperationException("Ollama yanıtında boş/tutarsız boyutlu vektör var.");

        return embeddings;
    }
}
