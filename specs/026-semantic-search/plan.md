# Implementation Plan: Semantik Arama (Dilim-2)

**Branch**: `feat/026-semantic-search` | **Date**: 2026-11-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/026-semantic-search/spec.md`

## Summary

Embedding omurgası (025) üstüne ilk tüketici: betimleyici sorguyu anlam yakınlığıyla eşleyen arama.
Catalog'da ayrı `semantic_search` MCP tool'u (yalnız ScoutAgent) — sorgu OpenAI'la embed edilir,
`product_embedding` üstünde kNN (`<=>` kosinüs) + yapısal jsonb pre-filter + eşik ile aranır. ScoutAgent
`/extract` betimleyici niyeti ayırır (demir yoksa → `SemanticQuery`), `/search` tool'u seçer. Rol-ayrımı:
semantik=vektör, BM25 KARIŞMAZ, RRF YOK. Lexical `search_books` / REST `/products/search` DEĞİŞMEZ.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: Npgsql (ham-SQL kNN, `ProductSearchQueries` deseni) · Microsoft.Extensions.AI.OpenAI
(`EmbeddingClientFactory`, 025) · Marten (künye çözümü + Publisher/Author ad→id) · ModelContextProtocol
(agent-içi MCP tool) · Wolverine (`UserSearched` fanout)

**Storage**: Postgres `catalogDb` (ParadeDB imajı) — `product_embedding` (025, `vector(1536)`, YALNIZ OKU) + `mt_doc_product`

**Testing**: xUnit + Shouldly saf birimler (routing niyet-ayrımı + vektör-literal + eşik/filtre kurucu); kNN/MCP canlı (İlke VI dışı)

**Target Platform**: Linux konteyner (Aspire orkestrasyon)

**Project Type**: web-service (Catalog BC) + agent (ScoutAgent routing)

**Performance Goals**: kNN 22k üstünde HNSW → ms-ölçek; +1 OpenAI sorgu-embed çağrısı/arama (~100-300ms); cache YOK (backlog)

**Constraints**: rol-ayrımı (RRF yok) · yüzey yalnız ScoutAgent (dış/REST semantik ucu yok) · query-only · İlke VIII (OpenAI key sunucuda)

**Scale/Scope**: ~22k kitap, tek Catalog BC; yalnız `semantic_search` + ScoutAgent routing (similar V2/taste AYRI dilim)

## Constitution Check

*GATE: Phase 0'dan önce geçmeli. Phase 1 sonrası yeniden.*

- **İlke I (BC izolasyon + MCP):** PASS — agent-içi query-only MCP tool (adr-mcp-inward-agent); yalnız ScoutAgent
  tüketir, YAZMA yok. Senkron cross-BC çağrı yok; `UserSearched` integration event (mevcut 012 deseni). Catalog
  yalnız kendi DB'si (`product_embedding`/`mt_doc_product`).
- **İlke III (VSA + CQRS):** PASS — query slice `Features/Agents/Queries/SemanticSearch.cs` ([McpServerToolType]
  aynı dosyada); ham-SQL yürütücü `Search/SemanticSearchQueries.cs`; repository yok.
- **İlke IV (Result):** PASS — handler `FeatureObjectResultModel<Response>`.
- **İlke VI (domain-TDD):** PASS — saf birimler test-first: ScoutAgent niyet-ayrımı (`IsSemanticIntent`),
  vektör-literal, eşik/filtre kurucu. Raw-SQL/MCP canlı doğrulanır (kural dışı).
- **İlke VII (FLOW.md):** PASS — Catalog FLOW Dilim-2 storm as-built'e çevrilir; ScoutAgent FLOW routing seam.
- **İlke VIII (sır dönmez):** PASS — OpenAI key yalnız sunucuda (`CatalogEmbeddingOptions`, 025); yüzeye/loga çıkmaz.

İhlal YOK → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/026-semantic-search/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (MCP tool + routing kontratı)
└── tasks.md             # /speckit-tasks (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Domains/Products/Features/Agents/Queries/
│   └── SemanticSearch.cs            # query slice + [McpServerToolType] semantic_search (SimilarBooks deseni)
├── Search/
│   ├── SemanticSearchQueries.cs     # ham-SQL: embed → jsonb pre-filter → kNN <=> → sıralı id (ProductSearchQueries deseni)
│   └── SemanticSearchOptions.cs     # eşik (cosine cutoff) + limit (Options; SimilarBooksOptions deseni)
└── (EmbeddingClientFactory 025'ten yeniden kullanılır)

src/agents/ScoutAgent.Api/
├── Search/BookSearchFilters.cs      # + SemanticQuery slot (SimilarToTitle deseni)
├── Search/ExtractPrompt.cs          # + betimleyici-niyet kuralı
├── Search/SemanticIntent.cs         # SAF: IsSemanticIntent(filters) (test-first)
├── Domains/Conversations/Features/Commands/SearchTyped.cs  # üçlü dal: semantic_search / similar_books / search_books
└── Agents/ToolResults.cs            # + SemanticSearchResult ayna-tipi

tests/
├── Catalog.Api.Tests/               # vektör-literal + eşik/filtre kurucu saf birimler
└── ScoutAgent.Api.Tests/            # SemanticIntent saf birim
```

**Structure Decision**: İki yüzey — Catalog (kNN motoru + MCP tool) + ScoutAgent (routing). Her ikisi mevcut
desenleri aynen izler (SimilarBooks/ProductSearchQueries/SimilarBooksOptions · SimilarToTitle slot/branch).

## Complexity Tracking

İhlal yok — boş.
