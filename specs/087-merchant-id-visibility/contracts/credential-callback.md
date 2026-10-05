# Kontrat: Credential Callback (PG → store)

Yön: PG (DropShop) → ECommerce Customer.Api. Dış-webhook (HTTP; İlke I sanksiyonlu istisna).
Sunucu: `MerchantRegistrationCallbackEndpointExtension` (YENİ) + `ReceiveMerchantCredentials` handler.
İmza: 077 `CallbackSignatureValidator` aynası (HMAC-SHA256(CallbackSecret, raw-body), constant-time).

## İstek

```
POST {CallbackBaseUrl}/internal/merchant-registration/callback
X-Signature: <hex(HMAC-SHA256(CallbackSecret, raw_body))>
Content-Type: application/json

{
  "correlationId": "<guid>",     # register'daki ile eşleşmeli
  "merchantId": "<guid>",        # PG onayda bastı
  "merchantKey": "<secret>",     # per-merchant sır
  "status": "Active"
}
```

## Yanıt

```
200 OK      # imza + correlation geçerli, persist edildi (veya idempotent no-op)
400         # imza geçersiz → nötr red, persist YOK
409/200     # bilinmeyen/çift correlation → idempotent (log, 200 ya da nötr)
```

## Doğrulama sırası (handler)

1. Raw body oku → `X-Signature` HMAC doğrula (`CallbackSecret`). Geçersiz → 400, hiçbir şey yapma.
2. `correlationId` → `MerchantInformation.PendingCorrelationId` ile eşle. Eşleşmezse nötr red.
3. `ApplyCredentialsFromCallback(correlationId, merchantId, merchantKey)` — idempotent (çift-callback no-op Ok).
4. persist (`[Transactional]`).

## Kurallar

- `merchantKey` persist edilir, **asla** log/trace/tool-dönüşüne yazılmaz.
- İmza doğrulanmadan gövde deserialize edilip kullanılmaz (önce HMAC).
- CallbackSecret ≠ BootstrapRegistrationKey ≠ MerchantKey.