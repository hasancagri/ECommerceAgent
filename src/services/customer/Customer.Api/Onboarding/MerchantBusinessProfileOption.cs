namespace Customer.Api.Onboarding;

// 087 FR-002: store-başlatır kayıtta PG'ye S2S gövdesinde giden first-party merchant iş/finansal künyesi.
// Hassas (TaxNumber/Iban) — secret store'da (dev: appsettings.Development/user-secrets, prod: vault, S5).
// MCP arg'ına/sohbete/loga HİÇ girmez; yalnız RegisterAsync gövdesinde taşınır. Tek first-party merchant
// → tek künye (çoklu-merchant YAGNI). Section "MerchantBusinessProfile".
public class MerchantBusinessProfileOption
{
    // Tüzel ünvan (kayıt başvurusu kimliği).
    public string LegalName { get; set; } = "";

    // Vergi numarası (hassas; yalnız S2S gövde).
    public string TaxNumber { get; set; } = "";

    // IBAN (hassas; yalnız S2S gövde, loglanmaz).
    public string Iban { get; set; } = "";
}