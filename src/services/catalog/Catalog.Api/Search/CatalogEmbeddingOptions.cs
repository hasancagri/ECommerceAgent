using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Search;

// 025 Dilim-1: embedding omurgası yapılandırması (tip'li Options — IConfiguration doğrudan okuma yasak).
// ApiKey AppHost secret parametresiyle gelir (scout-openai-key yeniden kullanılır) — git'e GİRMEZ, İlke VIII.
public sealed class CatalogEmbeddingOptions
{
    [Required]
    public string ApiKey { get; set; } = default!;

    // text-embedding-3-small = 1536-d, çok-dilli (Türkçe iş görür), ucuz. Boyut DDL ile hizalı (1536).
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimensions { get; set; } = 1536;

    // Backfill batch (tek OpenAI isteğindeki metin sayısı). 100 = token-limit'e güvenli + 22k/100≈220 istek.
    public int BackfillBatchSize { get; set; } = 100;

    // Worker yoklama aralığı (sn). Embed'i olmayan ürün kalmayınca boşta bekler.
    public int PollSeconds { get; set; } = 10;
}
