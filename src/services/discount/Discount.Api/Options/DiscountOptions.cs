namespace Discount.Api.Options;

// Discount BC yapılandırması. IConfiguration'dan doğrudan okuma YASAK → tip'li POCO
// (BindConfiguration + ValidateOnStart). Şimdilik dar; scheduling/telafi ayarları buraya eklenir.
public class DiscountOptions
{
    // İleride: geç-fire telafi penceresi vb. v1'de scheduling saf ScheduleAsync ile, ek ayar yok.
}
