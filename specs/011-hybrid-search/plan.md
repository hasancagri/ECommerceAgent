# Implementation Plan: Hibrit Arama (BM25 + Semantik + RRF)

**Branch**: `011-hybrid-search` | **Date**: 2026-11-16 | **Spec**: [spec.md](./spec.md)

## Summary

Catalog'da ParadeDB (pg_search BM25 + pgvector) üstünde üç bağımsız arama ayağı. **US1 BM25**: `mt_doc_product` JSONB'ye custom raw-DDL BM25 index (ad+açıklama+etiket) → alaka-sıralı keyword arama; embedding gerekmez; MVCC ile otomatik bakım. **US2 semantik**: OpenAI offline batch (008 deseni) vektör üretir → ayrı `product_embedding` tablosu (`vector(1536)` + HNSW) → kNN kosinüs. **US3 RRF**: iki sıralamayı konum-tabanlı birleştirir. Arama = Catalog içi raw-SQL sorgu katmanı (ParadeDB); **Marten'e dokunulmaz** (vektör ayrı tabloda, bump YOK).

## Technical Context

**Language/Version**: .NET 10; Postgres → **ParadeDB** (pg_search + pgvector)

**Primary Dependencies**: Marten 9.5 (DEĞİŞMEZ — vektör ayrı tabloda, PgVector add-on + bump GEREKMEZ), Npgsql (raw SQL), ParadeDB extensions (pg_search, vector). Offline araç: Python `openai` (008 deseni, repo-dışı book-crawl).

**Storage**: catalogDb (ParadeDB). BM25 index `mt_doc_product` üstünde (yeni tablo YOK). Vektör: yeni `product_embedding` tablosu (productId + vector(1536)). İkisi de `ExtendedSchemaObjects` custom `ISchemaObject` (idempotent raw DDL — Marten düşürmez).

**Testing**: Saf birim test-first (İlke VI): RRF skor hesabı, arama parametre validasyonu (limit/boş sorgu). Arama SQL'i host-gerekli → Aspire canlı doğrulama (quickstart).

**Target Platform**: Aspire AppHost; Catalog.Api + ParadeDB container

**Project Type**: Mevcut Catalog BC genişletme + solution-dışı offline embedding aracı

**Performance Goals**: Etkileşimli arama (<1 sn); BM25 + HNSW index'li; 22.5k ürün

**Constraints**: Marten version bump YOK (risk izolasyonu); BC-arası senkron çağrı yok; Türkçe İ/ı (ICU + ascii_folding, implement doğrulaması); embedding offline (arama anında üretilmez)

**Scale/Scope**: 22.5k ürün, 3 arama modu, 1 offline araç, 1 yeni tablo + 1 index

## Constitution Check

| İlke | Durum | Not |
|---|---|---|
| I BC İzolasyonu | GEÇER | Yalnız Catalog; arama Catalog verisi üstünde; senkron BC çağrısı yok; offline araç solution-dışı |
| II Aggregate | GEÇER (sınırlı) | `Product`'a `SetEmbedding` davranışı (opsiyonel — vektör ayrı tabloda ingest edilebilir); arama read-model/sorgu, aggregate mutasyonu değil |
| III VSA/CQRS | GEÇER | Arama = Query slice (`Features/Queries` + agent için `Agents/Queries`); salt-okur |
| IV Result Pattern | GEÇER | Sorgu handler'ları `FeatureListResultModel`/`FeaturePagedResultModel` |
| V Scope | GEÇER | Arama anonim (keşif); `[RequiredScope]` yok (katalog okuma anonim deseni) |
| VI Domain-TDD | GEÇER | Saf birim (RRF skoru, param validasyon) test-first; SQL arama host-gerekli → canlı doğrulama (İlke VI SQL'i dayatmaz) |
| VII FLOW.md | GEÇER | FLOW.md 8a/8b/8c storm sticky'leri; implement'te as-built |
| VIII Sır | N/A | OpenAI anahtarı offline araçta env (008 kuralı) — insan-yüzeye/log'a girmez |
| Teknoloji (Marten/Postgres) | GEÇER | ParadeDB = postgres üst-kümesi; Marten değişmez; custom index `ExtendedSchemaObjects` (Marten-idiyomatik) |

**Gate: PASS** — Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/011-hybrid-search/
├── plan.md · research.md · data-model.md · quickstart.md
└── contracts/
    ├── bm25-search.md          # BM25 index + sorgu sözleşmesi (US1)
    ├── embedding-ingest.md     # offline araç çıktısı + ingest + vektör tablo (US2)
    └── hybrid-rrf.md           # kNN + RRF füzyon sözleşmesi (US2/US3)
```

### Source Code (repository root)

```text
src/aspire/AppHost/AppHost.cs            # postgres image: pgvector/pgvector → paradedb/paradedb:<ver>-pg17

src/services/catalog/Catalog.Api/
├── Extensions/MartenExtensions.cs       # + ExtendedSchemaObjects: Extension("pg_search")+("vector")
│                                        #   + custom ISchemaObject: BM25 index (mt_doc_product) + product_embedding tablo+HNSW
├── Search/                              # YENİ: arama altyapısı (Domains dışı — süreç/altyapı)
│   ├── ProductSearchSchema.cs           #   ISchemaObject'ler (BM25 index, embedding tablo+HNSW; idempotent DDL)
│   └── ProductSearchQueries.cs          #   raw-SQL yürütücü (BM25 @@@+score, kNN <=>, RRF) — Npgsql/session.Connection
├── Domains/Products/
│   ├── Product.cs                        # (ops.) + SetEmbedding davranışı (ingest Product'a yazarsa)
│   └── Features/Queries/SearchProducts.cs      # US1 keyword arama slice (REST)
│   └── Features/Queries/SimilarProducts.cs     # US2 benzer kitaplar slice
│   └── Features/Queries/HybridSearch.cs        # US3 füzyon slice
│   └── Features/Agents/Queries/SearchBooks.cs  # (ops.) ScoutAgent MCP tool (anonim keşif)
└── Domains/Products/.../EmbeddingIngest  # US2 admin ingest ucu (offline araç çıktısını product_embedding'e yükler)

tests/Catalog.Api.Tests/
└── RrfFusionTests.cs                    # saf RRF skor birleştirme (test-first)

# Offline araç (repo-dışı, 008 gibi):
~/dev/book-crawl/embed_books.py          # OpenAI text-embedding-3-small; ISBN→vektör; resumable cache
```

**Structure Decision**: Arama Catalog içi yeni `Search/` altyapı klasörü (raw-SQL + ISchemaObject) + salt-okur Query slice'ları. Vektör ayrı `product_embedding` tablosu (Marten'e dokunmaz, doc şişmez). Offline embedding aracı solution-dışı (008 deseni). Marten version DEĞİŞMEZ.

## Complexity Tracking

Anayasa ihlali yok — tablo boş. (Not: Marten.PgVector fluent API [9.36+] bilinçli REDDEDİLDİ — solution-geneli bump riski > ayrı tablo maliyeti; ayrı tablo doc-şişmesini de önler.)
