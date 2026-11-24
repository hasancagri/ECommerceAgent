---
description: "Task list — 028 Taste-vektör kişiselleştirme (Dilim-4) — B: ScoutAgent orkestrasyon (m2m YOK)"
---

# Tasks: Taste-vektör kişiselleştirme (Dilim-4)

**MİMARİ REVİZYON (B):** gRPC+m2m YERİNE ScoutAgent orkestrasyon. Personalization `get_my_taste` PERSONAL MCP
tool (user JWT, 013/GetSearchHistoryTool deseni) → ScoutAgent → Catalog `recommend_books(vector,count)` anonim
MCP tool. m2m/proto/gRPC YOK; mevcut token-forward + JWT. Taste vektör sunucu-içi taşınır (İlke VIII korunur).

**Tests**: İlke VI → saf birimler (TasteVector) test-first. MCP/kNN/endpoint = canlı.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup

- [ ] T001 [P] Personalization csproj: `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI` (MCP server + JWT zaten var) — `src/services/personalization/Personalization.Api/Personalization.Api.csproj`
- [ ] T002 AppHost: personalization-api'ye `scout-openai-key` env (`PersonalizationEmbeddingOptions__ApiKey`) — `src/aspire/AppHost/AppHost.cs`

## Phase 2: Foundational (taste hesap hattı)

- [ ] T003 [P] Test-first: `TasteVectorTests` — `Average` (boyut-başı ortalama, boş→boş, ağırlıklı), `IsColdStart(<3)` — `tests/Personalization.Api.Tests/TasteVectorTests.cs`
- [ ] T004 Saf `TasteVector.Average(IReadOnlyList<float[]>, weights?)` + `IsColdStart(signalCount, threshold=3)` — `src/services/personalization/Personalization.Api/Domains/Taste/TasteVector.cs`
- [ ] T005 [P] `PersonalizationEmbeddingOptions` + `EmbeddingClientFactory` (025 deseni, AYNI model text-embedding-3-small) + Program bind — `src/services/personalization/Personalization.Api/Options/…` + `Search/EmbeddingClientFactory.cs` + `Program.cs`
- [ ] T006 `ComputeTaste` query slice: userId → `UserSearchRecord` distinct sorgular → OpenAI batch embed → `TasteVector.Average` → `{vector, signalCount}` — `src/services/personalization/Personalization.Api/Domains/Taste/Features/Queries/ComputeTaste.cs`
- [ ] T007 Personalization `get_my_taste` PERSONAL MCP tool (currentUser.Load; GetSearchHistoryTool deseni) → ComputeTaste → `{vector, signalCount}` — `src/services/personalization/Personalization.Api/Domains/Taste/Features/Agents/Queries/GetMyTaste.cs`

---

## Phase 3: US1 — "Sana özel" kişisel öneri (P1) 🎯 MVP

**Goal**: ScoutAgent `/feed` → Personalization taste al → Catalog recommend kNN; cold/boş → küratörlü. `/feed` curl-kanıtlı.

**Independent Test**: ≥3 aramalı login `/feed` → anlam-merkezine yakın kartlar; <3/anonim → küratörlü, boş değil.

- [ ] T008 [US1] Catalog `RecommendQueries` ham-SQL: verilen taste-vektörle kNN (`product_embedding <=> $vec::vector`, VectorLiteral 025/026) + hard filtre (satılabilir/dil/seviye) + `DISTINCT ON WorkKey` + çeşitlilik → LIMIT; **boş vektör → küratörlü** (popülerlik browse, 018 yolu) — `src/services/catalog/Catalog.Api/Search/RecommendQueries.cs`
- [ ] T009 [US1] Catalog `RecommendBooks` slice + `[McpServerToolType]` `recommend_books(float[] taste, int count)` (ANONİM; boş taste → küratörlü); Response=SemanticSearch.BookItem şekli — `src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Queries/RecommendBooks.cs`
- [ ] T010 [P] [US1] ScoutAgent ayna-tipleri: `TasteResult{vector,signalCount}` + `RecommendBooksResult{items,total}` — `src/agents/ScoutAgent.Api/Agents/ToolResults.cs`
- [ ] T011 [US1] ScoutAgent `GET /api/chat/v1/feed` (login `RequireAuthorization`): `get_my_taste` MCP (token forward) → signalCount<3/boş ise `recommend_books([])` (küratörlü) değilse `recommend_books(vector)` → `ChatResponse{reply:{text,items}}`. Vektör LOGLANMAZ (İlke VIII) — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Queries/Feed.cs` + `ConversationEndpointExtension.cs`
- [ ] T012 [P] [US1] React: `recommend_books` render case (search_books case'ine; `{items}`) — `~/dev/ECommerceWeb/src/pages/ChatWorkspace.tsx` _(feed GİRİŞ ekranı AYRI dilim — /feed curl ile test)_
- [ ] T013 [US1] Canlı: ≥3 aramalı login `/feed` → anlamlı kartlar; <3/anonim → küratörlü (quickstart)

**Checkpoint**: Kişisel feed uçtan-uca (backend); cold-start fallback.

---

## Phase 4: US2 — Spesifik sorgu kişiselliği ezer (P2)

- [ ] T014 [US2] Canlı doğrulama: "Dostoyevski"/"karanlık roman" → mevcut search/semantic (taste yok); `/feed` ayrı yol. Regresyon yok (SC-002). Kod gerektirmez.

## Phase 5: US3 — Sinyal biriktikçe güçlenir (P3)

- [ ] T015 [US3] `TasteVector.Average` ağırlıklı girdi (satın-alma/yorum kitap-embed yüksek ağırlık) — SAF, T003 kapsar; canlı tüketici (OrderCompleted/Review) BU DİLİMDE YOK (0 veri, kablo=imza)
- [ ] T016 [US3] Canlı: yeni arama sonrası `/feed` yeni sorguyu yansıtır (on-demand recompute)

## Phase 6: Polish

- [ ] T017 [P] FLOW.md as-built: Personalization (taste + get_my_taste) + Catalog (recommend_books) + ScoutAgent (feed) — ilgili FLOW.md'ler
- [ ] T018 İlke VIII denetimi: taste-vektör hiçbir REST/MCP dönüşünde kullanıcıya/loga çıkmıyor (get_my_taste yalnız ScoutAgent'a, oradan Catalog'a; feed yanıtı yalnız kartlar) + `dotnet build && dotnet test` yeşil + quickstart SC-001..004

---

## Dependencies & Sıra
- Setup (T001-002) → Foundational (T003-007: TasteVector→embedding→ComputeTaste→get_my_taste) → US1 (T008-013).
- US1 MVP; US2 (T014) doğrulama; US3 (T015-016); Polish son.

## Paralel
T001 [P] · T003 [P] · T010/T012 [P] · T017 [P].

## MVP
**US1 (T001-013)** — `/feed`: get_my_taste → recommend_books kNN + cold-start küratörlü, curl-kanıtlı. m2m/proto yok.
