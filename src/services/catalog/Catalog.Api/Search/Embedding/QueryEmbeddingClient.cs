using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Catalog.Api.Search.Embedding;

// Sorgu metnini OpenAI embeddings API'siyle 1536-boyut vektöre çevirir (text-embedding-3-small —
// ürün vektörleriyle AYNI model/boyut, yoksa kosinüs anlamsız). Anahtar yoksa / hata olursa null
// döner (semantik ayak devre-dışı; çağıran BM25'e düşer ya da boş sonuç verir). İlke VIII: anahtar log'lanmaz.
public sealed class QueryEmbeddingClient(
    IHttpClientFactory httpClientFactory,
    OpenAiEmbeddingOptions options,
    ILogger<QueryEmbeddingClient> logger) : ISingletonDependency
{
    public bool IsEnabled => options.IsConfigured;

    public async Task<float[]?> EmbedAsync(string text, CancellationToken ct)
    {
        if (!options.IsConfigured) return null;

        try
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

            var payload = JsonSerializer.Serialize(new { model = options.Model, input = text });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("embeddings", content, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var array = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");

            var vector = new float[array.GetArrayLength()];
            var i = 0;
            foreach (var el in array.EnumerateArray())
                vector[i++] = el.GetSingle();
            return vector.Length == OpenAiEmbeddingOptions.Dimensions ? vector : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "011 sorgu embedding üretilemedi — semantik ayak atlandı.");
            return null;
        }
    }
}
