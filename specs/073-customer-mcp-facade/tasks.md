---
description: "Task list for Tek Müşteri MCP Fasadı"
---

# Tasks: Tek Müşteri MCP Fasadı

**Input**: Design documents from `/specs/073-customer-mcp-facade/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: İlke VI (Domain-TDD) — fasadın SAF birimleri (ToolRoutingRegistry ad→BC, SurfaceFilter
müşteri/admin, step-up/anonim karar) test-first. Discovery/proxy/auth/wiring test-sonra + Claude Desktop
canlı doğrulama (quickstart). Fasad DB'siz + domain aggregate yok.

**Organization**: User story bazlı fazlar. MVP = US1.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 `src/agents/Mcp.Gateway/Mcp.Gateway.csproj` oluştur (net10; ref: Common, Shared, ServiceDefaults; `ModelContextProtocol.AspNetCore` + `ModelContextProtocol.Core`; sürümsüz PackageReference)
- [X] T002 `Mcp.Gateway` + `tests/Mcp.Gateway.Tests`'i `ECommerceAgent.slnx`'e ekle
- [X] T003 [P] `src/agents/Mcp.Gateway/GlobalUsings.cs`
- [X] T004 [P] `src/agents/Mcp.Gateway/Properties/launchSettings.json` (Production default tuzağı — bkz [[aspire-service-needs-launchsettings]])
- [X] T005 [P] `src/agents/Mcp.Gateway/Options/FacadeOption.cs` (downstream BC listesi + yüzey + RequiresUserAuth + discovery makine kimliği + CacheTtl) + `appsettings*.json` (downstream-registry.md eşlemesi)
- [X] T006 [P] `src/agents/Mcp.Gateway/Dependencies/DependencyExtensions.cs` — Scrutor `AsSelfWithInterfaces` (concrete enjeksiyon; bkz 072 DI dersi)
- [X] T007 `src/agents/Mcp.Gateway/Program.cs` iskelet: ServiceDefaults + Options bind + `AddAllDependencies()` + iki `MapMcp` (/mcp + /mcp-admin) yer tutucu + `AddHttpContextAccessor`
- [X] T008 `src/aspire/AppHost/AppHost.cs`: `mcp-gateway` kaydı + tool topladığı BC'leri `WaitFor` (kozmetik; lazy keşif correctness'i garanti eder) + `AppHost.csproj` referansı
- [X] T009 `src/services/gateway/Gateway/appsettings*.json`: `/mcp/{**}` + `/mcp-admin/{**}` + `/.well-known/oauth-protected-resource/mcp*` rotalarını `mcp-gateway`'e yönlendir (tek dış giriş)

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ US1 dahil tüm story'ler bundan önce ilerleyemez.**

- [X] T010 **SPIKE (throwaway):** tek BC (ör. storefront) ile fasad `ListTools` + `CallTool` proxy uçtan uca + `mcp-remote` oturum-ortası **step-up login** çalışan sürüm doğrula (R1/R2). Bulguyu research.md'ye yaz; sürümü pinle. Fizibilite geçmezse tasarım revizyonu.
- [X] T011 `src/others/Identity.Server/Config.cs`: `external-customer-agent` seed (public+PKCE, Explicit consent, müşteri scope demeti: basket.read/write, order.read/write, customer.read, payment.read, storefront.read) + `mcp-gateway-discovery` (client_credentials; salt audience) — 070/061 dış-agent deseni
- [X] T012 `src/agents/Mcp.Gateway/Aggregation/DiscoveryTokenSource.cs` — keşif makine token'ı (client_credentials, cache; ChatAgent `DiscoveryTokenSource` emsali)

**Checkpoint**: Fizibilite + kimlik + gateway hazır — US1 başlayabilir.

---

## Phase 3: User Story 1 - Anonim gez+sepet, checkout'ta tek login (Priority: P1) 🎯 MVP

**Goal**: Tek müşteri MCP'si; girişsiz keşif+anonim sepet; checkout'ta tek step-up login + sepet devri; adres/kart seçimi; doğru yönlendirme.

**Independent Test**: Claude Desktop'a tek fasad; girişsiz ara+anonim sepete ekle; checkout → tek login → sepet devrolur → sipariş tamamlanır (quickstart Senaryo 1-3).

### Tests (Domain-TDD, ÖNCE — FAIL etmeli) ⚠️

- [X] T013 [P] [US1] `tests/Mcp.Gateway.Tests/ToolRoutingRegistryTests.cs` — ad→BC+yüzey çözme, bulunamayan ad, çift-sahip deterministik seçim (test-first)
- [X] T014 [P] [US1] `tests/Mcp.Gateway.Tests/SurfaceFilterTests.cs` — müşteri/admin yüzey süzme + çapraz sızıntı yok (test-first)

### Implementation

- [X] T015 [P] [US1] `src/agents/Mcp.Gateway/Routing/ToolRoutingRegistry.cs` (saf; ad→BC+yüzey)
- [X] T016 [P] [US1] `src/agents/Mcp.Gateway/Routing/SurfaceFilter.cs` (saf; müşteri/admin)
- [X] T017 [US1] `src/agents/Mcp.Gateway/Aggregation/ToolCatalogCollector.cs` — lazy `ListTools` (yüzeye uyan downstream'lere paralel, makine token'ı; kısa-TTL cache; erişilemez BC atla → registry kur) (discovery-proxy.md)
- [X] T018 [US1] `src/agents/Mcp.Gateway/Aggregation/ProxyToolInvoker.cs` — `CallTool` → `Resolve` → sahibi BC'ye **kullanıcı bearer / anonim X-User-Key** forward (PerUserMcpTool server ikizi; kullanıcı token'ı cache'lenmez)
- [ ] T019 [US1] `src/agents/Mcp.Gateway/Auth/AnonymousUserKeySource.cs` — oturuma bağlı opak `UserKey` üret/taşı (anonim sepet; X-User-Key yan yolu, 057/061)
- [X] T020 [US1] `Program.cs`: `MapMcp("/mcp")` müşteri yüzeyi — dinamik tool sağlayıcı (collector+filter), anonim bağlanma; `RequiresUserAuth` tool'da token yoksa **401 + PRM challenge** (step-up); `AddMcpResourceMetadata` müşteri scope demeti (Common)
- [ ] T021 [US1] Checkout step-up login sonrası **anonim sepet devri**: fasad login anında Basket `MergeFrom(anonUserKey → userId)` tetikler (057 merge; agent yolu açılır — [[anonymous-basket-chat-gap]])
- [ ] T022 [US1] Claude Desktop tek MCP kaydı (fasad `/mcp`, gateway üzerinden) — quickstart; mcp-remote çalışan sürüm

**Checkpoint**: Tek MCP + tek login (checkout) uçtan uca; anonim gez/sepet + sipariş. **MVP burada.**

---

## Phase 4: User Story 2 - Yönetim ayrı tek-kapı (Priority: P2)

**Goal**: `/mcp-admin` fasadı; yönetim tek-login; yüzey ayrımı (müşteri tool'ları görünmez).

**Independent Test**: `/mcp-admin`'e external-admin-agent ile tek login → yalnız yönetim tool'ları; müşteri `/mcp`'de yönetim tool'ları görünmez.

- [X] T023 [US2] `Program.cs`: `MapMcp("/mcp-admin")` yönetim yüzeyi — collector admin downstream'leri (catalog/stock/customer `/mcp-admin`, 070) toplar; `AddMcpAdminResourceMetadata` admin scope demeti; `external-admin-agent` (mevcut)
- [X] T024 [US2] `FacadeOption` admin downstream girdileri (Surface=admin) + registry admin yüzey süzme doğrulaması
- [ ] T025 [US2] Claude Desktop yönetim MCP kaydı (`/mcp-admin`) — ayrı tek login

**Checkpoint**: US1 + US2 bağımsız çalışır; yüzey sızıntısı yok.

---

## Phase 5: User Story 3 - Servis çevrimdışıyken kapı ayakta (Priority: P2)

**Goal**: Graceful degrade — bir BC düşünce fasad kalanları sunar; BC dönünce tool'ları geri gelir (lazy).

**Independent Test**: Bir BC durdur → ListTools kalanları döner, fasad çökmez; BC başlat → sonraki ListTools'ta tool'ları geri gelir (restart yok).

- [X] T026 [US3] `ToolCatalogCollector`: downstream erişilemez → o BC atlanır (graceful degrade, log); TTL sonrası yeniden dener (kalıcı kayıp yok — 069 snapshot tuzağı testi)
- [X] T027 [US3] `ProxyToolInvoker`: downstream 401/erişilemez → istemciye MCP tool-error (fasad çökmez, oturum düşmez)
- [ ] T028 [P] [US3] `tests/Mcp.Gateway.Tests/` — collector graceful-degrade + registry yeniden-toplama saf birim testi (mümkün olduğunca)

**Checkpoint**: Tüm story'ler bağımsız çalışır + dayanıklı.

---

## Phase 6: Polish & Cross-Cutting

- [X] T029 [P] `CLAUDE.md` BC/agents haritasına `mcp-gateway` satırı + `scripts/check-claude-spec-links.sh` geçir
- [X] T030 `dotnet test` — tüm birim testleri yeşil (İlke VI kapsamı) + regresyon (mevcut BC MCP + ChatAgent keşfi etkilenmez)
- [ ] T031 `quickstart.md` senaryolarını Claude Desktop'tan koş (canlı tur: anonim keşif→checkout tek login→yönlendirme→dayanıklılık→yüzey ayrımı)

---

## Dependencies & Execution Order

- **Setup (P1)**: bağımsız, hemen.
- **Foundational (P2)**: Setup'a bağlı; **tüm story'leri BLOKLAR**. T010 spike ÖNCE (fizibilite gate).
- **US1 (P3)**: Foundational'a bağlı. MVP.
- **US2 (P4)**: Foundational + US1 (fasad server iskeleti US1'de kurulur; admin yüzey ekler).
- **US3 (P5)**: US1 (collector/invoker US1'de kurulur; dayanıklılık davranışı ekler).
- **Polish (P6)**: istenen story'ler bitince.

### Story içi

- Domain-TDD: T013/T014 (US1) implementasyondan ÖNCE, FAIL etmeli.
- Registry/Filter (saf) → Collector/Invoker → Program map → anonim/step-up + merge.

### Parallel fırsatları

- Setup [P]: T003,T004,T005,T006.
- US1 [P] testler T013,T014; [P] saf birimler T015,T016.
- US3 [P]: T028.

---

## Implementation Strategy

- **SPIKE önce (T010)**: fizibilite (dinamik MCP server + step-up login) doğrulanmadan US1'e girme.
- **MVP**: Phase 1 → 2 → 3 (US1). DUR + doğrula (tek MCP + tek login + anonim sepet devri + sipariş).
- **Artımlı**: +US2 (admin kapı) → +US3 (dayanıklılık). Her biri bağımsız test.
- **En ağır alt-iş (T019/T021)**: anonim X-User-Key + login-merge agent yoluna açma ([[anonymous-basket-chat-gap]]).
- **Kapsam dışı**: ChatAgent söküm (ayrı feature); UCP REST kanalı (değişmez).