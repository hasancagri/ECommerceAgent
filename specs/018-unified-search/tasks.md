---
description: "Task list — Unified Catalog Search"
---

# Tasks: Unified Catalog Search

**Input**: `specs/018-unified-search/` (plan.md, spec.md, research.md, data-model.md, contracts/search.md, quickstart.md)

**Tests**: İlke VI (Domain-TDD) — saf birimler (mod/sıra çözümü, BM25 sanitize/stopword, filtre→predicate) test-first; test task'ı implementasyondan ÖNCE. SQL/index/endpoint = canlı doğrulama (host harness yok).

**Kritik kesme sırası**: Türkçe ICU + fast-field index (Phase 2) naive yolun kaldırılmasından (Phase 3, US1) ÖNCE. Semantik söküm (Phase 5) birleşik yol kanıtlandıktan SONRA.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

**Purpose**: Sürüm/sözdizimi teyidi (research FLAG'leri).

- [x] T001 Aspire'ı kaldır (`dotnet run --project src/aspire/AppHost/AppHost.csproj`), kurulu `pg_search` sürümünü + fast-field jsonb ve `text_fields` ICU sözdizimini canlı teyit et; bulguları `specs/018-unified-search/research.md` FLAG'lerine işle.
- [x] T002 [P] `SearchRequest` + `SearchMode` tiplerini `data-model.md`'ye göre `src/services/catalog/Catalog.Api/Search/SearchRequest.cs` içinde tanımla (query DTO + türetilmiş mod enum; davranış yok).

---

## Phase 2: Foundational (Blocking) — index + saf birimler + birleşik yürütücü

**⚠️ CRITICAL**: US1/US2 başlamadan bitmeli. Türkçe ICU + fast-field BURADA (regresyon önleme).

### İndeks (cutover-önce)

- [x] T003 `ProductSearchSchema.cs`'te BM25 index'i fast-field'lerle genişlet: `Price.Amount`, `PageCount`, `PublishedOn`, `Binding`, `PublisherId`, `CategoryId`, `StockCount`, `DiscountRate`, `PurchaseCount` (jsonb yolları); DDL idempotent, satır-bağımsız. (`src/services/catalog/Catalog.Api/Search/ProductSearchSchema.cs`)
- [x] T004 `ProductSearchSchema.cs`'te text field'lere (`Name`/`Description`/`Tags.Value`) Türkçe/ICU tokenizer+normalizer (noktasız-ı katlama, lowercase) yapılandır; Aspire'da "tarih"/"tarıh" eşleşmesini canlı doğrula (SC-004).

### Saf birimler — TEST-FIRST (İlke VI)

- [x] T005 [P] Test: mod/sıra çözümü (`Text` dolu∧`Sort` boş→Relevance; `Sort` dolu→FieldSort; ikisi boş→DefaultBrowse=popülerlik) — `tests/Catalog.Api.Tests/SearchModeResolutionTests.cs` (FAIL).
- [x] T006 [P] Test: BM25 terim sanitize + stopword eleme (apostrof/özel karakter, ≤2 harf, Türkçe dolgu/soru) — `tests/Catalog.Api.Tests/Bm25TermSanitizeTests.cs` (FAIL).
- [x] T007 [P] Test: filtre→SQL predicate/parametre kurma (DB'siz; her filtre doğru koşul+parametre) — `tests/Catalog.Api.Tests/SearchPredicateBuilderTests.cs` (FAIL).
- [x] T008 Mod/sıra çözüm fonksiyonunu implemente et (T005 yeşil) — `src/services/catalog/Catalog.Api/Search/SearchMode.cs` (saf).
- [x] T009 BM25 sanitize+stopword'ü implemente et (T006 yeşil); mevcut `ProductSearchQueries` stopword/SanitizeBm25 mantığını saf birime taşı — `src/services/catalog/Catalog.Api/Search/Bm25Terms.cs`.
- [x] T010 Filtre→predicate kurucusunu implemente et (T007 yeşil) — `src/services/catalog/Catalog.Api/Search/SearchPredicateBuilder.cs` (saf: SearchRequest→WHERE parça+parametre listesi).

### Birleşik yürütücü

- [x] T011 `ProductSearchQueries.cs`'e TEK birleşik metot ekle: `SearchAsync(SearchRequest)` → tek SQL (metin? `@@@` + predicate WHERE + mod'a göre ORDER BY [`pdb.score`|fast-field|`PurchaseCount DESC`] + LIMIT/OFFSET) → sıralı id listesi; `ResolveOrderedAsync` ile künye. (`src/services/catalog/Catalog.Api/Search/ProductSearchQueries.cs`)

**Checkpoint**: İndeks fast-field+ICU hazır, saf birimler yeşil, birleşik yürütücü çağrılabilir.

---

## Phase 3: User Story 1 - Tek arama girişi (Priority: P1) 🎯 MVP

**Goal**: Basit ("şu yazarın kitabı") ve kompleks (filtreli) tüm aramalar tek endpoint + tek executor; naive bellekte-filtre kalkar.

**Independent Test**: Basit yazar sorgusu + çok-kısıtlı filtreli sorgu aynı uçtan, aynı künye biçiminde, doğru sonuç döner (quickstart Senaryo 1).

- [x] T012 [US1] REST `GET /api/catalog/products/search`'i birleşik executor'a bağla: `mode` parametresini KALDIR ve `q`'yu doğrudan birleşik yola yönlendir (mode'suz); tüm filtre/sort/sayfalama parametrelerini `SearchRequest`'e eşle (`SearchParameters` ile normalize). SINIR: bu task yalnız endpoint'i birleşik yola taşır; ölü `mode=semantic|hybrid` dal/branch KODUNU silmez — o T024'e aittir. (`src/services/catalog/Catalog.Api/Domains/Products/ProductEndpointExtension.cs`)
- [x] T013 [US1] `Features/Queries/SearchProducts.cs`'i birleşik sorguya indir (keyword yolu→unified; `SearchMode` keyword/semantic ayrımı kalkar, tek yol). (`src/services/catalog/Catalog.Api/Domains/Products/Features/Queries/SearchProducts.cs`)
- [x] T014 [US1] MCP `search_books` tool'unu birleşik executor'a bağla; `SearchBooks.cs` naive bellekte-filtre mantığını (aday yükle + LINQ) SİL, mevcut filtre parametrelerini `SearchRequest`'e devret. **YENİ `category` param ekle:** tool imzasına `[Description("Kategori filtresi — tek-leaf CategoryId ya da kategori adı")] ...` parametresi + `SearchRequest.CategoryId` eşlemesi (FR-002/FR-012 — kategori yüzeye bu param'la çıkar; LLM niyeti bu param'ı doldurur). (`src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Queries/SearchBooks.cs`)
- [x] T015 [US1] Her iki yüzeyin de aynı `ProductSummary` künyesini döndürdüğünü garanti et (yazar/yayınevi ad çözümü tek yerde). (`src/services/catalog/Catalog.Api/Search/ProductSummary.cs` + çağıranlar)
- [x] T016 [US1] Canlı doğrula (quickstart Senaryo 1+3): Dostoyevski basit + çok-kısıtlı filtreli; boş sorgu→filtre-only/boş; metin+sort yok→popülerlik.

