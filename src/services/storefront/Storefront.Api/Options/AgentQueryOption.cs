namespace Storefront.Api.Options;

// serbest-sorgu kapısının (query_storefront) tavan + kısıtlı-rol ayarları. RolePassword
// user-secrets/env'den gelir (appsettings'e YAZILMAZ); fail-fast ValidateOnStart.
public class AgentQueryOption
{
    // Sarmalanmış LIMIT tavanı (R4): her sorgu MaxRows+1 ile sarılır, +1 döndüyse Truncated=true.
    [Range(1, 500)] public int MaxRows { get; set; } = 50;

    // SET LOCAL statement_timeout (saniye) — uzun-süren sorgu zırhı (FR-004).
    [Range(1, 30)] public int TimeoutSeconds { get; set; } = 3;

    // Bekçi uzunluk tavanı (AgentSqlTooLong).
    [Range(100, 20000)] public int MaxSqlLength { get; set; } = 4000;

    // Kısıtlı DB rolü: TEK yetkisi storefront_sellable view SELECT'i (FR-005 yapısal güvence).
    [Required] public string RoleName { get; set; } = "storefront_agent_ro";
    [Required] public string RolePassword { get; set; } = null!;
}
