---
description: "Task list — RBAC (rol = scope demeti)"
---

# Tasks: RBAC — Rol = Scope Demeti

**Input**: Design documents from `/specs/030-rbac-scope-roles/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/role-management.md

**Tests**: İlke VI (Domain-TDD) — saf birimler (ScopeResolver, AssignableScopeValidator,
ApplySingleRole) test-first; test task'ı implementasyondan ÖNCE. Diğer katmanlar (endpoint,
pages, seed, UI) canlı doğrulama (quickstart) ile — birim testi opsiyonel.

**Kapsam notu**: Identity.Server IdP altyapısıdır; değişiklik oraya + Common'da tek scope
sabiti + WebApp'te tek header linki. Yeni servis/BC yok.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

**Purpose**: Proje iskeleti ve test projesi

- [X] T001 [P] `src/others/Identity.Server/Rbac/` klasörünü oluştur ve `tests/Identity.Server.Tests/`
  xUnit+Shouldly projesini (yoksa) kur, `ECommerceAgent.slnx`'e ekle

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: US1/US2/US4'ün paylaştığı çekirdek — scope registry, RoleScope tablosu, saf
birimler, rol yönetim servisi. **⚠️ Tüm story'ler bunu bekler.**

- [X] T002 [P] `identity.roles.manage` sabitini `src/others/Common/Utils/Constants/AuthorizationScopes.cs`'e ekle
- [X] T003 `identity.roles.manage` scope'unu `src/others/Identity.Server/Config.cs`'te kaydet
  (AllApiScopes + ScopeResources; admin demetine girecek)
- [X] T004 [P] KnownScopes registry'sini (ad + açıklama) `src/others/Identity.Server/Rbac/KnownScopes.cs`'te
  oluştur; kaynak AuthorizationScopes/AllApiScopes (FR-005/007)
- [X] T005 [P] RoleScope EF entity'sini `src/others/Identity.Server/Rbac/RoleScope.cs`'te oluştur (Id, RoleId, Scope)
- [X] T006 `DbSet<RoleScope>` + unique(RoleId,Scope) index'i `src/others/Identity.Server/Data/ApplicationDbContext.cs`'e ekle (T005)
- [X] T007 RoleScopes tablosu için EF migration üret + uygula (T006)
- [X] T008 [P] Domain-TDD: `ApplySingleRole` için başarısız birim testi yaz
  `tests/Identity.Server.Tests/ApplySingleRoleTests.cs` (INV-2/3: atama mevcut rolü değiştirir, rolsüz bırakmaz)
- [X] T009 [P] Domain-TDD: `AssignableScopeValidator` için başarısız birim testi yaz
  `tests/Identity.Server.Tests/AssignableScopeValidatorTests.cs` (INV-1: bilinmeyen scope reddi)
- [X] T010 `ApplySingleRole` saf mantığını `src/others/Identity.Server/Rbac/RoleAssignmentRules.cs`'te yaz (T008 geçsin)
- [X] T011 `AssignableScopeValidator` saf mantığını `src/others/Identity.Server/Rbac/AssignableScopeValidator.cs`'te yaz (T009 geçsin)
- [X] T012 `RoleAssignmentService`'i `src/others/Identity.Server/Rbac/RoleAssignmentService.cs`'te yaz:
  rol CRUD (seed+kullanımda-olan silme guard'ı INV-5), rol→scope set (T011 doğrulama), tek-rol atama
  (T010), son-admin kilidi (INV-4) — UserManager/RoleManager + RoleScope üzerinden (T005,T010,T011)

**Checkpoint**: Scope registry + RoleScope tablosu + rol yönetim servisi hazır.

---

## Phase 3: User Story 1 - Kullanıcı rolüne göre yetkilenir (Priority: P1) 🎯 MVP

**Goal**: Token verme anında granted API scope'ları = `requested ∩ rol demeti`; downstream
yalnız scope görür.

**Independent Test**: ScopeResolver birim testi mock'suz; canlı: customer vs admin login →
token scope claim'i rol demetine göre farklı (quickstart S1/S2/S5). Canlı demo seed'e (US4) bağlı.

- [X] T013 [P] [US1] Domain-TDD: `ScopeResolver` için başarısız birim testi yaz
  `tests/Identity.Server.Tests/ScopeResolverTests.cs` (INV-6: granted=requested∩bundle; geçersiz/KnownScopes-dışı eşleme yazılmaz)
- [X] T014 [US1] `ScopeResolver` saf mantığını `src/others/Identity.Server/Rbac/ScopeResolver.cs`'te yaz (T013 geçsin)
- [X] T015 [US1] Kullanıcının rol demetini (rol→scope) getiren okuma yardımcısını
  `src/others/Identity.Server/Rbac/RoleScopeQuery.cs`'te yaz (T005)
- [X] T016 [US1] `src/others/Identity.Server/Endpoints/AuthorizeEndpoint.cs`'te `SetScopes(...)`'ı
  rol demetiyle süz (T014,T015); kimlik scope'ları (openid/profile/email/roles/offline_access) her zaman geçer
- [X] T017 [P] [US1] `src/others/Identity.Server/Endpoints/TokenEndpoint.cs` refresh dalında scope'ları
  GÜNCEL rolden yeniden türet (FR-012); client_credentials (makine) dalına DOKUNMA (RBAC dışı)

**Checkpoint**: Login/refresh rol-süzülmüş scope verir; makine token'ı etkilenmez.

---

## Phase 4: User Story 4 - Seed: roller, map, bootstrap admin (Priority: P1)

**Goal**: Açılışta idempotent seed — admin+customer rolleri, rol→scope map, login olabilen
bootstrap admin, ingestion-agent client, rolsüz kullanıcı backfill.

**Independent Test**: Temiz identityDb → bootstrap admin login olur, yönetim ekranı açılır;
yeniden başlat → duplike yok (quickstart S1/S7).

- [X] T018 [US4] `admin` + `customer` rollerini seed et (RoleManager, idempotent)
  `src/others/Identity.Server/SeedHostedService.cs`
- [X] T019 [US4] Rol→scope map'ini seed et (customer/admin demetleri KnownScopes'tan; admin ⊇ customer +
  yönetim/yazma scope'ları + identity.roles.manage) `src/others/Identity.Server/SeedHostedService.cs` (T012,T019⟵T004)
- [X] T020 [US4] Bootstrap admin kullanıcıyı seed et (email+parola config'ten; admin rolü; EmailConfirmed=true)
  `src/others/Identity.Server/SeedHostedService.cs` + config anahtarları (parola KODDA DEĞİL) (FR-016)
- [X] T021 [P] [US4] `ingestion-agent` client'ını ekle (client_credentials, catalog.write+stock.write)
  `src/others/Identity.Server/Config.cs` + seed loop (FR-018)
- [X] T022 [US4] Rolsüz mevcut kullanıcıları `customer`'a backfill et `src/others/Identity.Server/SeedHostedService.cs` (T018, FR-021)

**Checkpoint**: Temiz kurulumda admin girer; seed idempotent; makine client hazır.

---

## Phase 5: User Story 2 - Admin ekrandan rol/scope/atama yönetir (Priority: P1)

**Goal**: Admin, IdP Razor Pages'ten rol CRUD + rol→scope işaretleme + kullanıcı-rol atama
yapar; scope yalnız KnownScopes'tan seçilir; giriş WebApp header linkinden.

**Independent Test**: Admin ekranda rol yaratır, scope işaretler, kullanıcı rolü değiştirir;
uydurma scope reddedilir; customer ekrana giremez (quickstart S0/S3/S4).

- [X] T023 [US2] `/Admin/*` sayfalarını cookie kullanıcısının `admin` rolüyle koru (convention/policy)
  `src/others/Identity.Server/Program.cs` + `Pages/Admin/` (D3); `identity.roles.manage` eşdeğer guard notu
- [X] T024 [P] [US2] `Pages/Admin/Roles` — List + Create + Delete (seed-rol & kullanımda-rol guard'ı T012 servisinden)
  `src/others/Identity.Server/Pages/Admin/Roles/`
- [X] T025 [P] [US2] `Pages/Admin/Roles/Scopes` — KnownScopes'tan checkbox listesi + kaydet (T012 doğrulama; serbest metin yok)
  `src/others/Identity.Server/Pages/Admin/Roles/Scopes.cshtml(.cs)` (FR-006/009)
- [X] T026 [P] [US2] `Pages/Admin/Users` — List + SetRole (tek-rol, son-admin kilidi T012 servisinden)
  `src/others/Identity.Server/Pages/Admin/Users/` (FR-010/019)
- [X] T027 [US2] WebApp header'ına scope-koşullu "Yönetim" linki (`identity.roles.manage`) → IdP `/Admin/Roles`
  `src/ui/WebApp/` (layout/header + view component) (D6, FR-011 kozmetik görünürlük)

**Checkpoint**: Tüm rol yönetimi ekrandan; uyumsuzluk (uydurma scope) imkansız.

---

## Phase 6: User Story 3 - Register → otomatik customer → direkt login (Priority: P2)

**Goal**: Yeni kayıt otomatik `customer` rolü alır (seçim yok), aktivasyonsuz direkt login.

**Independent Test**: Yeni hesap → onay adımı yok → login → token customer scope'ları (quickstart S2).

- [X] T028 [US3] Register akışında otomatik `customer` rolü ata (T012 servisi/UserManager)
  `src/others/Identity.Server/Pages/Account/Create/Index.cshtml.cs` (FR-013)
- [X] T029 [US3] Kayıt sonrası aktivasyonsuz direkt login yolunu doğrula/koru (mevcut auto sign-in)
  `src/others/Identity.Server/Pages/Account/Create/Index.cshtml.cs` (FR-014)

**Checkpoint**: Yeni kullanıcı customer olarak anında girer.

---

## Phase 7: Polish & Cross-Cutting

**Purpose**: Doküman, bellek, canlı doğrulama

- [X] T030 [P] CLAUDE.md "Yetkilendirme" bölümünü güncelle — rol=scope demeti, KnownScopes,
  token-anında süzme, admin ekran, seed, ingestion-agent client
- [X] T031 [P] Memory `roles-status.md`'yi güncelle — "roles REMOVED" → RBAC 030 built (rol=scope demeti)
- [ ] T032 Quickstart S0–S7'yi Aspire üzerinden canlı doğrula (`dotnet run --project src/aspire/AppHost/AppHost.csproj`)
- [X] T033 [P] `dotnet test --filter "FullyQualifiedName~Rbac"` tüm saf birim testleri yeşil (İlke VI)

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (P1)**: bağımsız.
- **Foundational (P2)**: Setup'a bağlı — TÜM story'leri BLOKLAR.
- **US1 (P3)**: Foundational'a bağlı. Mekanizma bağımsız test edilir; canlı demo US4 seed'ine bağlı.
- **US4 (P4)**: Foundational + T012'ye bağlı (rol yönetim servisi/map).
- **US2 (P5)**: Foundational + T012'ye bağlı; US4 (admin var olmalı) canlı önkoşul.
- **US3 (P6)**: Foundational + T012'ye bağlı.
- **Polish (P7)**: istenen story'ler bitince.

### Within-story
- Domain-TDD: T008/T009 (foundational) ve T013 (US1) testleri implementasyondan ÖNCE, FAIL etmeli.
- Model→servis→endpoint/pages sırası; core→integration.

### Parallel Opportunities
- Setup: T001.
- Foundational: T002,T004,T005 paralel; T008,T009 paralel (testler). T003/T006/T007/T010-T012 sıralı bağımlı.
- US1: T013 [P] test; T017 [P] (farklı dosya, T014/T015 sonrası).
- US4: T021 [P] (Config, farklı dosya).
- US2: T024,T025,T026 paralel (farklı sayfa klasörleri); T023 önce (guard), T027 WebApp ayrı.
- Polish: T030,T031,T033 paralel.

---

## Parallel Example: Foundational

```bash
# Paralel (farklı dosyalar):
Task: "T002 AuthorizationScopes'e identity.roles.manage ekle"
Task: "T004 KnownScopes registry oluştur"
Task: "T005 RoleScope EF entity oluştur"
# Paralel test-first (İlke VI):
Task: "T008 ApplySingleRole başarısız testi"
Task: "T009 AssignableScopeValidator başarısız testi"
```

---

## Implementation Strategy

### MVP (US1 + gerekli seed)
1. Phase 1 Setup → Phase 2 Foundational (kritik, hepsini bloklar).
2. Phase 3 US1 (token süzme) + Phase 4 US4 (seed) — US1'in canlı demosu seed'e bağlı olduğundan
   pratikte birlikte. **DUR & DOĞRULA**: customer vs admin token scope farkı (S1/S2/S5).

### Incremental
3. Phase 5 US2 (admin ekran) → S0/S3/S4 doğrula.
4. Phase 6 US3 (register) → S2 doğrula.
5. Phase 7 Polish → CLAUDE.md + memory + tam quickstart.

---

## Notes
- [P] = farklı dosya, bağımsız.
- İlke VI saf birimleri (ScopeResolver, AssignableScopeValidator, ApplySingleRole) test-first;
  diğer katmanlar canlı doğrulama.
- Downstream servisler DEĞİŞMEZ — yalnız Identity.Server + Common(1 sabit) + WebApp(1 link).
- Bootstrap admin parolası config/secret'ten; koda gömme.
- Her task veya mantıksal grup sonrası commit; checkpoint'te story'yi bağımsız doğrula.