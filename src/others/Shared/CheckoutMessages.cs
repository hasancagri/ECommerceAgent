using Wolverine.Persistence.Sagas;

namespace Shared;

// Checkout orchestrator broker komut/yanıt sözleşmeleri (hedefli async; fanout DEĞİL).
// İlke I v1.11.0 sanctioned kanal. Düz record'lar — Wolverine [SagaIdentity] orchestrator'da uygulanır
// (Shared Wolverine'e bağımlı değil). Envelope: komut = CheckoutId + IdempotencyKey; yanıt = CheckoutId
// + Success + ErrorClass. Additive alanlar default'lu eklenir (eski tüketici kırılmaz).
public static class CheckoutMessages
{
    // Geçici (retry edilebilir) vs kalıcı (telafi/iptal gerektiren) hata ayrımı (FR-025).
    public enum ErrorClass { None = 0, Transient = 1, Permanent = 2 }

    // PaymentMode + Charge yolu SÖKÜLDÜ. Ödeme her zaman ÖNCEDEN çekildi (hosted-CF callback başarılı
    // → Order.Api StartCheckout yayınlar) → saga charge ATLAR, sipariş ZATEN oluşturulmuştur (OrderId dolu).

    // Kalem: stok commit ProductId+Quantity kullanır; Order ayrıca Name+UnitPrice ister (varsayılanlı —
    // lean kullanım kırılmaz).
    public record CheckoutItem(Guid ProductId, int Quantity, string Name = "", decimal UnitPrice = 0);

    // Sipariş adresi (Order aggregate Address VO'suna map'lenir; BC izolasyonu — düz veri taşınır).
    public record OrderAddress(string Province, string District, string Street, string ZipCode, string Line);

    // Giriş: Order.Api (hosted-CF ödeme başarılı) StartCheckout'u yayınlar; orchestrator dinler → saga doğar.
    // CheckoutId = saga kimliği ([SagaIdentity]). 077: ödeme öncedendir → sipariş OrderId dolu gelir.
    public record StartCheckout(
        [property: SagaIdentity] Guid CheckoutId,
        Guid UserId,
        IReadOnlyList<CheckoutItem> Items,
        decimal Amount,
        OrderAddress Address,
        Guid OrderId);

    // --- Adım komutları (orchestrator → hedef BC) + yanıt-event'leri (→ orchestrator) ---
    // Komutlar BC handler'ında düz tüketilir (saga değil) → SagaIdentity yok. Yanıtlar saga'ya döner
    // → CheckoutId [SagaIdentity].

    public record CreateOrderCommand(Guid CheckoutId, Guid UserId, IReadOnlyList<CheckoutItem> Items, decimal Amount, OrderAddress Address, string CardRef, string IdempotencyKey);
    public record OrderCreated([property: SagaIdentity] Guid CheckoutId, Guid OrderId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    public record CommitStockCommand(Guid CheckoutId, Guid OrderId, Guid ProductId, Guid UserId, int Quantity, string IdempotencyKey);
    public record StockCommitted([property: SagaIdentity] Guid CheckoutId, Guid ProductId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    // ChargePaymentCommand + PaymentCharged SÖKÜLDÜ (ödeme hosted-CF ile öncedendir; saga charge çekmez).

    public record ConfirmOrderCommand(Guid CheckoutId, Guid OrderId, string IdempotencyKey);
    public record OrderConfirmed([property: SagaIdentity] Guid CheckoutId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    public record ClearBasketCommand(Guid CheckoutId, Guid UserId, string IdempotencyKey);
    public record BasketCleared([property: SagaIdentity] Guid CheckoutId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    // --- Telafi komutları (yalnız pivot öncesi; LIFO) ---

    public record RevertCommitStockCommand(Guid CheckoutId, Guid OrderId, Guid ProductId, Guid UserId, int Quantity, string IdempotencyKey);
    public record StockCommitReverted([property: SagaIdentity] Guid CheckoutId, Guid ProductId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);

    public record CancelOrderCommand(Guid CheckoutId, Guid OrderId, string ReasonCode, string IdempotencyKey);
    public record OrderCancelled([property: SagaIdentity] Guid CheckoutId, bool Success, ErrorClass ErrorClass, string? MessageCode = null);
}