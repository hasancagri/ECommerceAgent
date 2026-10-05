# Data Model: MerchantId Görünürlük (ECommerce bacağı)

Phase 1. customerDb (Marten). Yalnız değişen/yeni yapılar; mevcut alanlar kısaca.

## MerchantInformation (aggregate — EXTEND)

Mevcut: `MerchantId` (Guid), `MerchantKey` (string, sır), `CredentialsVerified` (bool) + audit (AggregateRoot).

| Alan | Tip | Not |
|---|---|---|
| MerchantId | Guid | PG basar; insan-yüzeyde render YOK |
| MerchantKey | string | sır; yalnız sunucu + gRPC; log/dönüş YASAK |
| CredentialsVerified | bool | callback geldiğinde true |
| PendingCorrelationId | Guid? | **YENİ** — aktif kayıt çağrısının correlation'ı; callback eşleme + idempotency |
| RegistrationStatus | enum | **YENİ** — `None`/`Pending`/`Active`; callback öncesi `Pending`, geldiğinde `Active` |

**Davranış (test-first, İlke VI):**
- `StartRegistration(correlationId)` → `RegistrationStatus=Pending`, `PendingCorrelationId=correlationId`. Guard: zaten `Pending` + farklı correlation varsa reddet (tek aktif kayıt).
- `ApplyCredentialsFromCallback(correlationId, merchantId, merchantKey)` → correlation eşleşmezse `ResultDomain.Error` (nötr red). Eşleşirse: `MerchantId/MerchantKey` set, `CredentialsVerified=true`, `RegistrationStatus=Active`, `PendingCorrelationId=null`. **İdempotent:** zaten Active + aynı correlation → no-op Ok (çift-callback yutulur).
- Mevcut `Create`/`UpdateKey`: `UpdateKey` reissue için KEEP ama callback yolundan çağrılır (elle-giriş yolu silinir).

**Enum (aggregate dosyasında, İlke II/CLAUDE.md):** `RegistrationStatus { None, Pending, Active }`.

## Secrets (Options — DropShopOnboardingOption EXTEND)

`IConfiguration` doğrudan okuma YOK; Options pattern. Üç ayrı alan:

| Alan | Rol | Ömür |
|---|---|---|
| BootstrapRegistrationKey | store→PG kayıt çağrısı yetkisi (`X-Registration-Key`) | ortak, platform-seviyesi, rotate |
| CallbackSecret | PG→store callback HMAC doğrulama | ayrı sır |
| CallbackBaseUrl | store'un callback ucu için PG'ye bildirilen taban URL | config |

(Dev: user-secrets; prod: vault — S5 borcuyla aynı.)

## SİLİNEN yapılar (TEARDOWN)

- `CredentialEntrySession` (token + expiry + Consume) — tüm aggregate.
- `CredentialEntryOptions` (PublicBaseUrl + LinkLifetime).
- `SubmitMerchantCredentials` command/response.
- `AdminRequestCredentialEntryLink` command/response + MCP tool.
- `PgOnboardingClient.ValidateCredentialsAsync` + `ValidateRequest` kontratı.

## İlişki / akış özeti

`AdminStartOnboarding` (register tetik) → `MerchantInformation.StartRegistration(correlationId)` persist → `PgOnboardingClient.RegisterAsync(payload, bootstrapKey, callbackUrl, correlationId)`. PG onayda callback → `ReceiveMerchantCredentials` → HMAC doğrula → `ApplyCredentialsFromCallback(...)` persist. Ödeme: `MerchantKeyGrpcService.GetKey` → Payment `MerchantKeyClient` (değişmez).