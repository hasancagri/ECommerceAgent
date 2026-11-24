# Contracts — Semantik Arama (Dilim-2)

## MCP tool: `semantic_search` (Catalog, agent-içi, query-only)

Yalnız ScoutAgent tüketir (dış/REST yüzey YOK). Anonim serbest.

**Girdi:**
- `query` (string, zorunlu) — betimleyici/kavramsal sorgu metni.
- `count` (int, varsayılan 10, en çok 20) — sonuç sayısı.
- `onlyInStock` (bool), `language` (string?), `minPrice`/`maxPrice` (decimal?), `minPageCount`/`maxPageCount`
  (int?), `publisher` (string?) — opsiyonel yapısal filtre (search_books ile aynı anlam).

**Çıktı:** `{ items: BookItem[], total }`. `BookItem` = search_books/similar_books kart şekli
(id, title, authors, price, isSalable, inStock, coverUrl, publisher, isbn, genre, moods).

**Davranış:**
1. `query` boş → boş sonuç.
2. `query` OpenAI'la embed (omurga modeli).
3. jsonb yapısal pre-filter (verilen kısıtlar) → aday id kümesi.
4. kNN: aday üstünde `embedding <=> sorgu_vektörü`, eşik `MaxCosineDistance` altı, mesafe artan sırala, LIMIT count.
5. Sıralı id → künye (Marten, sıra korunur).
6. Eşiğe hiçbir aday girmezse boş (alakasız doldurma yok).

## ScoutAgent routing kontratı

**`/extract` çıktısı (`BookSearchFilters`):** betimleyici sorgu → `SemanticQuery` dolu, diğer alanlar boş.
Demir (başlık/yazar/ISBN/yayınevi/filtre) varsa → eski alanlar, `SemanticQuery` null.

**`/search` (SearchTyped) dalı:**
- `SemanticQuery` dolu → `semantic_search(query=SemanticQuery, count, [filtreler])`.
- `SimilarToTitle` dolu → `similar_books` (024, değişmez).
- ikisi de boş → `search_books` (lexical/filtre, değişmez).

Cevap `reply.items[].tool = "semantic_search"`, `result` = Response. React `SemanticSearchResult`'ı
mevcut kart dağıtıcısında render eder (search_books case'ine eklenir — `{items}` aynı şekil).

## Integration event

`UserSearched(userId, query, mode="semantic", resultCount, searchedAt)` — login'de yayınlanır (fanout →
Personalization). Ayrık + hata-yutar; anonim yayınlamaz. (012/014 deseni.)

## Değişmeyen kontratlar (regresyon kalkanı)

- `search_books` MCP tool imzası + davranışı — DOKUNULMAZ.
- REST `/products/search` — DOKUNULMAZ.
- `similar_books` (024) — DOKUNULMAZ.
