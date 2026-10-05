# Tasks: MerchantId Görünürlük Politikası (ECommerce bacağı)

**Feature**: `087-merchant-id-visibility` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Kapsam: yalnız ECommerceAgent (Customer.Api). PG bacağı (AgentPlatform) ayrı. Yollar `src/services/customer/Customer.Api/` kökünden.

## Phase 1: Setup

- [X] T001 [P] `DropShopOnboardingOption`'a üç alan ekle: `BootstrapRegistrationKey`, `CallbackSecret`, `CallbackBaseUrl`; `BindConfiguration` + `ValidateDataAnnotations` + `ValidateOnStart` — `Onboarding/DropShopOnboardingOption.cs`
- [X] T002 [P] Dev secret'ları set: `dotnet user-secrets set DropShopOnboarding:BootstrapRegistrationKey/CallbackSecret/CallbackBaseUrl --project src/services/customer/Customer.Api`; prod=vault notu — (dev config, kod değil)
- [X] T003 [P] `CallbackSignatureValidator` ekle (HMAC-SHA256(secret, raw-body) hex, constant-time), 077 `Payment.Api/Infrastructure/CallbackSignatureValidator.cs`'den ayna — `Infrastructure/CallbackSignatureValidator.cs`

## Phase 2: Foundational (US1'i bloke eder — domain TDD, İlke VI)

- [X] T004 [P] Domain test: `MerchantInformation.StartRegistration(correlationId)` — Pending+correlation set; zaten Pending+farklı correlation → Error (tek-aktif guard). xUnit+Shouldly — `tests/Customer.Api.Tests/MerchantInformationTests.cs`
- [X] T005 [P] Domain test: `ApplyCredentialsFromCallback` — correlation eşleşmezse Error (nötr red); eşleşirse Active+key set; zaten Active+aynı correlation → idempotent Ok (çift-callback no-op) — `tests/Customer.Api.Tests/MerchantInformationTests.cs`
- [X] T006 `MerchantInformation` EXTEND: `PendingCorrelationId` (Guid?) + `RegistrationStatus` enum (None/Pending/Active, aggregate dosyasında) + `StartRegistration` + `ApplyCredentialsFromCallback` davranışları (T004/T005 yeşil edene dek) — `Domains/MerchantInformations/MerchantInformation.cs`

## Phase 3: User Story 1 — Merchant credential'ı hiç ellemez (makine teslim) (P1)

**Goal:** Elle-giriş ekranı sökülür; kayıt store-başlatır (bootstrap key), credential HMAC-callback'le gelir, gRPC kullanım sürer.
**Independent Test:** quickstart Senaryo 1-5 — kayıt Pending, callback Active, negatif-güvenlik red, ödeme çalışır, 078 route 404.

### Teardown (078 elle-giriş)

- [X] T007 [P] [US1] `CredentialEntryEndpointExtension.cs` sil + `Program.cs:86 MapCredentialEntryEndpoints()` çağrısını kaldır — `CredentialEntryEndpointExtension.cs`, `Program.cs`
- [X] T008 [P] [US1] `Pages/CredentialEntry/*.html` (layout/form/success-verified/success-unverified/notfound) sil + `.csproj` embedded-resource girdileri — `Pages/CredentialEntry/`
- [X] T009 [P] [US1] `CredentialEntrySession` aggregate sil — `Domains/MerchantInformations/CredentialEntrySession.cs`
- [X] T010 [P] [US1] `SubmitMerchantCredentials` command/handler/response sil — `Domains/MerchantInformations/Features/Commands/SubmitMerchantCredentials.cs`
- [X] T011 [P] [US1] `AdminRequestCredentialEntryLink` command + MCP tool sil; `Shared/McpToolNames.cs` + `Shared/McpToolDescriptions.cs` ilgili sabitleri temizle — `Domains/MerchantInformations/Features/Agents/Commands/AdminRequestCredentialEntryLink.cs`
- [X] T012 [P] [US1] `CredentialEntryOptions` sil + `Program.cs` options kaydını kaldır — `Options/CredentialEntryOptions.cs`, `Program.cs`
- [X] T013 [US1] `PgOnboardingClient.ValidateCredentialsAsync` + `ValidateRequest` kontratını kaldır — `Onboarding/PgOnboardingClient.cs`

### Kayıt yolu (store→PG)

- [X] T014 [US1] `PgOnboardingClient.RegisterAsync(payload, bootstrapKey, callbackUrl, correlationId)` ekle: `POST /api/v1/onboarding/register`, `X-Registration-Key` header, finansal payload gövdede, 202/401/409 ele al — `Onboarding/PgOnboardingClient.cs` (kontrat: `contracts/register-request.md`)
- [X] T015 [US1] `AdminStartOnboarding` EXTEND: correlationId üret → `MerchantInformation.StartRegistration` persist → `RegisterAsync` çağır (business payload + bootstrap key + callbackUrl); dönüş sır-free (MerchantId/Key YOK) — `Domains/MerchantInformations/Features/Agents/Commands/AdminStartOnboarding.cs`

