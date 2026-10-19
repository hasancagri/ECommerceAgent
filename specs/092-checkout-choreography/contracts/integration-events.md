# Phase 1 Contracts: Integration Events

Sözleşmeler `src/others/Shared/`'da yaşar. Yayıncı exchange deklare eder, **binding'i tüketici kurar**
(soğuk-açılış dersi). Tümü fanout (D3).

## Yeni event'ler (`Shared.IntegrationEvents`, additive)

```csharp
// Order → Stock. Checkout stok düşümü talebi (tek tüketici Stock, yine fanout).
public record StockCommitRequested(
    Guid OrderId,
    Guid UserId,
    IReadOnlyList<CheckoutItem> Items);   // CheckoutItem: (ProductId, Quantity, Name, UnitPrice) — korunur

// Stock → Order. Tüm kalemler atomik düştü.
public record StockCommitted(Guid OrderId);

// Stock → Order. İş hatası: bir kalem yetersiz, hiçbiri düşmedi.
public record StockCommitFailed(Guid OrderId, string ReasonCode);  // ReasonCode = STOCK_INSUFFICIENT (D4)
```

## Yeni fanout exchange sabitleri (`RabbitMqConstants`)

| Event | Exchange | Bağlayan tüketici (queue) |
|---|---|---|
| `StockCommitRequested` | `stock.commit-requested` | Stock (`stock.commit-requested`) |
| `StockCommitted` | `stock.committed` | Order (`order.stock-committed`) |
| `StockCommitFailed` | `stock.commit-failed` | Order (`order.stock-commit-failed`) |

Adlar kesinleştirilirken mevcut `RabbitMqConstants` deseni (`<topic>.<event>` exchange + `<consumer>.<event>`
queue) izlenir. `OrderCompleted` exchange MEVCUT (`order.completed`); Basket yeni queue `basket.order-completed`
bağlar (Reviews/Library binding'leri aynen durur).

## Mevcut, dokunulmaz

- `PaymentSucceeded` / `PaymentFailed` (Payment → fanout): değişmez. Order `PaymentSucceeded` tüketimi
  davranışı değişir (içerik), kontrat değişmez.
- `OrderCompleted` (Order → fanout, Reviews + Library): kontrat değişmez; yalnız **Basket binding'i eklenir**.

## Silinen sözleşmeler (`Shared.CheckoutMessages` + `RabbitMqConstants.Checkout`)

Record'lar: `StartCheckout`, `CommitStockCommand`, `StockCommitted`(eski reply şekli — SagaIdentity'li),
`RevertCommitStockCommand`, `StockCommitReverted`, `ConfirmOrderCommand`, `OrderConfirmed`,
`CancelOrderCommand`, `OrderCancelled`, `ClearBasketCommand`, `BasketCleared`.

> **Ad çakışması uyarısı**: eski `CheckoutMessages.StockCommitted` (reply, `[SagaIdentity] CheckoutId`,
> `Success`, `ErrorClass`) ile yeni `IntegrationEvents.StockCommitted(OrderId)` ADAŞ ama farklı şekil/namespace.
> Eski silinince karışıklık kalmaz; yeni olan sade (yalnız OrderId — başarı zaten event'in kendisi).

Queue sabitleri: `Checkout.StartQueue/OrderCommandsQueue/StockCommandsQueue/BasketCommandsQueue/RepliesQueue`.

Korunan ortak tipler: `CheckoutItem`, `OrderAddress` (Order hâlâ adres taşır), `ErrorClass` (başka kullanım
varsa kalır; yoksa silinir — tasks aşamasında grep'le doğrulanır).

## Wolverine kayıt (her tüketici için ZORUNLU)

```csharp
opts.Discovery.IncludeType(typeof(Order.Api.StockConsumers));
opts.Discovery.IncludeType(typeof(Stock.Api.OrderConsumers));
opts.Discovery.IncludeType(typeof(Basket.Api.OrderConsumers));
```
`*Consumers` çoğul ad-sonekiyle otomatik keşfedilmez; unutulursa event sessiz yutulur (hata/DLQ yok).
Elle publish'te AMQP `type` prefix'li tam ad olmalı (memory: wolverine-rabbitmq-wire-message-type).
