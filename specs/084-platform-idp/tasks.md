---
description: "Task list — Platform IdP Terfisi (Dilim A)"
---

# Tasks: Platform IdP Terfisi (Dilim A / 084-platform-idp)

**Input**: `specs/084-platform-idp/` — plan.md, spec.md, research.md, data-model.md, contracts/app-registration.md, quickstart.md

**Tests**: İLKE VI — yalnız saf mantık (AppRegistry doğrulama değişmezleri) için test-first ZORUNLU. Taşıma/kablo/altyapı = canlı doğrulama (quickstart S1-S6).

**İki repo**: `AgentPlatform/` (YENİ) + `ECommerceAgent/` (mevcut). Paylaşılan: `Platform.Auth` NuGet.

## Phase 1: Setup

- [X] T001 `AgentPlatform` git repo'sunu oluştur; `AgentPlatform/AgentPlatform.slnx` çözümü + `AgentPlatform/src/` iskeleti
- [X] T002 `AgentPlatform/src/AppHost` Aspire AppHost projesi (idp + `identityDb` Postgres kaynağı) — ECommerce AppHost deseninden
- [X] T003 [P] `Platform.Auth` sınıf kütüphanesi (nötr auth kablosu) + NuGet paketleme metadata'sı + `AgentPlatform/tests/Identity.Server.Tests` test projesi (xUnit + Shouldly); ikisini de `AgentPlatform.slnx`'e ekle
- [X] T004 [P] `Platform.Auth` için paket beslemesini yapılandır (GitHub Packages private feed); iki repoda `NuGet.config` + CPM `Directory.Packages.props` sürüm pin'i

---

## Phase 2: Foundational (bloklayıcı ön-koşul)

**Purpose**: Common ayrımı — hem IdP (sunucu) hem relying party'ler (ECommerce servisleri) buna dayanır.

