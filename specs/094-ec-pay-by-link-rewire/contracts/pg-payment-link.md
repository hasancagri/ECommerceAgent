# Contract: EC→PG payment-link oluşturma (REST, dış-sağlayıcı)

**Yön**: EC Payment.Api (`PgPaymentLinkClient`) → PG `POST {PgBaseUrl}/payment-links`
**Auth**: header `X-Api-Key: {StoreApiKey}` (dev `dev-ec-pg-key`)
**Kaynak**: PG 089 CreatePaymentLink (E2E'de doğrulandı)

## İstek

```json
{
  "merchantId": "<Guid, config sabit MerchantId>",
  "amount": 100.50,
  "currency": "TRY",
  "orderRef": "<OrderId>",
  "returnUrl": "http://localhost:5080/checkout/done",
  "storeName": "<mağaza adı>",
  "orderSummary": "<kısa özet>"
}
```

## Yanıt (başarı 200)

```json
{ "linkUrl": "http://localhost:5201/payment-links/<openToken>", "pgPaymentRef": "<Guid>" }
```

→ `pgPaymentRef` PaymentIntent'e yazılır; `linkUrl` Ayşe'ye (HostedUrl) döner.

## Yanıt (mağaza satışa kapalı)

```json
{ "data": {"linkUrl":"","pgPaymentRef":""}, "isSuccess": false,
  "messages": [{ "code": "STORE_ONBOARDING_STORE_NOT_LIVE" }] }
```

→ client Result hatasına çevirir; start_payment "mağaza satışa kapalı" taşır (FR-011). Order/Intent
commit EDİLMEZ.
