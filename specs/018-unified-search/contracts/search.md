# Contract — Unified Catalog Search (Phase 1)

İki çağıran (REST müşteri yüzeyi + MCP sohbet agent'ı) aynı birleşik arama yürütücüsüne yakınsar. Yanıt biçimi tüm türlerde aynı (ProductSummary sayfası).

## REST — birleşik arama

`GET /api/catalog/products/search` (anonim, opsiyonel JWT — bugünkü desen)

Query parametreleri (hepsi opsiyonel; mode parametresi KALKAR):
```
q           : serbest metin
publisher / excludePublisher : yayınevi dahil/hariç (kısmi ad)
minPrice / maxPrice          : fiyat aralığı
minPageCount / maxPageCount  : sayfa aralığı
binding     : "karton" | "ciltli"
categoryId  : kategori (tek-leaf)
onlySalable : bool
onlyDiscounted : bool
sort        : price_asc | price_desc | newest | popular
page / pageSize : sayfalama (pageSize ≤ 20)
```
Yanıt `200`:
```json
{ "items": [ { "isbn": "...", "name": "...", "imageUrl": "...", "price": 0, "discountRate": 0, "tags": ["..."] } ],
  "total": 0, "page": 1 }
```
Davranış: boş `q` + filtre → filtre-only sonuç; `q` boş + sort boş → popülerlik sırası; sonuç yok → boş `items`.

## MCP tool — `search_books` (ScoutAgent)

Aynı parametre kümesi (serbest metin + filtreler + sort + sayfalama), ince sarmalayıcı → birleşik executor (naive bellekte-filtre KALKAR). Yanıt kart künyesi (ProductSummary). Niyet (NL → parametreler) agent'ta çözülür.

## Nokta-fetch (KAPSAM DIŞI — korunur)

`GET /api/catalog/products/{isbn}` → tek kitap (Marten PK lookup). MCP `get_book` → aynı. Aramaya dahil DEĞİL.

## KALDIRILAN uçlar (söküm)

- `GET /api/catalog/products/search?mode=semantic|hybrid` → yalnız birleşik (mode yok).
- `GET /api/catalog/products/{isbn}/similar` → SİLİNİR (benzer-kitap, semantik).
- `POST /api/admin/catalog/embeddings` → SİLİNİR (embedding upload).

## Hata/durum

- Geçersiz/aşırı parametre → `SearchParameters` normalize (hata değil; guard'lanır).
- Beklenmeyen → `GlobalExceptionHandler` (İlke IV; beklenen durumlar Result).