- [X] T005 `Common`'ın nötr kısmını `Platform.Auth`'a taşı: `IdentityOption`, `Extensions/AuthenticationExtension.cs` (`AddAuthenticationAndAuthorizationExtension`) (kaynak: `src/others/Common/Options/IdentityOption.cs`, `src/others/Common/Extensions/AuthenticationExtension.cs`). NOT: `ScopeClaimArrayHandler` SUNUCU-tarafı OpenIddict handler'ı (`IOpenIddictServerHandler`, token üretimi) → pakete DEĞİL, IdP'de kalır (T009 ile AgentPlatform'a taşınır)
- [X] T006 `Common/Utils/Constants/AuthorizationScopes.cs`'i ECommerce'te BIRAK (app #1 ad-uzayı); nötr olmadığını doğrula (scope sabitleri paket dışı)
- [X] T007 `Platform.Auth` paketini yayınla (ilk sürüm); ECommerce `Common`'ı ona referanslasın (taşınan tipler için)

**Checkpoint**: `Platform.Auth` tüketilebilir; ECommerce build yeşil (nötr auth kablosu paketten).

---

## Phase 3: User Story 1 — Kimlik makamı ECommerce dışında, nötr durur (P1)

**Goal**: IdP `AgentPlatform`'a taşınmış + uygulama-nötr; ikinci app config'le tanınır, çakışma reddedilir.

**Independent Test**: quickstart S1, S5, S6 — standalone boot + `pg` config'le tanınır + `catalog.write` çakışması RET.

### Test-first (İLKE VI — saf doğrulama mantığı)
- [X] T008 [P] [US1] `AppRegistryOptions` doğrulama testleri (başarısız yaz): AppId tekilliği, scope kesişmezliği (çakışma RET), client scope alt-küme kuralı — mevcut `AgentPlatform/tests/Identity.Server.Tests` projesine `AppRegistryValidationTests.cs` (contracts/app-registration.md değişmezleri)

### Taşıma + nötrleme
- [X] T009 [US1] `src/others/Identity.Server`'ı `AgentPlatform/src/Identity.Server`'a taşı; csproj referanslarını `Platform.Auth`'a çevir; çözüme ekle
- [X] T010 [US1] `AppRegistryOptions` POCO + doğrulama (T008'i geçir) — `AgentPlatform/src/Identity.Server/Options/AppRegistryOptions.cs`; `AddOptions().BindConfiguration("AppRegistry").ValidateOnStart()`
- [X] T011 [US1] `Config.cs`'i AppRegistry'den besle: sabit `ScopeResources`/`AllApiScopes`/client dizileri registry'den okunur — `AgentPlatform/src/Identity.Server/Config.cs`
- [X] T012 [US1] `KnownScopes.All`'ı registry birleşiminden türet (tek-kaynak korunur) — `AgentPlatform/src/Identity.Server/Rbac/KnownScopes.cs`
- [X] T013 [US1] `SeedHostedService` seed döngüsünü app-registry üzerinden çalıştır (app başına scope + client + rol) — `AgentPlatform/src/Identity.Server/Connect/SeedHostedService.cs`
- [X] T014 [US1] ECommerce'i "app #1" olarak AgentPlatform config'ine yaz (mevcut scope/client/rol demetleri) — `AgentPlatform/src/AppHost` appsettings `AppRegistry:Apps`
- [X] T015 [US1] Issuer URL'ini config'ten oku (sabit `https://localhost:5001` kaldır); HTTPS zorunlu kalsın — `AgentPlatform/src/Identity.Server/Program.cs`

**Checkpoint**: quickstart S1 (boot) + S5 (`pg` tanınır) + S6 (çakışma RET) PASS.

---

## Phase 4: User Story 2 — Login/consent/RBAC çalışır (P1)

**Goal**: Makam çıkarıldıktan sonra kimlik doğrulama fiilen çalışır; downstream scope görür (rol değil).

**Independent Test**: quickstart S2, S3, S4 — login+token+korumalı erişim, scope-only karar, DCR+consent.

- [X] T016 [US2] ECommerce AppHost'ta IdP proje-ref'ini (`AddProject<Projects.Identity_Server>`) dış-servis referansına çevir; bağımlı servislerin `IdentityOption.Address`'i AgentPlatform issuer URL'ine baksın — `src/aspire/AppHost/AppHost.cs`
- [X] T017 [US2] ECommerce relying-party servisleri `Platform.Auth`'ı tüketsin (`AddAuthenticationAndAuthorizationExtension` paketten); build + auth kablosu yeşil
- [X] T018 [US2] Downstream scope-only kararını doğrula (rol adı token'da yetki kaynağı değil) — quickstart S3
- [X] T019 [US2] DCR + consent + loopback muafiyetinin (`AdminAgentApplicationManager`) taşındıktan sonra çalıştığını doğrula; YENİ loopback-only bağ EKLENMEDİĞİNİ teyit et — quickstart S4

**Checkpoint**: quickstart S2-S4 canlı PASS (iki AppHost koşar, ECommerce IdP'ye dış-servis olarak bağlı).

---

## Phase 5: Polish & Cross-Cutting

- [X] T020 [P] Eski `src/others/Identity.Server` + `Common` nötr tiplerinin kalkan referanslarını temizle (ECommerce'te ölü `Projects.Identity_Server` izleri)
- [X] T021 [P] `CLAUDE.md` BC haritası + "Yapma listesi"ni güncelle: identity-server artık `AgentPlatform` repo'sunda platform IdP; ECommerce relying party
- [X] T022 [P] Memory `central-multi-app-mcp-direction`'a A tamamlandı + repo=`AgentPlatform` + Common=`Platform.Auth` notu düş
- [X] T023 Tam çözüm build (iki repo) + `AppRegistryValidationTests` yeşil + quickstart S1-S6 tam tur

---

## Dependencies

- **Setup (T001-T004)** → her şeyden önce.
- **Foundational (T005-T007)** → US1 ve US2'den önce (paket olmadan ne IdP taşınır ne relying party bağlanır).
- **US1 (T008-T015)** → US2'den önce (makam çıkmadan/nötrlenmeden auth kablosu dış-ref'e dönemez).
- **US2 (T016-T019)** → US1 sonrası.
- **Polish (T020-T023)** → en son.
- Test-first: **T008 → T010** (doğrulama impl'i testi geçirir).

## Parallel Opportunities

- T003, T004 birlikte (paket projesi + feed).
- T008 (test yazımı) T009 taşımayla paralel başlayabilir (farklı dosyalar).
- T020, T021, T022 birlikte (temizlik + iki doküman).

## MVP

**MVP = US1 + US2 birlikte** (ikisi de P1): US1 makamı çıkarır/nötrler ama US2 olmadan auth fiilen
kullanılamaz. İkisi tamamlanınca "tek nötr platform IdP, ikinci app config'e hazır" teslim edilir.
Dilim B (PG relying party) + Dilim C (fasat) ayrı spec.

## Uygulama sapmaları (2026-09-24, kullanıcı onaylı)

- **T004 feed = YEREL dosya-sistemi** (`/Users/macbook/dev/local-nuget`), GitHub Packages DEĞİL (kullanıcı
  seçimi: credential'sız, dev'e yeter; research "yerel feed de yeter" dedi). İki repo `NuGet.config` → `../local-nuget`.
- **T014 app-registry konumu = `Identity.Server/appsettings.json`** (`AppRegistry:Apps`), AppHost appsettings
  değil — validasyonu IdP process'i yapar, config-owner IdP daha doğru. Fonksiyonel olarak aynı (config güdümlü).
- **T001 repo = `/Users/macbook/dev/AgentPlatform`, `git init` only** (GitHub remote/push YOK — kullanıcı sonra).
- **Platform.Auth namespace'leri `Common.*` KORUNDU** → ECommerce tüketicilerinde using değişmedi (0 servis csproj churn).

## Canlı doğrulama sonucu (2026-09-24, iki AppHost koştu)

- **S1 PASS** — IdP standalone boot, issuer=`https://localhost:5001/` config'ten.
- **S2 PASS (token)** — m2m token üretildi (registry client `mcp-gateway-discovery`), scope→audience config'ten, scope JSON-array (ScopeClaimArrayHandler).
- **S3 PASS** — scope confinement: client sahibi olmadığı scope isteyince `400`; korumalı `/mcp` token'sız `403`. (T018)
- **S5 PASS** — `pg` config'le eklendi, restart sonrası `pg.merchant`+`pg.commission.admin` discovery `scopes_supported`'ta; ECommerce koduna dokunulmadı (SC-002).
- **S6 PASS** — `pg`'ye `catalog.write` eklenince boot REDDETTİ (identity-server crash + net çakışma hatası) (SC-004).
- **KALAN = S4 (T019):** DCR + consent + loopback — Claude Desktop tarayıcı-login akışı, kullanıcı testi. Kod hazır (registry-güdümlü DCR şablonu + `AllowLoopbackRedirect` bayrağı). T023 = S4 sonrası tam tur.