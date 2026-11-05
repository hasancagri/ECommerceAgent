using System.ComponentModel.DataAnnotations;

namespace Gateway.Options;

// 004 Dilim-1 kenar yetkisi: admin rota sınıfı X-Admin-Api-Key başlığıyla korunur.
// GEÇİCİ düzen — Dilim 2'de yerel kimlik JWT'si bu noktaya oturur, anahtar katmanı sökülür (FR-016).
// Anahtar DEĞERİ hiçbir log/yanıt/ekrana yazılmaz (Anayasa İlke VIII).
public class GatewayAuthOptions
{
    [Required(AllowEmptyStrings = false)]
    public string AdminApiKey { get; set; } = default!;
}
