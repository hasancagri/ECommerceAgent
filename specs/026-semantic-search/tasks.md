---
description: "Task list — 026 Semantik Arama (Dilim-2)"
---

# Tasks: Semantik Arama (Dilim-2)

**Input**: `/specs/026-semantic-search/` (plan.md, spec.md, research.md, data-model.md, contracts/)

**Tests**: İlke VI (Domain-TDD) → saf birimler (routing niyet-ayrımı, vektör-literal) test-first + önce sıralı.
Raw-SQL / MCP tool / endpoint / React = test-sonra/canlı (quickstart).

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: paralel (farklı dosya, bağımsız)

---

## Phase 1: Setup

- [ ] T001 [P] `SemanticSearchOptions` (eşik `MaxCosineDistance`=0.68 + `DefaultCount`/`MaxCount` + `ClampCount`) — `src/services/catalog/Catalog.Api/Search/SemanticSearchOptions.cs` (SimilarBooksOptions deseni)
- [ ] T002 Program.cs'e `SemanticSearchOptions` bind + singleton unwrap — `src/services/catalog/Catalog.Api/Program.cs` (SimilarBooksOptions kaydı deseni)

## Phase 2: Foundational (tüm story'lerin ön koşulu)

- [ ] T003 [P] Saf `VectorLiteral.From(ReadOnlySpan<float>)` helper'ı çıkar (`[f1,f2,...]` InvariantCulture) — `src/services/catalog/Catalog.Api/Search/VectorLiteral.cs`; 025 `EmbeddingBackfillWorker.ToVectorLiteral`'ı buna refactor et (tek kaynak)
- [ ] T004 [P] Test-first: `VectorLiteralTests` (boş/tek/çok eleman, "." ondalık, format) — `tests/Catalog.Api.Tests/VectorLiteralTests.cs`

---

## Phase 3: US1 — Betimleyici sorgu anlamla eşleşir (P1) 🎯 MVP

**Goal**: Catalog `semantic_search(query)` — embed → kNN → eşik → künye. Filtre YOK (US3), routing YOK (US2); tool elle/MCP-inspector ile test edilebilir.

**Independent Test**: `semantic_search` tool'unu betimleyici sorguyla çağır (ScoutAgent'tan bağımsız) → anlamca ilgili kartlar, eşik dışı boş.

- [ ] T005 [US1] `SemanticSearchQueries` ham-SQL yürütücü: sorgu embed (`EmbeddingClientFactory`, 025) → `product_embedding` kNN `embedding <=> $vec::vector` + `WHERE <=> < MaxCosineDistance` + `ORDER BY <=> LIMIT count` → sıralı id → `ResolveOrderedAsync` künye — `src/services/catalog/Catalog.Api/Search/SemanticSearchQueries.cs` (ProductSearchQueries + VectorLiteral deseni; `IScopedDependency`)
- [ ] T006 [US1] `SemanticSearch` query slice + `[McpServerToolType]` `semantic_search(query, count)` tool (ince sarmalayıcı → `IMessageBus.InvokeAsync`); `Response`/`BookItem` = SimilarBooks şekli — `src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Queries/SemanticSearch.cs`
- [ ] T007 [US1] Canlı duman: Aspire → MCP `semantic_search("karanlık distopya")` → anlamlı kartlar; eşik dışı sorgu boş (quickstart kNN SQL ile eşik hissi)

