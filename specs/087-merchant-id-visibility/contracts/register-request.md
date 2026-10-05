# Kontrat: Merchant Kayıt İsteği (store → PG)

Yön: ECommerce Customer.Api → PG (DropShop). Sanksiyonlu dış-S2S (HTTP; dış PSP gRPC konuşmaz).
İstemci: `PgOnboardingClient.RegisterAsync` (EXTEND). Mevcut `OnboardingGatewayTokenHandler` m2m bearer taşır.

## İstek

```
POST {PG}/api/v1/onboarding/register
Authorization: Bearer <m2m token>          # taşıma kimliği (mevcut)
X-Registration-Key: <BootstrapRegistrationKey>   # kayıt-ucu yetkisi (ortak bootstrap sır)
Content-Type: application/json

{
  "correlationId": "<guid>",               # store üretir; callback'le eşleşir
  "callbackUrl": "<CallbackBaseUrl>/internal/merchant-registration/callback",
  "business": {                            # finansal dahil — yalnız S2S gövde, MCP arg DEĞİL
    "legalName": "string",
    "taxNumber": "string",
    "contactEmail": "string",
    "iban": "string"                        # hassas; gövdede, loglanmaz
  }
}
```

## Yanıt (senkron — yalnız kabul makbuzu, credential DEĞİL)

```
202 Accepted
{ "accepted": true, "correlationId": "<guid>", "status": "Pending" }
```

- Credential (MerchantId/Key) bu yanıtta DÖNMEZ — onay asenkron, callback'le gelir.
- 401/403: bootstrap key geçersiz → store fail-closed (kayıt başlamaz).
- 409: aynı correlation / zaten pending → idempotent kabul.

## Kurallar

- `iban` + `business.*` yalnız bu gövdede; MCP tool arg'ına, log/trace'e, tool dönüşüne girmez.
- `X-Registration-Key` yalnız kayıt ucunda; başka PG ucu kabul etmez (dar kapsam).