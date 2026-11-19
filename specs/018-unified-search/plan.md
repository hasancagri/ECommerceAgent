# Implementation Plan: Unified Catalog Search

**Branch**: `018-unified-search` | **Date**: 2026-11-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/018-unified-search/spec.md`

## Summary

Catalog'daki iki kopuk arama yolunu (naive bellekte-filtre `search_books` + ayrı `Search/` hibrit) TEK sorgu yürütücüsünde birleştir. Tek motor = **pg_search BM25** (lexical + yapısal filtre/sort/kategori, fast field + Top-K). Semantik/pgvector altyapısı (embedding tablosu, sorgu-vektör üretimi, RRF, benzer-kitap, admin embedding upload) **sökülür**. Tüm müşteri aramaları tek `SearchRequest` → tek executor → yalnız sayfa döner; milyonlarca satırda ölçeklenir. Kesme sırası: Türkçe ICU tokenizer + fast field index ÖNCE, naive yol SONRA kaldırılır (sıfır regresyon).

## Technical Context

**Language/Version**: .NET 10, C# (`Nullable`+`ImplicitUsings` açık)

**Primary Dependencies**: Marten (Postgres doc store), ParadeDB `pg_search` (BM25 index/sorgu operatörü `@@@`, `pdb.score`), Npgsql (ham SQL — Marten LINQ `@@@` bilmez), Wolverine (query bus). **Kaldırılan:** `pgvector`/`vector` extension, OpenAI embedding istemcisi, RRF füzyon.

**Storage**: Postgres `catalogManagement` şeması; `mt_doc_product` (Marten belge) + BM25 index (`id, data` → fast field'lerle genişletilir). **Düşen:** `product_embedding` tablosu + HNSW index.

**Testing**: xUnit + Shouldly, saf unit (İlke VI). Saf birimler: `SearchParameters` (var), mod/sıra çözümü, BM25 terim sanitize/stopword, filtre→SQL predicate/parametre kurma. SQL yürütme + index = Aspire'da canlı doğrulama (host harness yok).

**Target Platform**: Linux/konteyner, Aspire AppHost (tüm sistem birlikte).

**Project Type**: web-service — tek BC (Catalog.Api), sorgu (CQRS okuma) tarafı.

**Performance Goals**: ≥1M kayıtta filtreli+sıralı arama yalnız sayfayı (10-20) döndürür; aday kümesi belleğe yüklenmez; %95 arama kabul gecikmesinde (kullanıcı anında görür).

**Constraints**: Türkçe noktasız-ı katlama (ICU) ZORUNLU (yoksa regresyon); stok/indirim filtreleri kopya-tabanlı (eventual-consistent, kabul); index Marten jsonb şekline (`$values`, Money VO nesting) bağlı — kırılganlık, test/anchor.

**Scale/Scope**: Katalog bugün ~23k kitap; tasarım milyonlara. Tek BC içi değişiklik; yeni aggregate/tablo/event/BC yok (bir tablo + bir alt-sistem SÖKÜLÜR).

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **I. BC İzolasyonu** ✓ — Değişiklik Catalog BC içinde; kendi `catalogManagement` şeması + kendi `mt_doc_product`'ı. Arama yalnız Catalog verisi + mevcut kopyalar (StockCount/DiscountRate; event-fed, zaten var) üzerinde. **Yeni cross-BC erişim/senkron RPC YOK.** Semantik söküm cross-BC etki yaratmaz.
- **II. Zengin Aggregate** ✓ — `Product` aggregate DEĞİŞMEZ. Arama bir sorgu (okuma) yolu; `SearchRequest` bir query DTO'su, aggregate değil. Yeni invariant yok.
- **III. VSA+CQRS, Repository Yok** ✓ — Birleşik arama bir Query slice'ı; handler `IQuerySession`/`NpgsqlDataSource` ile OKUR, yazmaz. `NpgsqlDataSource` ham-SQL kullanımı mevcut `ProductSearchQueries` deseni (Marten LINQ `@@@`/fast-field sort ifade edemez) — bilinçli okuma istisnası, repository değil. MCP tool ince sarmalayıcı kalır.
- **IV. Result Pattern** ✓ — Handler `FeatureObjectResultModel`/`FeaturePagedResultModel`; `SearchParameters` saf. Beklenen durumlar (boş sorgu, sonuç yok) Result ile.
- **V. Scope Yetki** ✓ — Arama anonim okuma (opsiyonel JWT, bugünkü desen); yeni scope yok. Kaldırılan admin embedding ucu X-Admin-Api-Key'liydi — söküm yetki yüzeyini daraltır.
- **VI. Domain-TDD** ✓ — Saf birimler test-first (mod/sıra çözümü, stopword/sanitize, filtre→predicate); `tasks.md`'de test task'ı implementasyondan önce. SQL/index canlı doğrulanır (harness kurulmaz).
- **VII. Domain Süreci (FLOW.md)** ✓ — Arama domain state değiştirmez (sorgu); Catalog FLOW.md domain süreci DEĞİŞMEZ. `UserSearched` yayını (login arama) bu feature'da değişmiyor (REST'ten zaten kaldırılmış, ScoutAgent tek kaynak). Kenar-anchor rename'leri (silinen semantik slice'lar) FLOW'da anchor ise aynı PR'da düşürülür.
- **VIII. Sır İnsan-Yüzeyde Görünmez** ✓ — OpenAI anahtarı bağımlılığı KALKAR (sır yüzeyi daralır); arama kitap künyesi döner, sır yok.

**Sonuç: GEÇTİ, gerekçelenecek ihlal yok.** (Artefakt kademesi: **Tam** — endpoint kontratı değişir + bir alt-sistem sökülür + index DDL değişir.)

## Project Structure

### Documentation (this feature)

```text
specs/018-unified-search/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — pg_search fast field, Türkçe ICU, söküm envanteri
├── data-model.md        # Phase 1 — SearchRequest, mod/sıra, index fast field haritası
├── quickstart.md        # Phase 1 — Aspire canlı doğrulama senaryoları
├── contracts/
│   └── search.md        # Phase 1 — birleşik arama REST + MCP kontratı (+ kaldırılan uçlar)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Search/
│   ├── ProductSearchSchema.cs      # DEĞİŞİR: BM25 index + fast field'ler + Türkçe ICU; embedding tablosu/HNSW/vector DÜŞER
│   ├── ProductSearchQueries.cs     # DEĞİŞİR: tek birleşik SQL kurucu (text? + WHERE fast field + ORDER BY mod); SemanticAsync/SimilarAsync/SemanticSql/SimilarSql DÜŞER
│   ├── SearchParameters.cs         # KALIR (saf; gerekirse genişler)
│   ├── ProductSummary.cs           # KALIR (sonuç künyesi)
│   ├── RrfFusion.cs                # SİLİNİR (füzyon yok)
│   └── Embedding/                  # SİLİNİR (QueryEmbeddingClient, OpenAiEmbeddingOptions)
├── Domains/Products/Features/
│   ├── Queries/SearchProducts.cs   # DEĞİŞİR: tek birleşik sorgu (keyword→unified); semantic mod DÜŞER
│   ├── Queries/HybridSearch.cs     # SİLİNİR (RRF hibrit)
│   ├── Queries/SimilarProducts.cs  # SİLİNİR (benzer-kitap)
│   ├── Commands/IngestEmbeddings.cs# SİLİNİR (embedding upload)
│   └── Agents/Queries/SearchBooks.cs # DEĞİŞİR: naive bellekte-filtre KALKAR, birleşik executor'a devreder (filtreler korunur)
└── Domains/Products/ProductEndpointExtension.cs # DEĞİŞİR: /search tek mod; /{isbn}/similar + /api/admin/catalog/embeddings DÜŞER; /{isbn} (nokta-fetch) KALIR

tests/Catalog.Api.Tests/           # saf birim testleri (mod/sıra, sanitize/stopword, filtre→predicate)
```

**Structure Decision**: Tek BC (Catalog.Api) içi; arama yürütme (`Search/`) + sorgu slice'ları + endpoint extension değişir, `Product` aggregate ve diğer BC'ler el değmez. MCP `search_books` ve REST `/search` aynı birleşik executor'a yakınsar.

## Complexity Tracking

> Anayasa ihlali yok — bu bölüm boş.
