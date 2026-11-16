# Contract: UserSearched Integration Event

## Tanım (Shared.IntegrationEvents)

```
record UserSearched(Guid UserId, string Query, string Mode, int ResultCount, DateTimeOffset SearchedAt)
```

- `RabbitMqConstants.UserSearched.Exchange` = fanout.
- `RabbitMqConstants.UserSearched.Queues.Personalization` = tüketici kuyruğu.

## Yayıncı (BFF)

- Exchange deklare (fanout) + `PublishMessage<UserSearched>().ToRabbitExchange(UserSearched.Exchange)`.
- YALNIZ login aramada yayınlanır (userId dolu).
- BFF durumsuz → outbox YOK; publish RabbitMQ durability'ye dayanır (FLAG: nadir publish-kaybı kabul, düşük-önem sinyal).

## Tüketici (Personalization)

- Exchange deklare + `BindQueue(Queues.Personalization)` + `ListenToRabbitQueue(Queues.Personalization)` — **binding'i tüketici kurar** (soğuk-açılış dersi, 007).
- Handler `UserSearched` → `UserSearchRecord.Create` → append. Hata → uniform retry merdiveni → error-queue (DLQ, 088).
- Handler adı `*Consumer(s)` değilse `opts.Discovery.IncludeType(...)` ZORUNLU (Wolverine keşif tuzağı).

## Değişmezler

- Additive alan default'lu (eski tüketici kırılmaz). Fanout → ileride ek tüketici bedava.
