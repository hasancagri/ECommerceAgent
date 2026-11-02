namespace Library.Api.Domains.PriceAlarms.Entities;


// FR-007: gonderim denemesinin kalici izi — davranissiz, append-only dokuman (aggregate DEGIL;
// ileriki "Bildirimlerim" ekraninin tohumu). NotificationSent event'inden yazilir.
public class NotificationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool Success { get; set; }

    // "sent" | "no-email" | kisa hata ozeti.
    public string Detail { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}