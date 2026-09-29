using System.ComponentModel.DataAnnotations;

namespace Checkout.Orchestrator.Options;

// checkout süreç ayarları (watchdog + retry). House-style: config section → tip'li POCO.
public class CheckoutOptions
{
    // Sürecin tamamı için watchdog süresi (saniye); yanıt gelmezse faz'a göre telafi/no-op.
    [Range(1, 3600)] public int WatchdogSeconds { get; set; } = 120;

    // Adım başına geçici hata retry üst sınırı.
    [Range(0, 10)] public int MaxAttempts { get; set; } = 3;

    // Retry gecikmesi (saniye).
    [Range(1, 300)] public int RetryDelaySeconds { get; set; } = 5;
}