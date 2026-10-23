using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 094: EC→PG pay-by-link yapılandırması. IConfiguration doğrudan okuma YASAK → tip'li POCO
// (BindConfiguration + ValidateDataAnnotations + ValidateOnStart). BaseUrl + MerchantId non-secret
// (appsettings); StoreApiKey + CallbackSecret SIR (user-secrets, İlke VIII — log/trace/dönüşe girmez).
// Sabit localhost base (cross-host service-discovery DEĞİL — EC/PG ayrı Aspire host).
public class PgPaymentLinkOptions
{
    // PG Payment.Api taban adresi (ör. http://localhost:5201).
    [Required] public string BaseUrl { get; set; } = string.Empty;

    // PG'nin EC'den beklediği paylaşılan API key (X-Api-Key). SIR.
    [Required] public string StoreApiKey { get; set; } = string.Empty;

    // PG→EC webhook HMAC doğrulaması için paylaşılan sır (PG StoreCallbackOptions.CallbackSecret ile aynı). SIR.
    [Required] public string CallbackSecret { get; set; } = string.Empty;

    // Bu mağazanın PG'deki merchant kimliği (Live merchant). PG link isteğinde gider.
    [Required] public Guid MerchantId { get; set; }

    // PG hosted sayfada gösterilecek mağaza adı.
    [Required] public string StoreName { get; set; } = string.Empty;

    // PG'nin ödeme sonrası tarayıcıyı döndüreceği EC ucu (GET /checkout/done). Durum webhook'tan gelir.
    [Required] public string ReturnUrl { get; set; } = string.Empty;
}
