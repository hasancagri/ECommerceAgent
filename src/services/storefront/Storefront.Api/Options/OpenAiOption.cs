namespace Storefront.Api.Options;

// embedding üretimi için OpenAI config — fail-fast (ApiKey eksikse ValidateOnStart açılışı durdurur;
// ChatAgent emsali). Embedding "agent" davranışı değildir: reasoning yok, düz deterministik API çağrısı.
public class OpenAiOption
{
    [Required] public string ApiKey { get; set; } = default!;

    // 1536 boyut. Model/boyut değişimi TÜM embedding'lerin yeniden üretimini gerektirir (bilinçli sabit).
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}