# Contract: PG→EC ödeme sonucu webhook (dış-taraf callback istisnası)

**Yön**: PG `StoreResultDelivery` → EC Payment.Api `POST /payments/callback`
**Hedef**: PG `StoreCallbackOptions.CallbackUrl` = `http://localhost:5080/payments/callback` → EC
Payment.Api bu yolu bu portta dinlemeli (dev koordinasyonu)
**Auth**: anonim + HMAC imza (JWT yok — dış-taraf). Kaynak: PG 090 StoreResultDelivery (birebir)

## Gövde (ham JSON, camelCase, anahtarlar SABİT)

```json
{
  "pgPaymentRef": "<Guid>",
  "orderRef": "<OrderId>",
  "status": "Paid",
  "amount": 100.50,
  "currency": "TRY",
  "bankCode": "MAVIBANK",
  "reasonCode": null
}
```

- `status` = `"Paid"` (başarı) | `"Failed"` (başarısızlık). İkisi de webhook atar.

## İmza

- Header `X-Signature` = **HMAC-SHA256(CallbackSecret, raw_body) → lowercase hex**.
- EC `CallbackSecret` = PG `StoreCallbackOptions.CallbackSecret` (aynı değer; dev
  `dev-sim-callback-secret-0909`).
- **Ham gövde baytları üzerinden doğrula** — JSON yeniden serialize ETME (PG raw_body'yi imzaladı;
  yeniden serialize boşluk/sıra değiştirir → imza tutmaz).

## EC işleme kuralı

1. İmza doğrula → başarısız **404 fail-closed** (durum değişmez, bilgi sızdırma).
2. `pgPaymentRef` ile PaymentIntent bul → yoksa 404 fail-closed.
3. `amount == PaymentIntent.Amount` guard → değilse reddet (anti-tamper).
4. `"Paid"` → `MarkSucceeded()` → `PaymentSucceeded` yayınla; `"Failed"` → `MarkFailed(reasonCode)` →
   `PaymentFailed` yayınla. `[Transactional]` (outbox).
5. İdempotent: MarkSucceeded/MarkFailed terminal'de no-op → retry güvenli → **200** dön (PG retry'ı durur).

## Yanıt

- `200` = kabul (idempotent tekrar dahil). `404` = fail-closed red (imza/eşleşme/tamper).
