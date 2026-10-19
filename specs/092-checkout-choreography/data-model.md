# Phase 1 Data Model: Checkout Choreography

Yeni aggregate/tablo YOK. Mevcut aggregate'lerin checkout'a dokunan davranışları + silinen yüzey.

## Order (orderDb) — mevcut

Durum: `Pending → Confirmed | Cancelled`. Checkout'un fiili durum sahibi (saga Phase'i YERİNE geçer).

| Metot | Çağıran (yeni) | Davranış | Event |
|---|---|---|---|
| `Confirm()` | `StockConsumers.Handle(StockCommitted)` | `Pending→Confirmed`; `Status==Confirmed` ise idempotent no-op | `OrderCompleted` fanout yayar |
| `Cancel(reasonCode)` | `StockConsumers.Handle(StockCommitFailed)` + mevcut `PaymentConsumers.Handle(PaymentFailed)` | `Pending→Cancelled`; `Cancelled` ise no-op | — |

Değişmez: aggregate davranışı (İlke II). Yalnız çağıran süreç-glue'su saga→event consumer'a geçer.

## ProductStock (stockDb) — mevcut, budanır

| Metot | Durum | Not |
|---|---|---|
| `Commit(quantity, orderId)` | KALIR | orderId-idempotent (`_processedOps`: `"{orderId}:commit"`). Batch'te N aggregate için aynen çağrılır. Yetersizse `STOCK_INSUFFICIENT` Result (mutasyon yok). |
| `RevertCommit(quantity, orderId)` | **SİLİNİR** | Telafi kalmadı (atomik batch → başarısızlıkta commit yok). |

Silinen resource kodları: `STOCK_REVERT_INVALID`, `STOCK_REVERT_WITHOUT_COMMIT` (+ `"{orderId}:revert"` anahtarı
ve `RevertKey`). `STOCK_INSUFFICIENT` KALIR (StockCommitFailed taşır). `_processedOps` artık yalnız commit tutar.

**Batch atomiklik (handler seviyesi, aggregate değil):** `OrderConsumers.Handle(StockCommitRequested)`
tüm `ProductStock`'ları yükler → her birine `Commit` → biri hata verirse erken çık (store/SaveChanges yok)
→ `StockCommitFailed` yayar. Hepsi Ok ise tek `SaveChangesAsync` + `StockCommitted`. Marten tek tx → ya hep ya hiç.

## Basket (basketDb) — mevcut

| Aksiyon | Çağıran (yeni) | Davranış |
|---|---|---|
| sepet sil | `OrderConsumers.Handle(OrderCompleted)` | `session.Delete(basket)` idempotent (sepet yoksa da Ok) — mevcut desen |

## Silinen durum sahipleri

- **CheckoutProcess saga state** (checkoutDb): `Phase`, `NextIndex`, `CommittedItems`, `CancelReason`,
  `CompensationFailed`, `UserId/OrderId/Items` kopyası — tümü silinir. Süreç durumu artık Order durumunda +
  event akışında yaşar (ADR §3 "OrderStatus ≠ saga Phase" ayrımı: artık tek OrderStatus kalır).

## Idempotency özeti (at-least-once altında)

| Çift teslimat | Guard |
|---|---|
| `StockCommitRequested` ×2 | `ProductStock.Commit` orderId-anahtarlı → no-op |
| `StockCommitted` ×2 | `Order.Confirm` `Status==Confirmed` guard → yeniden yayın yok |
| `StockCommitFailed` ×2 | `Order.Cancel` `Cancelled` guard → no-op |
| `OrderCompleted` ×2 | Basket silme idempotent; Reviews/Library upsert idempotent (mevcut) |
