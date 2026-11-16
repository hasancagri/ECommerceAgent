namespace Catalog.Api.Search.Embedding;

// 011 US2/US3: sorgu-anı embedding (serbest-metin niyet → vektör). Ürün vektörleri OFFLINE üretilir
// (Python aracı); yalnız SORGU vektörü canlı-tek burada üretilir. Anahtar user-secrets'ta
// (OpenAiEmbeddingOptions:ApiKey) — İlke VIII: log'lanmaz, insan-yüzeye dönmez. Anahtar YOKSA
// semantik/hibrit ayak sessizce devre-dışı (BM25 tam çalışır) — Google-login koşullu deseni.
public sealed class OpenAiEmbeddingOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "text-embedding-3-small";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public const int Dimensions = 1536;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