**Checkpoint**: Tek arama girişi çalışır; basit ve kompleks aynı yol. MVP.

---

## Phase 4: User Story 2 - Ölçekte doğru sonuç (Priority: P1)

**Goal**: Milyonlarca kayıtta aday kümesi belleğe yüklenmeden yalnız sayfa döner (Top-K + fast-field).

**Independent Test**: Büyük kümede filtreli+sıralı sorgu yalnız sayfa döndürür; süreç belleği şişmez; yanıt süresi kabul sınırında (quickstart Senaryo 2).

- [x] T017 [US2] Birleşik yolda hiçbir tam-nesne aday yükleme kalmadığını doğrula: filtre/sort/sayfalama tamamen SQL'de (WHERE/ORDER BY/LIMIT), yalnız sayfa id'leri `ResolveOrderedAsync`'e gider. Artık `IsOneOf(orderedArr)` tam-yükle deseni yoksa onayla/sök. (`ProductSearchQueries.cs` + `SearchBooks.cs`)
- [x] T018 [US2] Ölçek canlı doğrula (quickstart Senaryo 2): büyük veri (≥1M ya da en büyük mevcut) filtreli+sıralı sorgu; yalnız sayfa, süre/bellek kabul; `total` doğru.

**Checkpoint**: US1 + US2 birlikte; tek yol hem doğru hem ölçeklenir.

---

## Phase 5: Semantik Altyapı Söküm (FR-013 / SC-006)

**Purpose**: Birleşik yol kanıtlandıktan sonra ölü semantik alt-sistemi kaldır (ölü kod/tablo/config bırakma).

