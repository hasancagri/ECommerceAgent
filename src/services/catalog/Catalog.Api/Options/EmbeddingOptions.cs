using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Options;

// 096: local embedding üretimi config'i — section "EmbeddingOptions". Ollama DIŞ bağımlılıktır
// (AppHost başlatmaz, R6); adres/model buradan okunur. Model değişimi toplu tazelemeyi tetikler
// (FR-006 — ProductEmbedding.Model karşılaştırması). Enabled=false = süreç hiç koşmaz (CI/prod supabı).
public class EmbeddingOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string BaseUrl { get; set; } = "http://localhost:11434";

    [Required]
    public string Model { get; set; } = "bge-m3";

    [Range(1, 1024)]
    public int BatchSize { get; set; } = 64;

    [Range(1, 86400)]
    public int IntervalSeconds { get; set; } = 60;

    // Compose kırpma sınırı — hash KIRPILMIŞ metinden (tutarlılık, R5).
    [Range(100, 100000)]
    public int MaxChars { get; set; } = 6000;
}