### Callback yolu (PG→store)

- [X] T016 [US1] `ReceiveMerchantCredentials` yazma slice: `[Transactional]` handler — correlation eşle → `ApplyCredentialsFromCallback` → persist; idempotent; MerchantKey asla log/dönüş — `Domains/MerchantInformations/Features/Commands/ReceiveMerchantCredentials.cs` (kontrat: `contracts/credential-callback.md`)
- [X] T017 [US1] `MerchantRegistrationCallbackEndpointExtension` ekle: `POST /internal/merchant-registration/callback` — raw body oku → `CallbackSignatureValidator` (geçersiz=400, önce HMAC sonra deserialize) → `ReceiveMerchantCredentials` invoke; `Program.cs`'e map — `MerchantRegistrationCallbackEndpointExtension.cs`, `Program.cs`
- [X] T018 [US1] `ReceiveMerchantCredentials` handler adı `*Handler` değilse `Program.cs`'e `opts.Discovery.IncludeType(...)` ekle (Wolverine keşif tuzağı) — `Program.cs`

### Reissue (ekransız)

- [X] T019 [US1] `AdminReissueMerchantKey` EXTEND: `RevealUrl`/ekran dönüşünü kaldır; PG reissue yeni key'i aynı HMAC-callback yoluyla teslim eder (`ReceiveMerchantCredentials` yeni-key'i de karşılar) — `Domains/MerchantInformations/Features/Agents/Commands/AdminReissueMerchantKey.cs`

### Wiring + süreç belgesi

- [X] T020 [US1] `appsettings.json` DropShopOnboarding section'a callback alanları; AppHost gerekiyorsa CallbackBaseUrl service-discovery wiring — `appsettings.json`, `src/aspire/AppHost/AppHost.cs`
- [X] T021 [US1] `FLOW.md` güncelle (onboarding süreci: elle-giriş → store-başlatır kayıt + onay + HMAC-callback); `scripts/check-flow-links.sh` yeşil — `src/services/customer/FLOW.md`

## Phase 4: User Story 2 — Politika tek yerde yazılı (P2)

**Goal:** MerchantId/Key ele-alma kuralı kanonik dokümanda; yeni yüzeyler referans alır.
**Independent Test:** quickstart — doküman "render YOK / kayıt S2S / dönüş HMAC-callback / kullanım gRPC / LLM opak tutamaç" içeriyor mu.

- [X] T022 [US2] `CLAUDE.md` + `docs/conventions.md`'ye ADR referanslı kısa kural ekle (render YOK / S2S / callback / gRPC / LLM opak); vault ADR `adr-mcp-control-plane-no-secret-return` zaten var — `CLAUDE.md`, `docs/conventions.md`

## Phase 5: Polish & Cross-Cutting

- [X] T023 [P] Grep denetimi: `MerchantKey`/`iban` hiçbir `ILogger`/trace/tool-dönüşünde değil (FR-006/ADR nöbetçi) — repo geneli
- [ ] T024 [P] quickstart Senaryo 1-5 manuel doğrula (kayıt Pending, callback Active, 400/nötr-red/idempotent, ödeme, 078 404) — BEKLİYOR: canlı Aspire + PG register/onay/callback stub'ı gerektirir (PG bacağı AgentPlatform, ayrı repo). Domain davranışı xUnit'te (21 test) kapsandı; uçtan-uca PG stub'ıyla koşulacak.
- [X] T025 `dotnet build` + `dotnet test tests/Customer.Api.Tests` yeşil

## Dependencies

- Phase 1 (T001-T003) → Phase 2.
- Phase 2 (T004-T006; test-first: T004/T005 ÖNCE, T006 sonra) → US1 bloke.
- US1: Teardown (T007-T013) US1 davranışından bağımsız, paralel. Kayıt yolu T014→T015. Callback yolu T006→T016→T017→T018. T019 T016'ya bağlı. T021 süreç netleştikten sonra.
- US2 (T022) US1'den bağımsız — paralel başlayabilir (politika yazımı koda bağlı değil).
- Polish US1+US2 sonrası.

## Parallel Opportunities

- Setup: T001, T002, T003 paralel.
- Domain test: T004, T005 paralel (sonra T006).
- Teardown: T007-T012 paralel (ayrı dosyalar); T013 PgOnboardingClient'ta T014 ile çakışır → sıralı.
- US2 (T022) US1 ile paralel.
- Polish: T023, T024 paralel.

## MVP

**US1 (P1) tek başına MVP** — makine-handoff + callback + 078 söküm; S4'ün çekirdeği. US2 (politika yazımı) ucuz ek, US1 ile paralel. PG bacağı (AgentPlatform) US1'i canlı uçtan-uca kapatmak için gerekli ama ayrı repo/plan.