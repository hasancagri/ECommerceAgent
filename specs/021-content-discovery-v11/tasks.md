# Tasks: İçerik-Temelli Keşif Omurgası (v11 Katalog)

**Input**: specs/021-content-discovery-v11/ (spec, plan, research, data-model, contracts, quickstart)
**Branch**: `feat/021-content-discovery-v11` · Her faz sonu: `dotnet build && dotnet test` YEŞİL.

## Phase 1: Setup

- [ ] T001 Branch + spec artefaktlarını commit'le (specs/021-*, FLOW.md mini-storm, feature.json)

## Phase 2: Foundational (BLOKLAYICI — tüm story'ler bunun üstüne)

**Taksonomi sözlüğü (test-first, İlke VI)**

- [ ] T002 [P] BookTaxonomyTests yaz (sözlük bütünlüğü, IsValid*, CompatibleAudienceLevels grupları, "belirsiz") — tests/Catalog.Api.Tests/BookTaxonomyTests.cs
- [ ] T003 BookTaxonomy static holder (PDF §19 listeleri: Genres/Moods/AudienceLevels/Themes FrozenSet + Unknown + uyum grupları) — src/services/catalog/Catalog.Api/Domains/Products/BookTaxonomy.cs

**Kategori + Tags sökümü (tek derlenen blok, tek commit)**