- [x] T019 [P] `Search/Embedding/` klasörünü sil (QueryEmbeddingClient, OpenAiEmbeddingOptions). (`src/services/catalog/Catalog.Api/Search/Embedding/`)
- [x] T020 [P] `Search/RrfFusion.cs`'i sil. (`src/services/catalog/Catalog.Api/Search/RrfFusion.cs`)
- [x] T021 [P] `Features/Queries/HybridSearch.cs` + `Features/Queries/SimilarProducts.cs` + `Features/Commands/IngestEmbeddings.cs`'i sil. (`src/services/catalog/Catalog.Api/Domains/Products/Features/`)
- [x] T022 `ProductSearchQueries.cs`'ten `SemanticSql`/`SimilarSql`/`SemanticAsync`/`SimilarAsync`/`ToVectorLiteral`'ı sök. (`src/services/catalog/Catalog.Api/Search/ProductSearchQueries.cs`)
- [x] T023 `ProductSearchSchema.cs`'ten `product_embedding` tablosu + HNSW index + `CREATE EXTENSION vector`'ı kaldır; mevcut DB'de `DROP TABLE IF EXISTS ... product_embedding` + index drop (idempotent). (`src/services/catalog/Catalog.Api/Search/ProductSearchSchema.cs`)
- [x] T024 `ProductEndpointExtension.cs`'ten `/search?mode=semantic|hybrid` dalını, `GET /{isbn}/similar`'ı ve `POST /api/admin/catalog/embeddings` grubunu sil; `GET /{isbn}` nokta-fetch'i KORU. (`src/services/catalog/Catalog.Api/Domains/Products/ProductEndpointExtension.cs`)
- [x] T025 DI/Options/GlobalUsings temizliği: `OpenAiEmbeddingOptions` bind + embedding-ilgili `using`/kayıtları kaldır; `IngestEmbeddings` gateway rotası varsa Gateway config'ten düş. (`Program.cs`, `GlobalUsings.cs`, `src/others/Gateway`)
- [x] T026 Söküm doğrula (quickstart Senaryo 5): `dotnet build` 0 hata, Aspire boot temiz, `product_embedding` tablosu yok, `grep -r "Embedding\|RrfFusion\|Semantic\|SimilarProducts\|HybridSearch\|pgvector" src/services/catalog` canlı referans yok.

**Checkpoint**: Semantik tamamen söküldü; sistem BM25 tek motorla çalışır.

---

## Phase 6: Polish & Cross-Cutting

- [x] T027 [P] Catalog `FLOW.md` kenar-anchor'larını güncelle (silinen semantik slice'lar, birleşik arama); domain süreci değişmediyse yalnız anchor düzelt. (`src/services/catalog/FLOW.md`)
- [x] T028 [P] `CLAUDE.md` BC haritası + Yüzey modeli: arama = BM25 tek motor; semantik/embedding/RRF/pgvector söküldü satırlarını hizala. (`CLAUDE.md`)
- [x] T029 `dotnet build && dotnet test` yeşil (commit-öncesi); tüm saf birim testleri geçer.
- [x] T030 Tam quickstart.md doğrulaması (Senaryo 1-6) Aspire'da elle yürütülür; sonuçlar not edilir.

---

## Dependencies & Execution Order

- **Phase 1 Setup** → bağımsız.
- **Phase 2 Foundational** → Setup sonrası; US1/US2'yi BLOKLAR. İndeks (T003/T004) US1'den ÖNCE (cutover sırası, regresyon).
- **Phase 3 US1** → Foundational sonrası. MVP.
- **Phase 4 US2** → aynı birleşik yolu kullanır; US1 ile paylaşımlı, çoğu doğrulama. US1 sonrası.
- **Phase 5 Söküm** → birleşik yol (US1/US2) kanıtlandıktan SONRA (erken sökersen çalışan yol kalmaz).
- **Phase 6 Polish** → tümü sonrası.

### Within stories
- İlke VI: saf test (T005-T007) implementasyondan (T008-T010) ÖNCE.
- İndeks/executor (Foundational) → endpoint/tool bağlama (US1) → ölçek doğrulama (US2).

### Parallel Opportunities
- T005/T006/T007 (ayrı test dosyaları) [P] birlikte.
- T019/T020/T021 (ayrı dosya silmeleri) [P] birlikte.
- T027/T028 (FLOW.md / CLAUDE.md) [P] birlikte.

---

## Implementation Strategy

**MVP** = Phase 1 + 2 + 3 (US1). Durdur + doğrula: tek giriş basit+kompleks çalışıyor mu. Sonra US2 (ölçek doğrulama), sonra Söküm, sonra Polish.

**Kesme güvenliği**: İndeks ICU+fast-field (T003/T004) naive silme (T014) ÖNCESİ bitmeli — yoksa Türkçe regresyon (SC-004). Semantik söküm en sonda, çalışan birleşik yol üstüne.

## Notes
- [P] = farklı dosya, bağımsız.
- SQL/index/endpoint canlı doğrulanır (İlke VI harness kurmaz); saf birimler test-first.
- Her task ya da mantıksal grup sonrası commit.