**Checkpoint**: Semantik motor tek başına çalışır (ScoutAgent'sız).

---

## Phase 4: US2 — Niyet doğru yola gider (P2)

**Goal**: ScoutAgent betimleyici sorguyu `SemanticQuery`'ye ayırır, `/search` üçlü dal `semantic_search`'e yönlendirir.

**Independent Test**: "Suç ve Ceza"→lexical · "kayıp üstüne sakin kitap"→semantic (dönüş `tool` adından).

- [ ] T008 [P] [US2] Test-first: `SemanticIntentTests` — `SemanticQuery` dolu+demir yok → semantic; demir var → değil — `tests/ScoutAgent.Api.Tests/SemanticIntentTests.cs`
- [ ] T009 [US2] Saf `SemanticIntent.IsSemantic(BookSearchFilters)` — `src/agents/ScoutAgent.Api/Search/SemanticIntent.cs`
- [ ] T010 [US2] `BookSearchFilters`'a `SemanticQuery` slotu (SimilarToTitle deseni) — `src/agents/ScoutAgent.Api/Search/BookSearchFilters.cs`
- [ ] T011 [US2] `ExtractPrompt`'a betimleyici-niyet kuralı (başlık/yazar değil, "ne hakkında/nasıl hissettiren" → SemanticQuery, Text boş) — `src/agents/ScoutAgent.Api/Search/ExtractPrompt.cs`
- [ ] T012 [P] [US2] `SemanticSearchResult` ayna-tipi (`Items`/`Total`) — `src/agents/ScoutAgent.Api/Agents/ToolResults.cs`
- [ ] T013 [US2] `SearchTyped` üçlü dal: `SemanticQuery` dolu → `semantic_search` · SimilarToTitle → similar_books · else → search_books — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SearchTyped.cs`
- [ ] T014 [P] [US2] React render case `'semantic_search'` (search_books case'ine ekle; `{items}` aynı şekil) — `~/dev/ECommerceWeb/src/pages/ChatWorkspace.tsx`
- [ ] T015 [US2] Canlı: React Kâşif "kayıp üstüne sakin bir kitap" → `semantic_search` kartları; "Suç ve Ceza" → lexical

**Checkpoint**: Routing + semantik uçtan-uca (betimleyici sorgu kart basar).

---

## Phase 5: US3 — Yapısal filtre anlamla birlikte (P3)

**Goal**: Betimleyici sorguda yapısal kısıt (stokta/dil/fiyat/sayfa/yayınevi) kNN'e jsonb pre-filter.

**Independent Test**: "stokta karanlık bir roman" → dönen her kitap stokta.

- [ ] T016 [US3] `semantic_search` tool'una opsiyonel filtre param'ları (onlyInStock/language/min-maxPrice/min-maxPageCount/publisher) + `SemanticSearchQueries`'e jsonb pre-filter (aday daralt → kNN; publisher ad→id `ResolvePublisherIdsAsync` reuse) — `SemanticSearch.cs` + `SemanticSearchQueries.cs`
- [ ] T017 [US3] ScoutAgent `SearchTyped` semantik dalında `BookSearchFilters` yapısal alanlarını (Flags/PriceHint/PageHint/Publisher/Language) `semantic_search` arg'larına geçir (FilterInterpreter mantığı reuse) — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SearchTyped.cs`
- [ ] T018 [US3] Canlı: "stokta karanlık bir roman" → hepsi stokta + anlamca uygun

**Checkpoint**: Filtre+anlam birlikte.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T019 [US1] `semantic_search` login'de `UserSearched(mode="semantic")` yayınlar (ayrık + hata-yutar; 012/014 deseni) — `SemanticSearch.cs` tool/handler
- [ ] T020 [P] FLOW.md as-built: Catalog Dilim-2 storm → gerçek (anchor + hotspot closed) — `src/services/catalog/FLOW.md`; ScoutAgent routing seam — `src/agents/ScoutAgent.Api/FLOW.md`
- [ ] T021 `dotnet build && dotnet test` yeşil + quickstart US1-3/SC-001..004 canlı doğrulama

---

## Dependencies & Sıra

- **Setup (T001-002)** → her şeyden önce.
- **Foundational (T003-004)** → VectorLiteral US1'in SQL'inde lazım.
- **US1 (T005-007)** = MVP; Setup+Foundational sonrası. Tek başına değer (motor).
- **US2 (T008-015)** → US1 tool'u gerektirir (routing ona yönlendirir).
- **US3 (T016-018)** → US1 (tool genişler) + US2 (ScoutAgent filtre geçişi) sonrası.
- **Polish (T019-021)** → en son.

## Paralel fırsatlar

- T001 ∥ T003/T004 (farklı dosya).
- US2: T008/T012/T014 [P] (test ∥ mirror ∥ React — farklı repo/dosya).
- T020 [P] (FLOW, koddan bağımsız).

## MVP

**US1 (T001-007)** — Catalog `semantic_search` motoru tek başına. Betimleyici sorgu anlamla eşleşir
(ScoutAgent'tan bağımsız MCP ile kanıtlanır). Routing (US2) + filtre (US3) artımlı eklenir.
