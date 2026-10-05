# Implementation Plan: MerchantId Görünürlük Politikası (ECommerce bacağı)

**Branch**: `087-merchant-id-visibility` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md)

**Input**: `specs/087-merchant-id-visibility/spec.md`

## Summary

Merchant credential'ı hiçbir insan-yüzeyinde render edilmez. Mevcut 078 elle-giriş ekranı SÖKÜLÜR; kayıt store-başlatır (admin MCP tetik → sunucu-tarafı PG'ye S2S POST, bootstrap key'le imzalı, finansal payload gövdede), credential asenkron **HMAC-callback** ile döner (077 `CallbackSignatureValidator` deseni aynalanır), store-içi kullanım mevcut gRPC `MerchantKeyService` ile sürer. Dayanak: [[adr-mcp-control-plane-no-secret-return]].

**Kapsam notu:** Bu plan yalnız **ECommerceAgent (Customer.Api)** bacağını kapsar. PG bacağı (kayıt ucu, onay-MCP, HMAC-callback göndereni, MCP dönüşünde hassas-alan dışlama) **AgentPlatform/PaymentGateway** repo'sunda ayrı planlanır (FR-005/FR-007/FR-009 oraya düşer).

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings)
**Primary Dependencies**: Marten (customerDb document store), Wolverine (in-proc + idempotent handler), ASP.NET Minimal API (callback endpoint), gRPC (`MerchantKeyService`, mevcut), OpenIddict relying-party (m2m token mevcut `OnboardingGatewayTokenHandler`)
**Storage**: customerDb — `MerchantInformation` aggregate (MerchantId + MerchantKey + CredentialsVerified)
**Testing**: xUnit + Shouldly — `MerchantInformation` aggregate davranışı test-first (İlke VI)
**Target Platform**: Linux server, Aspire AppHost
**Project Type**: web-service (tek BC mikroservis — Customer.Api)
**Performance Goals**: N/A (düşük hacim; tek first-party merchant bugün)
**Constraints**: Callback idempotent + fail-closed; HMAC doğrulanmadan persist YOK; hiçbir değer log/trace/tool-dönüşünde
**Scale/Scope**: Tek first-party merchant (MerchantKeyClient yorumu); çoklu-merchant YAGNI

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden.*

- **İlke I (BC izolasyonu):** store→PG = dış PSP (DropShop); callback **dış-webhook** → HTTP zorunlu (sanksiyonlu istisna, CLAUDE.md S2S kuralı). Kontrat `contracts/` altında bilinçli sözleşme. gRPC merchant-key DB izolasyonunu bozmaz. ✓
- **İlke III (VSA+CQRS, MCP ince):** kayıt tetik = admin MCP tool'u ince sarmalayıcı (`AdminStartOnboarding` EXTEND); callback = `Features/Commands/ReceiveMerchantCredentials` (yazma slice, `[Transactional]`). Repository yok. ✓
- **İlke IV (Result):** tüm handler/aggregate `FeatureResultModel`/`ResultDomain`. ✓
- **İlke V (scope):** register MCP tool `merchant.credentials.write` scope'lu (mevcut). **v1.11.1 capability-link istisnasına ARTIK dayanılmıyor** — ekran söküldüğü için sır hiç ekrana girmez (daha güçlü konum). Callback endpoint HMAC ile korunur (JWT değil; İlke V "JWT-dışı custom şema meşru, zorlama kanıttır" — HMAC imza = kanıt). ✓
- **İlke VI (Domain-TDD):** `MerchantInformation` callback-credential-set davranışı test-first. ✓
- **İlke VII (FLOW.md):** Customer.Api merchant-onboarding domain süreci değişiyor → `src/services/customer/FLOW.md` aynı PR'da güncellenir. ✓

**Gate: PASS** — ihlal yok, Complexity Tracking gereksiz.

## Project Structure

### Documentation (this feature)

```text
specs/087-merchant-id-visibility/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (register request + callback)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (ECommerceAgent, Customer.Api)

```text
src/services/customer/Customer.Api/
├── Domains/MerchantInformations/
│   ├── MerchantInformation.cs                      # EXTEND: callback-credential-set davranışı (+correlation)
│   ├── CredentialEntrySession.cs                   # TEARDOWN
│   ├── Features/Commands/
│   │   ├── SubmitMerchantCredentials.cs            # TEARDOWN
│   │   └── ReceiveMerchantCredentials.cs           # YENİ: callback handler (idempotent, HMAC doğrulanmış sonrası)
│   └── Features/Agents/Commands/
│       ├── AdminStartOnboarding.cs                 # EXTEND: kayıt verisi+bootstrap key+CallbackUrl+correlation ile PG register
│       ├── AdminRequestCredentialEntryLink.cs      # TEARDOWN
│       └── AdminReissueMerchantKey.cs              # EXTEND: reissue sonucu da callback'le (ekransız)
├── CredentialEntryEndpointExtension.cs             # TEARDOWN
├── MerchantRegistrationCallbackEndpointExtension.cs # YENİ: POST /internal/merchant-registration/callback (HMAC)
├── Infrastructure/CallbackSignatureValidator.cs    # YENİ: 077 deseninden ayna (HMAC-SHA256)
├── Options/CredentialEntryOptions.cs               # TEARDOWN
├── Onboarding/
│   ├── PgOnboardingClient.cs                       # EXTEND: RegisterAsync (bootstrap-key auth, CallbackUrl); ValidateCredentialsAsync TEARDOWN
│   └── DropShopOnboardingOption.cs                 # EXTEND: BootstrapRegistrationKey + CallbackSecret + CallbackBaseUrl
├── Pages/CredentialEntry/*.html                    # TEARDOWN (5 dosya)
└── Grpc/HostedPayment/MerchantKeyGrpcService.cs    # KEEP (değişmez)
```

**Structure Decision**: Mevcut VSA düzeni korunur. Callback endpoint `*EndpointExtension` + yazma slice `Features/Commands/`'te (süreç-güdümlü değil — dış sistemin tetiklediği ama BC'nin kendi credential-persist niyeti; `ReceiveMerchantCredentials` yazma slice). Wolverine idempotent handler → `Program.cs` `IncludeType` gerekebilir (ad `*Handler` ise gerekmez; kontrol et).

## Complexity Tracking

Gereksiz — Constitution Check PASS, ihlal yok.