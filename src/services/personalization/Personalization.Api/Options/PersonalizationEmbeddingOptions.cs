using System.ComponentModel.DataAnnotations;

namespace Personalization.Api.Options;

// 028: taste-vektör için OpenAI embedding yapılandırması. Model Catalog 025 ile AYNI olmalı
// (text-embedding-3-small 1536) — taste noktası kitap-vektörleriyle aynı uzayda, kıyaslanabilir.
// ApiKey AppHost secret (scout-openai-key reuse) — git'e girmez (İlke VIII).
public sealed class PersonalizationEmbeddingOptions
{
    [Required]
    public string ApiKey { get; set; } = default!;

    public string Model { get; set; } = "text-embedding-3-small";
}
