---
description: "Task list — ScoutAgent Router → Sub-Agent Routing"
---

# Tasks: ScoutAgent Router → Sub-Agent Routing

**Input**: Design documents from `/specs/019-scout-subagent-routing/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: İlke VI (Domain-TDD) — saf birimler (registry invariant'ı, model çözümleme) test-first;
LLM/MCP/agent-kurulum yolları harness'sız (canlı doğrulama, mevcut kural).

**Yol**: tüm değişiklik `src/agents/ScoutAgent.Api/` + `tests/ScoutAgent.Api.Tests/`.

## Phase 1: Setup

- [X] T001 İskelet dosyaları oluştur: `Agents/SubAgentDefinitions.cs`, `Agents/ChatClientFactory.cs`
  (doğrudan gerçek içerikle kuruldu)

---

## Phase 2: Foundational (tüm story'leri bloklar)

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz.

- [~] T002 **D1/D4 SPIKE** — akıl-yürütmeyle yapıldı: `AsAIFunction` eager kurar (SC-001 bozar) →
  lazy `AIFunctionFactory.Create`'e düzeltildi (research D1 BULGU). D4a uygulandı (ResponseFormat yok).
  **Runtime iç içe tool döngüsü + D4 kararı CANLI (T011) bekliyor** — kod derleniyor.
- [X] T003 `ScoutOptions`'a `RouterModel` + `Agents` (Dictionary) eklendi — `Options/ScoutOptions.cs`
- [X] T004 [P] Saf test: `SubAgentRegistryTests.cs` (7 kayıt, Key tekil, endpoint eşleşme) — yeşil
- [X] T005 `SubAgentDefinitions.cs` — 7 BC kaydı + dar prompt'lar + ResolveModel/ResolveRouterModel/Validate
- [X] T006 `McpToolSource.GetToolsForEndpointAsync` — TEK uçtan tool getir (kör `GetToolsAsync` kaldırıldı)
- [X] T007 `ChatClientFactory` — model-keyed `IChatClient` (Scrutor-auto); Program.cs tek IChatClient kaldırıldı

**Checkpoint**: registry + per-endpoint fetch + model-keyed factory + options hazır.

---

## Phase 3: User Story 1 - Niyete göre seçici bağlanma (P1) 🎯 MVP

**Goal**: Router niyeti anlar, ilgili sub-agent'a dağıtır; yalnız o sub-agent'ın MCP ucu bağlanır.

**Independent Test**: Anon "Dostoyevski" araması → yalnız `catalog` ucu bağlanır (diğer 6 uç handshake
YOK), kart cevabı bugünküyle eşdeğer.

- [X] T008 [US1] `ScoutAgentFactory`: 7 sub-agent LAZY tool (`AIFunctionFactory.Create` —
  `MakeSubAgentTool`) → router `ChatClientAgent`. Seçilmeyen uç bağlanmaz.
- [X] T009 [US1] `ToolResultCollector` sub-agent'ın leaf MCP tool'larına threadlendi (CapturingFunction
  via GetToolsForEndpointAsync) → `reply.items` yaprak sonuçlardan, çağrı sırasıyla (D5)
- [X] T010 [US1] `SendMessage.Handle`: `CreateRouter(collector)` çağrılıyor; kör `GetToolsAsync`+`CreateAgent`
  yolu + McpToolSource bağımlılığı kaldırıldı; `reply.items` akışı korundu
- [ ] T011 [US1] **CANLI (Aspire, quickstart S1/S2):** anon kitap arama → yalnız `catalog` ucu bağlanır
  (diğer 6 handshake YOK); kart cevabı eşdeğer (SC-001, SC-002) — kullanıcı sürer

**Checkpoint**: Seçici routing çalışır (tek varsayılan modelle). MVP.

---

## Phase 4: User Story 2 - Alan başına farklı model (P2)

**Goal**: Router ucuz model, sub-agent'lar kendi modeliyle.

**Independent Test**: `RouterModel` + `Agents:catalog` farklı atanır → bir turda router ≠ catalog
sub-agent modeli (log/iz).

- [X] T012 [P] [US2] Saf test: `ModelResolutionTests.cs` (5 test) — yeşil
- [X] T013 [US2] `ScoutAgentFactory`: router `ResolveRouterModel`, sub-agent `ResolveModel(Key)` →
  `ChatClientFactory.ForModel`
- [ ] T014 [US2] **CANLI (quickstart S4):** router ≠ sub-agent modeli iz/log'da (SC-003) — kullanıcı sürer

**Checkpoint**: US1 + US2 bağımsız çalışır.

---

## Phase 5: User Story 3 - Giriş gerektiren alanlarda kimlik taşıma (P2)

**Goal**: Login kişisel sorular ilgili sub-agent'a; bearer taşınır; anon kişisel veri döndürmez.

**Independent Test**: Login "siparişlerim" → yalnız `order` ucu, token taşınır, siparişler döner;
tokensız aynı istek kişisel veri döndürmez, girişe yönlendirir.

- [X] T015 [US3] Login sub-agent'lar tek token-taşıyan `"mcp"` HttpClient'ı kullanır
  (`GetToolsForEndpointAsync` → `CreateClientAsync(HttpClientName)`); bearer `UserTokenForwardingHandler`
  ile downstream'e — değişmedi, korundu
- [ ] T016 [US3] **CANLI (quickstart S3/S5):** login → kişisel kart; anon → veri yok + nazik
  yönlendirme; BC down → tur tamamlanır (SC-004, SC-005) — kullanıcı sürer

**Checkpoint**: 3 user story bağımsız çalışır.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T017 **CANLI (quickstart S6, FR-010):** "sepetim ve Dostoyevski" → iki alan, `items` sıralı birleşir — kullanıcı sürer
- [ ] T018 **CANLI (quickstart S7, FR-009):** kart reload + `UserSearched` login+dolu-sorgu invariant — kullanıcı sürer
- [ ] T019 `FLOW.md` as-built — D3 hotspot `open`→`closed`, `_(planned)_` düşer, kenar-anchor eklenir
  (**T011 canlı GEÇİNCE** — as-built gerçeği doğrulanınca)
- [X] T020 Ölü kod: kör `McpToolSource.GetToolsAsync` kaldırıldı; tek-düz-agent `CreateAgent` yolu gitti
- [X] T021 [P] `dotnet build` 0 hata + `dotnet test` 29/29 yeşil; dev "Kasif" `SystemPrompt`
  SubAgentDefinitions'a parçalandı (ScoutAgentFactory'de RouterPrompt kaldı)

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002-T007)** → user story'ler.
- **T002 spike** EN ÖNCE — D4 vs D4a kararını verir (tipli çıktı yolu).
- **T004 (test)** → T005; **T012 (test)** → T013 (İlke VI).
- **US1 (T008-T011)** foundational sonrası; MVP. **US2/US3** US1'e dayanır (aynı ScoutAgentFactory) ama
  bağımsız doğrulanır.
- **Polish (T017-T021)** tüm story'ler sonrası.

### Parallel Opportunities
- T004 (test) T003/T005 ile farklı dosya → paralel yazılabilir.
- T012 (test) US1 bitince paralel.
- T021 polish sonunda tek.

## Implementation Strategy

**MVP = US1**: Setup + Foundational + Phase 3 → seçici routing (tek model) çalışır, SC-001/SC-002
doğrulanır → dur/demo. Sonra US2 (model çeşitliliği), US3 (kişisel/token). Her story bağımsız eklenir.

**Risk kapısı**: T002 spike D4a'ya düşerse (tipli çıktı + tool çakışır) sub-agent düz metin döner,
yapı MCP structuredContent'ten `items`'a akar — US1 çıktısı değişmez, SC'ler yine karşılanır.
