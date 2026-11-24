# Data Model — Semantik Arama (Dilim-2)

Bu feature YENİ kalıcı veri EKLEMEZ — 025'in `product_embedding` tablosunu yalnız OKUR. Aşağıdakiler
taşıma/ara tipler (DB değil).

## Okunan (025'ten, değişmez)

- **product_embedding** — `product_id uuid PK`, `embedding vector(1536)`, `input_hash`, `updated_at`.
  Bu feature: kNN kaynağı (`embedding <=> $sorgu_vektörü`). YAZMAZ.
- **mt_doc_product** — künye + jsonb (filtre alanları: `StockCount`, `Language`, `Price.Amount`,
  `PageCount`, `PublisherId`). Pre-filter + künye çözümü.

## Taşıma tipleri (yeni, kalıcı değil)

### Catalog (`SemanticSearch`)
- **Query**: `Query(string Text, int Count, SemanticFilter? Filter)`.
- **SemanticFilter** (opsiyonel yapısal kısıt): `OnlyInStock bool`, `Language string?`, `MinPrice/MaxPrice decimal?`,
  `MinPageCount/MaxPageCount int?`, `Publisher string?`. (search_books filtreleriyle aynı anlam; jsonb predicate.)
- **Response**: `Items: IReadOnlyList<BookItem>`, `Total`. `BookItem` = SimilarBooks.BookItem ile aynı şekil
  (Id/Title/Authors/Price/IsSalable/InStock/CoverUrl/Publisher/Isbn/Genre/Moods) → React mevcut kartı render eder.

### ScoutAgent
- **BookSearchFilters.SemanticQuery** `string?` — betimleyici sorgu metni (LLM doldurur; null = semantik değil).
  Mevcut `SimilarToTitle` slotuyla aynı tarz.
- **SemanticSearchResult** (ayna-tip, `ToolResults.cs`): `Items`, `Total` (Catalog Response şekli; İlke I kopya).

## Kurallar / invariantlar

- Eşik dışı (cosine mesafesi > `MaxCosineDistance`) kitap sonuçta YOK (FR-002).
- Embedding'i olmayan ürün kNN'de yok → doğal dışlanır (FR-008), hata değil.
- Sonuç sırası = kosinüs mesafesi artan (en yakın önce); DISTINCT gerekmez (ürün başına tek embedding).
- Yapısal filtre = uygunluk (jsonb), vektör = yakınlık (sıralama); ikisi ayrı (rol-ayrımı, RRF yok).
- `SemanticQuery` ⊕ `SimilarToTitle` ⊕ (ikisi de boş=lexical) — SearchTyped üçlü dal, aynı anda biri.

## Routing niyet-ayrımı (saf, test-first)

`SemanticIntent.IsSemantic(BookSearchFilters)` → `SemanticQuery` dolu VE demir yok (author/publisher/
binding/price/page/flags/similarToTitle boş). Deterministik; LLM yargısı `SemanticQuery`'nin dolu olup
olmamasında kodlanmış. (Dal basitçe `SemanticQuery` doluluğuna bakabilir; saf fonksiyon test kancası.)
