# Tasks: Hibrit Arama (BM25 + Semantik + RRF)

**Feature**: `specs/011-hybrid-search` | **Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

Yalnız Catalog BC. ParadeDB (pg_search + pgvector) raw-SQL; **Marten değişmez** (vektör ayrı `product_embedding` tablosu). 3 US bağımsız, öğrenme-sıralı: US1 BM25 embedding'siz canlı → US2 semantik → US3 RRF. Domain-TDD (İlke VI): saf birim (RRF skoru, param validasyon) test-first; SQL arama host-gerekli → canlı doğrulama.

**Kısaltma**: `Catalog/`=`src/services/catalog/Catalog.Api/`, `Tests/`=`tests/Catalog.Api.Tests/`.

---

## Phase 1: Setup

- [X] T001 Başlangıç yeşili: `dotnet build && dotnet test` — temiz mi doğrula
- [X] T002 ParadeDB image swap `src/aspire/AppHost/AppHost.cs`: `pgvector/pgvector:pg17` → `paradedb/paradedb:<ver>-pg17` (kesin tag Docker Hub'dan sabitle — research FLAG); fresh infra (eski postgres volume sil — image değişimi temiz başlar)

---

## Phase 2: Foundational (US1+US2 bloklar — ParadeDB eklenti + raw-SQL altyapı)

- [X] T003 Eklenti kaydı `Catalog/Extensions/MartenExtensions.cs`: `opts.Storage.ExtendedSchemaObjects.Add(new Weasel.Postgresql.Extension("pg_search"))` + `Extension("vector")` (idempotent; ApplyAllDatabaseChangesOnStartup kurar)
- [X] T004 Raw-SQL arama yürütücü iskeleti `Catalog/Search/ProductSearchQueries.cs`: `IQuerySession`/`NpgsqlConnection` ile parametreli raw SQL çalıştırma + productId→künye çözme (sıra korunur) yardımcıları

---

## Phase 3: User Story 1 — BM25 keyword arama (P1) 🎯 MVP

**Hedef**: ad+açıklama+etiket üstünde BM25 alaka-sıralı keyword arama; embedding GEREKMEZ.

**Bağımsız test**: `q=Orwell`/`distopya` → beklenen kitap üst sıralarda; boş sorgu güvenli boş; Türkçe İ/ı aynı sonuç.

- [X] T005 [US1] BM25 index ISchemaObject `Catalog/Search/ProductSearchSchema.cs`: idempotent `CREATE INDEX IF NOT EXISTS ... ON mt_doc_product USING bm25 (id, (data->>'Name'), (data->>'Description'), (data->'Tags')) WITH (...)` — ICU tokenizer + ascii_folding (contracts/bm25-search.md); `ExtendedSchemaObjects.Add`
- [X] T006 [P] [US1] Test-first `Tests/ProductSearchValidationTests.cs`: boş/whitespace sorgu→boş sonuç sinyali, limit guard (≤100, 0/negatif→default) — saf param validasyon
- [X] T007 [US1] BM25 sorgu `Catalog/Search/ProductSearchQueries.cs`: `... @@@ :q ORDER BY paradedb.score(id) DESC LIMIT :limit OFFSET :offset`; T006 validasyonu geçsin
- [X] T008 [US1] REST slice `Catalog/Domains/Products/Features/Queries/SearchProducts.cs`: `GET /catalog/v1/search?q=&limit=&offset=` (anonim keşif) → sıralı künye; gateway rotası
- [ ] T009 [US1] Doğrula (CANLI — Aspire gerekli, kullanıcı koşar): Aspire; BM25 index kuruldu; `q=Orwell`→Orwell üstte, `q=distopya`→tema üstte, `q=` boş, `İstanbul`/`istanbul` aynı (quickstart US1; İ/ı FLAG — yetmezse sorgu-öncesi normalize)

**Checkpoint**: US1 = MVP (BM25 keyword arama canlı, embedding'siz).

---

## Phase 4: User Story 2 — Semantik arama + benzer kitaplar (P2)

**Hedef**: OpenAI offline embedding → pgvector → kNN; "benzer kitaplar" + "anlamca ara".

**Bağımsız test**: ünlü kitabın benzerleri tematik; kelime-dışı niyet sorgusu tema uygun kitap döner.

- [X] T010 [US2] Embedding tablo+HNSW ISchemaObject `Catalog/Search/ProductSearchSchema.cs`: `product_embedding(product_id uuid pk, embedding vector(1536))` + `hnsw (embedding vector_cosine_ops)` (contracts/embedding-ingest.md)
- [X] T011 [P] [US2] Offline araç `~/dev/book-crawl/embed_books.py`: OpenAI `text-embedding-3-small`, metin=ad+yazar+açıklama+etiket, ISBN-anahtarlı resumable cache, çıktı `{isbn:[float×1536]}`; anahtar env (log'lama — İlke VIII); 008 deseni
- [X] T012 [US2] Admin ingest ucu `Catalog/Domains/Products/.../EmbeddingIngest`: batch `[{isbn,vector}]` → Gtin→product_id → `product_embedding` upsert (`ON CONFLICT DO UPDATE`); boyut≠1536/bilinmeyen ISBN atla; admin-anahtar korumalı
- [X] T013 [US2] kNN sorgu `Catalog/Search/ProductSearchQueries.cs`: benzer (ürün vektörü→`<=> ORDER BY` kendini ele) + anlam (metin→OpenAI anlık sorgu-vektör→`<=>`)
- [X] T014 [US2] REST slice'lar `Catalog/Domains/Products/Features/Queries/SimilarProducts.cs` + anlam aramayı SearchProducts'a mod olarak ekle (`GET .../similar/{isbn}`, `GET .../search?q=&mode=semantic`)
- [ ] T015 [US2] Doğrula (CANLI — Aspire + OpenAI anahtarı, kullanıcı koşar): offline araç küçük örnek → ingest → `product_embedding` dolu; benzer kitaplar tematik; `q=yalnızlığı anlatan sıcak roman` tema uygun (quickstart US2)

**Checkpoint**: US1 + US2 = kelime + anlam ayrı ayrı çalışır.

---

## Phase 5: User Story 3 — RRF füzyon (P3)

**Hedef**: BM25 + kNN iki sıralamayı konum-tabanlı tek listede birleştir.

**Bağımsız test**: `q=Orwell karanlık gelecek` → ad (BM25) + tema (kNN) birleşir; tek-ayaktan iyi.

- [X] T016 [P] [US3] Test-first `Tests/RrfFusionTests.cs`: `Combine_TwoLists_RanksByPositionSum`, `Combine_OnlyInOneList_NotDropped`, `Combine_K_DampensTopDominance` — saf (host'suz), contracts/hybrid-rrf.md
- [X] T017 [US3] RRF saf fonksiyon `Catalog/Search/RrfFusion.cs`: `Combine(listA, listB, k=60)` → `skor=Σ 1/(k+sıra)`, dedupe, azalan sırala; T016 geçsin
- [X] T018 [US3] Hibrit slice `Catalog/Domains/Products/Features/Queries/HybridSearch.cs`: BM25 top-N + kNN top-N (anlam sorgu-vektör) → `RrfFusion.Combine` → künye (`GET .../search?q=&mode=hybrid`)
- [ ] T019 [US3] Doğrula (CANLI — Aspire + OpenAI, kullanıcı koşar): `q=Orwell karanlık gelecek` hibrit → ad+tema birleşir; karışık sorguda tek-ayaktan iyi (quickstart US3, SC-004)

**Checkpoint**: US1+US2+US3 = tam hibrit arama.

---

## Phase 6: Polish & Cross-Cutting

- [X] T020 [P] FLOW.md as-built `src/services/catalog/FLOW.md`: 8a/8b/8c `_(planned, 011)_` ekini düşür, kenar-anchor ekle (ProductSearchQueries, RrfFusion, product_embedding)
- [~] T021 [P] ScoutAgent MCP tool (opsiyonel) — ATLANDI: MCP yüzeyi bu repo'dan SÖKÜLÜ (Program.cs
  "MCP yüzeyi SÖKÜLDÜ"; CLAUDE.md "MCP parça parça geri getirme" yasağı). Hibrit arama REST'te açık.
- [ ] T022 (CANLI — kullanıcı) Implement FLAG'leri kapat + SC raporla: kesin ParadeDB tag sabit, `USING bm25` vs `USING paradedb` yazımı sürüme göre teyit, Türkçe İ/ı küçük örnekle doğrula; SC-001 (bilinen sorgu→beklenen ilk 10) + SC-003 (benzer tematik) + SC-004 (hibrit>tek-ayak) elle örnek

---

## Dependencies & Execution Order

- **Setup (T001-T002)** → önce (ParadeDB image şart)
- **Foundational (T003-T004)** → US'leri bloklar (eklenti + raw-SQL iskelet)
- **US1 (T005-T009)** → MVP; embedding'siz. Test-first T006→T007
- **US2 (T010-T015)** → US1'den bağımsız test edilir (kendi tablo+araç+ingest); T011 offline araç paralel
- **US3 (T016-T019)** → US1+US2 üstüne (birleştirecek iki liste). Test-first T016→T017
- **Polish (T020-T022)** → sonra

## Parallel Opportunities

- T006 (validasyon testi) ∥ T005 (index) — ayrı dosyalar
- T011 (offline araç, repo-dışı Python) ∥ US2'nin .NET işleri
- T016 (RRF testi) US3 impl'inden bağımsız
- T020/T021 (FLOW/MCP) ∥ T022

## MVP Scope

**US1 (T001-T009)** = BM25 keyword arama canlı — embedding/OpenAI olmadan. BM25'i izole gösterir/öğretir. US2 anlamı, US3 füzyonu ekler. Her US ayrı demoable.

## Implement notları (as-built sapmalar + FLAG durumu)

- **ISchemaObject yerine hosted-service DDL** (T003/T005/T010): `ExtendedSchemaObjects`/`ISchemaObject`
  yerine `ProductSearchSchemaInitializer : IHostedService` — AddCatalogMarten'DEN SONRA kaydedilir
  (Marten şema-apply'dan sonra koşar), idempotent ham DDL; her ifade bağımsız (biri patlarsa ötekiler
  kurulur). Daha az kırılgan (Weasel ISchemaObject sürüm-hassas). Ham SQL için ayrı `NpgsqlDataSource`.
- **FLAG: ParadeDB tag SABİT** = `paradedb/paradedb:0.26.6-pg17` (Docker Hub, 2026-11 stable pg17).
- **FLAG: BM25 yazımı** = `USING bm25` (0.26'da `USING paradedb` alias'ı; bm25 geri-uyumlu) +
  alan `::pdb.icu` cast + `pdb.score(id)`. Çok-alan OR'lu `@@@` skoru CANLI teyit (T022).
- **FLAG: İ/ı** = ICU tokenizer lowercase (InvariantGlobalization zemini) + Tags ASCII-ikiz (009).
  Name/Description dotless-ı için ICU `ascii_folding` arg'ı CANLI küçük-örnekle denenir (T009/T022);
  SQL tek yerde (`ProductSearchQueries`/`ProductSearchSchema`) — tweak kolay.
- **OpenAI anahtarı opsiyonel**: `OpenAiEmbeddingOptions:ApiKey` user-secrets'ta yoksa semantik/hibrit
  ayak sessizce devre-dışı, BM25 tam çalışır (US1 bağımsız demoable).