- [ ] T004 Product'tan CategoryId/RelatedCategoryIds/Tags + SetCategory/SetRelatedCategories/SetTags çıkar; BookTag VO sil — Domains/Products/Product.cs + ValueObjects/ProductValueObjects.cs
- [ ] T005 Domains/Categories/ + Helpers/CategoryNaming.cs sil; MartenExtensions Category kayıt/index çıkar — Extensions/MartenExtensions.cs
- [ ] T006 Import'tan kategori/etiket oku-çöz çıkar (ResolveCategory, FallbackCategoryName, ParseCategoryPath, Etiketler) — Process/ImportJobWorker.cs + Helpers/ExcelRowMapper.cs
- [ ] T007 Search'ten CategoryId + Tags.Value çıkar — Search/SearchRequest.cs + SearchPredicateBuilder.cs + ProductSearchQueries.cs
- [ ] T008 Endpoint/MCP'den category çıkar (categoryId param, GetBook zinciri, SearchBooks category+ResolveCategoryIdAsync) — ProductEndpointExtension.cs + Features/Queries/GetBook.cs + Features/Agents/Queries/SearchBooks.cs
- [ ] T009 ProductChangedEvent kategori alanlarını çıkar + yayıncılarını düzelt — src/others/Shared/IntegrationEvents.cs + Catalog yayın noktaları
- [ ] T010 ScoutAgent'tan kategori izlerini çıkar (CatalogSubAgent wrapper/prompt, ToolResults mirror) — src/agents/ScoutAgent.Api/Agents/SubAgents/CatalogSubAgent.cs + Agents/ToolResults.cs
- [ ] T011 Ölü testleri sil/güncelle (CategoryChainResolverTests, CategoryTests, BookTagTests, ProductTagTests; Product/Search/Mapper/ImportJob kategori-etiket case'leri) — tests/Catalog.Api.Tests/ → build+test yeşil, söküm commit'i

**Product enrichment + WorkKey (test-first)**

- [ ] T012 [P] ProductTests'e SetEnrichment case'leri yaz (geçersiz→belirsiz düşme, kardinalite kırpma, confidence clamp) — tests/Catalog.Api.Tests/ProductTests.cs
- [ ] T013 [P] WorkKeyNormalizerTests yaz (parantez atma, Türkçe fold, yazar sıralama, boşluk; `dostoyevski|suc ve ceza` örneği) — tests/Catalog.Api.Tests/WorkKeyNormalizerTests.cs
- [ ] T014 Product'a Genre/Moods/AudienceLevel/Themes/EnrichmentConfidence/WorkKey + InStock(computed) + SetEnrichment/SetWorkKey — Domains/Products/Product.cs
- [ ] T015 WorkKeyNormalizer helper — src/services/catalog/Catalog.Api/Helpers/WorkKeyNormalizer.cs

**Kontrat**

- [ ] T016 ProductAdded'a `bool InStock = true` ekle (contracts/product-added-v2.md) — src/others/Shared/IntegrationEvents.cs → Foundational commit

## Phase 3: User Story 1 — v11 kataloğu temiz yükle (P1) 🎯 MVP

- [ ] T017 [P] [US1] ExcelRowMapperTests v11 case'leri yaz (tur/ton `;`-split/seviye/konu/guven virgül-toleranslı parse, "SATIŞ YOK*"→InStock=false, boş hücre→true) — tests/Catalog.Api.Tests/ExcelRowMapperTests.cs
- [ ] T018 [P] [US1] ImportJobTests Enriched sayacı case'leri yaz (Tagged→Enriched rename) — tests/Catalog.Api.Tests/ImportJobTests.cs
- [ ] T019 [P] [US1] StockItem Create(0) testi yaz — tests/Stock.Api.Tests/StockItemTests.cs
- [ ] T020 [US1] ExcelRowMapper v11 kolonları oku (Genre/MoodsRaw/AudienceLevelRaw/ThemesRaw/Confidence/InStock; Ortalama Okuma Süresi düşer) — Helpers/ExcelRowMapper.cs
- [ ] T021 [US1] ImportJob Tagged→Enriched (alan + sayaç + dry-run rapor metni) — Domains/ImportJobs/ImportJob.cs
- [ ] T022 [US1] ImportJobWorker: satır başına SetEnrichment + SetWorkKey; ProductAdded(InStock) yayını; Enriched artışı — Process/ImportJobWorker.cs
- [ ] T023 [US1] Stock consumer: InStock=false→Create(0), true→Random 20-50 ([Transactional] hali üstüne) — src/services/stock/Stock.Api/CatalogConsumers.cs
- [ ] T024 [US1] build+test yeşil → US1 commit. (Canlı import doğrulaması Faz 8/quickstart'ta — Aspire + v11 dry-run→confirm)

## Phase 4: User Story 2 — Facet'li arama (P2)

- [ ] T025 [P] [US2] SearchPredicateBuilderTests facet case'leri yaz (jsonb fragment üretimi: Genre ANY, Themes ?|, OnlyInStock; BM25 grubuna Genre/Moods/Themes; sözlük-dışı red) — tests/Catalog.Api.Tests/SearchPredicateBuilderTests.cs + ProductSearchValidationTests.cs
- [ ] T026 [US2] SearchRequest + validation: Genres/Moods/AudienceLevels/Themes/OnlyInStock; BookTaxonomy doğrulaması — Search/SearchRequest.cs
- [ ] T027 [US2] SearchPredicateBuilder: çıktı sözleşmesi (Bm25Query + SqlFragment listesi); facet jsonb predicate'leri; BM25 metin grubu genişlemesi — Search/SearchPredicateBuilder.cs
- [ ] T028 [US2] SearchAsync: fragment'ları tek WHERE'de birleştir — Search/ProductSearchQueries.cs
- [ ] T029 [US2] REST paramları (genre/mood/audienceLevel/theme çoklu + onlyInStock; response'a taksonomi alanları) — Domains/Products/ProductEndpointExtension.cs (+ GetBook response)
- [ ] T030 [US2] MCP search_books paramları + tool description sözlük özeti — Features/Agents/Queries/SearchBooks.cs
- [ ] T031 [US2] ScoutAgent CatalogSubAgent wrapper paramları + SearchBookItem mirror (Genre/Moods/InStock) — ScoutAgent.Api Agents/SubAgents/CatalogSubAgent.cs + Agents/ToolResults.cs
- [ ] T032 [US2] build+test yeşil → US2 commit

## Phase 5: User Story 3 — similar_books (P3)

- [ ] T033 [P] [US3] Benzerlik saf-birim testleri yaz (BM25 OR query-string kurucu sanitize'lı; CompatibleAudienceLevels eşleme; fallback karar mantığı) — tests/Catalog.Api.Tests/SimilarBooksQueryTests.cs
- [ ] T034 [US3] SimilarBooksOptions (AuthorBoost/GenreBoost/TranslatorBoost/DefaultCount/MaxCount; Options pattern bind+validate) — Options/SimilarBooksOptions.cs + Program.cs kaydı
- [ ] T035 [US3] SimilarBooks slice + MCP tool (contracts/similar-books-tool.md: skor+boost+hard filtre+DISTINCT ON WorkKey+sıralama+fallback; ham SQL ProductSearchQueries deseni) — Features/Agents/Queries/SimilarBooks.cs
- [ ] T036 [US3] ScoutAgent: similar_books wrapper + SimilarBooksResult mirror + prompt kuralları ("şuna benzer"→tool; "ne anlatıyor"→özet YOK, karta/detaya yönlendir — FR-024) — CatalogSubAgent.cs + ToolResults.cs
- [ ] T037 [US3] build+test yeşil → US3 commit

## Phase 6: Polish & kapanış

- [ ] T038 FLOW.md as-built (catalog: planned ekleri düşür, 4/4b kategori-etiket satırlarını söküm olarak işle, hotspot kapat; stock: 1b InStock as-built) — src/services/catalog/FLOW.md + src/services/stock/FLOW.md
- [ ] T039 CLAUDE.md BC haritası catalog/stock satırları (kategori yok, taksonomi+WorkKey+similar_books, InStock sinyali) — CLAUDE.md
- [ ] T040 quickstart.md canlı doğrulama koşusu (Aspire + v11 import dry-run→confirm; US2 REST denemeleri; US3 Kâşif sohbeti) — kanıt: sayılar + ekran çıktısı
- [ ] T041 speckit-analyze tutarlılık + son `dotnet build && dotnet test` + PR

## Dependencies

- Phase 2 her şeyi bloklar. İçinde: T002→T003; T004-T011 blok (T003 sonrası); T012/T013→T014/T015; T016 bağımsız ama Phase 3 öncesi.
- US1 (P3): T017-T019 [P] → T020-T023 → T024. US2 (P4): T025 → T026-T031 → T032 (US1'den bağımsız derlenir ama canlı test import ister). US3 (P5): T033 → T034-T036 → T037 (US2'nin sanitize altyapısını kullanır).
- Phase 6 hepsinden sonra.

## Parallel örnekleri

- T002 ‖ (T004 hazırlık okuması); T012 ‖ T013; T017 ‖ T018 ‖ T019; T025 testleri US1 koşarken yazılabilir.

## MVP

US1 (Phase 1-3): temiz v11 kataloğu + taksonomi + WorkKey + stok sinyali canlı. US2/US3 artımlı.