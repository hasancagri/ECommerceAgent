# Kontrat: Arama yüzeyi (REST + MCP search_books) — 021 değişiklikleri

## REST `GET /api/catalog/products/search` (gateway, anonim)

| Param | Durum | Açıklama |
|---|---|---|
| `categoryId` | ÇIKAR | Kategori öldü |
| `genre` | YENİ (çoklu) | BookTaxonomy.Genres değeri; tekrar edilebilir (`genre=roman&genre=öykü`) → OR |
| `mood` | YENİ (çoklu) | BookTaxonomy.Moods değeri → OR |
| `audienceLevel` | YENİ (çoklu) | BookTaxonomy.AudienceLevels değeri → OR |
| `theme` | YENİ (çoklu) | BookTaxonomy.Themes değeri → OR |
| `onlyInStock` | YENİ (bool) | StockCount>0 süzgeci (IsSalable'dan ayrı) |
| diğerleri | AYNEN | q, publisher, excludePublisher, binding, min/maxPrice, min/maxPageCount, onlySalable, onlyDiscounted, sort, page, pageSize |

- Facet alanları arası AND, alan içi OR. Sözlük-dışı değer → 400 + anlaşılır hata kodu.
- Serbest metin `q` artık Genre/Moods/Themes değerlerini de tarar (Tags.Value'nun yerine).
- Response item'ına taksonomi alanları eklenir (Genre, Moods, AudienceLevel, Themes, InStock).
- Türkçe değerler URL-encode ister (018 Kestrel 400 tuzağı).

## MCP `search_books` (Catalog /mcp — ScoutAgent tüketir)

- `category` paramı ÇIKAR (ResolveCategoryIdAsync ölür).
- YENİ paramlar: `genres`, `moods`, `audienceLevels`, `themes` (string dizileri),
  `onlyInStock` (bool). Tool description sözlük değerlerini özetler (agent doğru değer üretsin).
- Davranış REST ile aynı arama yolu (`SearchAsync`); UserSearched yayını DEĞİŞMEZ.

## Kâşif davranış kuralı (karar, 2026-11-23)

"Kitap ne anlatıyor?" sorusu CEVAPLANMAZ — açıklama/özet tool'u bilinçli yok (get_book MCP'ye
dönmedi, get_author açılmadı). Kâşif kartı gösterir ve detay sayfasına yönlendirir (016 SEO-link);
özet anlatmaya/uydurmaya girişmez. CatalogSubAgent prompt kuralı US3 kapsamında eklenir.