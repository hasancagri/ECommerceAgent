namespace Customer.Api.Onboarding;

// 087 FR-002: store-başlatır kayıtta PG'ye S2S gövdesinde giden first-party merchant iş/finansal künyesi.
// Hassas (TaxNumber/Iban) — secret store'da (dev: appsettings.Development/user-secrets, prod: vault, S5).
// MCP arg'ına/sohbete/loga HİÇ girmez; yalnız RegisterAsync gövdesinde taşınır. Tek first-party merchant
// → tek künye (çoklu-merchant YAGNI). Section "MerchantBusinessProfile". Alan seti PG RegisterRequest.Submit
// tam-KYC invariant'larını karşılar (PG değişmez — kontrat uzlaşı kararı 2026-10-05); Email tool arg'ından gelir.
public class MerchantBusinessProfileOption
{
    // İşyeri tipi — PG MerchantType enum değeri (1=Personal, 2=PrivateCompany, 3=LimitedOrJointStockCompany).
    public int Type { get; set; } = 3;

    // İşyeri/site adı (PG Name).
    public string Name { get; set; } = "";

    // Tüzel ünvan (PG LegalCompanyTitle; şirket tiplerinde zorunlu).
    public string LegalName { get; set; } = "";

    // Vergi numarası (hassas; yalnız S2S gövde).
    public string TaxNumber { get; set; } = "";

    // Vergi dairesi (şirket tiplerinde zorunlu).
    public string TaxOffice { get; set; } = "";

    // IBAN (hassas; yalnız S2S gövde, loglanmaz).
    public string Iban { get; set; } = "";

    // Telefon (PG GsmNumber; zorunlu).
    public string GsmNumber { get; set; } = "";

    // Adres (PG Address; zorunlu).
    public string Address { get; set; } = "";

    // Yetkili adı (PG ContactName; zorunlu).
    public string ContactName { get; set; } = "";

    // Yetkili soyadı (PG ContactSurname; zorunlu).
    public string ContactSurname { get; set; } = "";

    // Kimlik no (hassas; Personal/PrivateCompany'de zorunlu — şirkette opsiyonel).
    public string? IdentityNumber { get; set; }
}
