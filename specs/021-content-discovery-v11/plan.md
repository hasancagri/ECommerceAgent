# Implementation Plan: İçerik-Temelli Keşif Omurgası (v11 Katalog)

**Branch**: `feat/021-content-discovery-v11` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/021-content-discovery-v11/spec.md`

## Summary

Kategori + Etiket eksenleri sökülür; Product kapalı-sözlük taksonomi alanları (Genre/Moods/
AudienceLevel/Themes/EnrichmentConfidence — değerler Türkçe sözlük) + WorkKey kazanır; v11 Excel import bu alanları ve ikili stok
sinyalini (ProductAdded.InStock → Stock seed 0 / 20-50) besler; arama tur/ton/seviye/konu facet
filtreleri (jsonb exact-match) + BM25 metin genişlemesi alır; yeni MCP tool `similar_books`
konu+ton BM25 örtüşmesi + objektif boost + WorkKey tekilleştirmeyle benzer önerir. DB sıfır —
migration yok, Marten auto-create.

## Technical Context

**Language/Version**: .NET 10 / C# (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten (Postgres document store, Newtonsoft), Wolverine (bus + RabbitMQ
fanout), ParadeDB pg_search (BM25), ClosedXML (Excel), MCP SDK (agent-içi query tool), xUnit+Shouldly

**Storage**: catalogDb (Marten `mt_doc_product` + BM25 index `product_bm25_idx`), stockDb

**Testing**: saf unit (xUnit+Shouldly, tests/Catalog.Api.Tests + Stock.Api.Tests + ScoutAgent.Api.Tests);
host/integration harness YOK (İlke VI)

**Target Platform**: Aspire AppHost (lokal), k3s (prod)

**Project Type**: mikroservis (BC-per-service), VSA+CQRS

**Performance Goals**: arama tek-SQL kalır (BM25 + jsonb predicate aynı sorguda); similar_books
tek sorgu (CTE + DISTINCT ON), ~22K satırda <100ms beklenir

**Constraints**: INSERT-ONLY import korunur; IsSalable tanımı değişmez; ProductAdded additive
(InStock default true); BM25 index DDL değişmez (data fast+expand_dots yeni alanları kapsar)

**Scale/Scope**: 22.286 kitap, 5 yayınevi; ~15 dosya söküm + ~10 dosya yeni/değişen; 3 BC
(Catalog, Stock, ScoutAgent) + Shared kontrat

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Durum | Not |
|---|---|---|
| I — BC izolasyonu | ✅ | Tek kanal: ProductAdded fanout (additive alan); Stock adedin tek yazarı kalır; Catalog StockCount event-fed kopya. MCP yalnız ScoutAgent tüketir (similar_books query-only) |
| II — Zengin aggregate | ✅ | SetEnrichment/SetWorkKey davranış metotları, guard'lar aggregate'te; BookTaxonomy static sözlük (aggregate değil, Domains-içi veri) |
| III — VSA+CQRS | ✅ | SimilarBooks = Features/Agents/Queries slice; facet filtreleri Search/ altyapısında (018 deseni); repository yok |
| IV — Result pattern | ✅ | ResultDomain / FeatureObjectResultModel aynen |
| V — Scope yetki | ✅ | similar_books anonim-okuma (search_books emsali); yetki yüzeyi değişmiyor |
| VI — Domain-TDD | ✅ | BookTaxonomy, WorkKeyNormalizer, SetEnrichment, StockItem.Create(0), benzerlik query-string kurucu test-first; tasks.md'de test task'ları önce |
| VII — FLOW.md | ✅ | Mini-storm yazıldı (catalog + stock); as-built güncellemesi implement PR'ında |
| VIII — Sır yok | ✅ | Yeni sır/kimlik yüzeyi yok; WorkKey sır değil ama iç alan — tool dönüşüne çıkmaz |

Post-design re-check: ✅ ihlal yok, Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/021-content-discovery-v11/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── product-added-v2.md
│   ├── search-api.md
│   └── similar-books-tool.md
└── tasks.md            (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/others/Shared/IntegrationEvents.cs                 # ProductAdded +InStock; ProductChangedEvent kategori alanları çıkar
src/services/catalog/Catalog.Api/
├── Domains/Products/
│   ├── Product.cs                                     # +Genre/Moods/AudienceLevel/Themes/EnrichmentConfidence/WorkKey/InStock(computed); -CategoryId/-RelatedCategoryIds/-Tags
│   ├── BookTaxonomy.cs                                # YENİ: kapalı sözlük + seviye uyum grupları
│   ├── ValueObjects/ProductValueObjects.cs            # BookTag ÇIKAR (Money kalır)
│   ├── ProductEndpointExtension.cs                    # categoryId çıkar; facet + onlyInStock paramları
│   └── Features/
│       ├── Queries/GetBook.cs                         # kategori zinciri çıkar; taksonomi alanları döner
│       └── Agents/Queries/SearchBooks.cs              # category çıkar; facet paramları + sözlük description
│       └── Agents/Queries/SimilarBooks.cs             # YENİ: similar_books MCP tool
├── Domains/Categories/                                # SİLİNİR
├── Domains/ImportJobs/ImportJob.cs                    # Tagged → Enriched
├── Helpers/CategoryNaming.cs                          # SİLİNİR
├── Helpers/ExcelRowMapper.cs                          # v11 kolonları; kategori/etiket okuma çıkar
├── Helpers/WorkKeyNormalizer.cs                       # YENİ
├── Process/ImportJobWorker.cs                         # SetEnrichment+SetWorkKey; InStock tespiti; kategori resolve çıkar
├── Search/SearchRequest.cs                            # -CategoryId; +Genres/Moods/AudienceLevels/Themes/OnlyInStock
├── Search/SearchPredicateBuilder.cs                   # jsonb fragment listesi; BM25 grubu tur/ton/konu
├── Search/ProductSearchQueries.cs                     # WHERE fragment'ları + benzerlik sorgusu yardımcıları
├── Options/SimilarBooksOptions.cs                     # YENİ: boost katsayıları + N
└── Extensions/MartenExtensions.cs                     # Category kayıt/index çıkar
src/services/stock/Stock.Api/CatalogConsumers.cs       # InStock=false → Create(0); true → random 20-50
src/agents/ScoutAgent.Api/
├── Agents/SubAgents/CatalogSubAgent.cs                # similar_books wrapper + prompt; category param çıkar
└── Agents/ToolResults.cs                              # SimilarBooksResult mirror; kategori alanları çıkar
tests/Catalog.Api.Tests/                               # BookTaxonomyTests, WorkKeyNormalizerTests YENİ;
                                                       # Category*/BookTag* SİLİNİR; Product/Search/Mapper/ImportJob güncellenir
tests/Stock.Api.Tests/                                 # StockItem Create(0) case
```

**Structure Decision**: Mevcut VSA yerleşimi korunur; yeni dosyalar 018/014 desenlerinin yanına.
Benzerlik SQL'i ProductSearchQueries deseniyle ham NpgsqlDataSource (Marten LINQ dışı).

## Complexity Tracking

Yok — ihlal yok.