# Research: MerchantId Görünürlük (ECommerce bacağı)

Phase 0. NEEDS CLARIFICATION yok (clarify oturumunda kapandı); kararlar + mevcut-kod dayanakları.

## Karar 1 — Credential dönüşü: asenkron HMAC-callback (077 ayna)

- **Decision:** PG onayda MerchantId+Key'i store'un callback ucuna POST eder, `CallbackSecret` ile HMAC-SHA256(raw-body) imzalı; store imza+correlation doğrular, sonra persist.
- **Rationale:** Onay asenkron (PG admin MCP'den onaylar) → senkron yanıt olmaz. 077'de birebir emsal var: `CallbackSignatureValidator` (constant-time hex karşılaştırma, `X-Signature`), `HandlePaymentCallback` (idempotent + outbox). Ayna → sıfır yeni desen.
- **Alternatives:** Senkron yanıtta credential (onay asenkron olduğu için düşer); store-polling (gereksiz karmaşa, callback varken).

## Karar 2 — Kayıt çağrısı auth: ortak bootstrap kayıt key'i

- **Decision:** store→PG register çağrısı önceden-paylaşılan **bootstrap kayıt key'i** ile imzalanır/başlıklanır; secret store'da (dev: user-secrets, prod: vault), platform-seviyesi, merchant-başına değil.
- **Rationale:** Tavuk-yumurta — kayıtta henüz MerchantKey yok. Bootstrap key tek çevrimdışı güven çıpası; yalnız kayıt ucunu açar (dar kapsam), rotate edilebilir. Mevcut `OnboardingGatewayTokenHandler` (m2m client-credentials) zaten store→PG bearer enjekte ediyor — bootstrap key bununla taşınabilir ya da ayrı `X-Registration-Key`. **Seçim:** ayrı `X-Registration-Key` header (m2m token = taşıma kimliği; registration key = kayıt-ucu yetkisi; iki kaygı ayrı).
- **Alternatives:** Per-merchant key (kayıtta yok → imkansız); mTLS (dev DropShop için ağır, sertifika dağıtım); yalnız m2m token (kayıt-ucu özel yetkisini ayırmaz).

## Karar 3 — Üç ayrı sır, ayrı ömür

- **Decision:** `BootstrapRegistrationKey` (ortak, kayıt çağrısı) ≠ `MerchantKey` (per-merchant, PG onayda basar, gRPC ödeme) ≠ `CallbackSecret` (PG→store callback HMAC). Üçü `DropShopOnboardingOption`'a eklenir (Options pattern; `IConfiguration` doğrudan okuma YOK).
- **Rationale:** Sızma yarıçapını ayırır; biri rotate edilince öteki etkilenmez. 077'de `CallbackSecret` zaten MerchantKey'den ayrıydı — emsal.

## Karar 4 — Correlation + idempotency

- **Decision:** store register'da `correlationId` üretir, kayıtta gönderir; callback onu taşır. Store callback'i correlation ile eşler; aynı correlation ikinci kez gelirse idempotent yutulur (Marten upsert; `MerchantInformation` deterministik Id). Eşleşmeyen correlation → nötr red.
- **Rationale:** Çift-callback / gecikmeli-callback güvenliği (spec edge case). 077 `HandlePaymentCallback` idempotent emsali.

## Karar 5 — 078 ekranı tam sök

- **Decision:** `CredentialEntryEndpointExtension`, `CredentialEntrySession`, `SubmitMerchantCredentials`, `AdminRequestCredentialEntryLink`, `CredentialEntryOptions`, `Pages/CredentialEntry/*` (5 html), `PgOnboardingClient.ValidateCredentialsAsync` silinir. `Program.cs:86 MapCredentialEntryEndpoints` + options kaydı sökülür.
- **Rationale:** Sır artık ekrana hiç girmediğinden forward/sub-kıyası borcu (adr-credential-link-forward-protection) konusuz. Ölü kod bırakmak = bakım + sır-ekrana-girer riski. Kullanıcı "tam sök" dedi.
- **Dikkat:** Reissue (`AdminReissueMerchantKey`) KEEP ama `RevealUrl` dönüşü ekransız modele taşınır — yeni key de callback'le gelir (EXTEND).

## Karar 6 — gRPC merchant-key yolu değişmez

- **Decision:** `MerchantKeyService` / `MerchantKeyGrpcService` / Payment `MerchantKeyClient` KEEP. Ödeme yolu (`X-Api-Key` kaynağı customer.read gRPC) aynen.
- **Rationale:** Zaten ADR-uyumlu (key agent'a dönmez); 087 yalnız credential'ın store'a GİRİŞ yolunu değiştirir, store-içi KULLANIM yolunu değil.

## Açık kalan (plan-sonrası, tasks/impl)

- Reissue'nun yeni-key'i callback'le mi yoksa senkron S2S yanıtla mı döndüğü — PG bacağı kararına bağlı (AgentPlatform planı). Bu repoda handler iki yolu da karşılayacak şekilde `ReceiveMerchantCredentials`'a birleşebilir.
- Callback endpoint rate-limit (S1 borcu) — ayrı; bu spec eklemez ama not düşer.