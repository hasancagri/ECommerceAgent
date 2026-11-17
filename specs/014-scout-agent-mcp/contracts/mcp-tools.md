# Contract — BC MCP query tool'ları

Her tool = ilgili BC `Agents/Queries` slice'ında (`[McpServerToolType]` aynı dosyada, conventions VSA).
BC `/mcp` ucu geri açılır (`MapMcp` + `WithToolsFromAssembly`). Tümü **query-only** (yazma YOK).
ScoutAgent MCP client olarak bağlanır; kişisel uçta isteğin bearer'ı taşınır (uç scope doğrular).

## Auth-gating

| Grup | Tool | /mcp ucu auth | Durum |
|---|---|---|---|
| Açık | `search_books`, `get_book` | hayır | 007'de VAR (ILIKE uzlaştır) |
| Açık | `get_book_reviews` | hayır | YENİ |
| Login | `get_my_orders` | evet | 007'de VAR |
| Login | `get_my_library` | evet | 007'de VAR |
| Login | `get_my_addresses` | evet | 007'de VAR |
| Login | `get_basket` | evet | YENİ |
| Login | `get_search_history` | evet | YENİ (Personalization) |

## Catalog (açık)

- **`search_books(query, page=1, pageSize=10)`** → `{ items:[{id,title,authors,price,isSalable,coverUrl}], total, page }`.
  Ad VEYA yazar ILIKE; yazar-eşleşmeleri ÖNCE, grup içi PurchaseCount. `total` deterministik sayfalama kaynağı.
  **Arama kaydı:** login + dolu-query → `UserSearched` yayınla (fanout → Personalization 013). Anon → yayınlama.
- **`get_book(id)`** → tek kitap künyesi (ad/yazar/açıklama/fiyat/kapak/satılabilir).

## Reviews (açık) — YENİ

- **`get_book_reviews(productId, page=1, pageSize=10)`** → `{ items:[{author?,rating,text,createdAt}], total, average, count }`.
  Yorum HEMEN görünür (moderasyon yok); özet okuma anında hesaplanır.

## Basket (login) — YENİ

- **`get_basket()`** → `{ items:[{productId,title,quantity,price}], totalQuantity }`. Sahip = token'daki sub.
  Kalemleri insan diliyle an (GUID gösterme — MCP kural #10).

## Order (login)

- **`get_my_orders(page=1, pageSize=10)`** → `{ items:[{orderId,status,createdAt,total}], total }`. Yalnız kendi.

## Library (login)

- **`get_my_library(page=1, pageSize=10)`** → `{ items:[{productId,title,acquiredAt}], total }`. Yalnız kendi.

## Customer (login)

- **`get_my_addresses()`** → `{ items:[{label,recipient,line,city,isDefault}] }`. Yalnız kendi.

## Personalization (login) — YENİ

- **`get_search_history(page=1, pageSize=20)`** → `{ items:[{query,mode,lastSearchedAt,count}], total, page }`.
  Tekilleştirilmiş sayfalı liste (013 `SearchHistoryProjection` ile aynı okuma; Türkçe-duyarlı normalize). Yalnız kendi (`sub`).
  **Yazma YOK:** silme/temizleme bu tool'da yok — agent "arama geçmişimi sil" isteğinde geçmiş ekranına yönlendirir (013 REST). 013'ün REST yüzeyi (GET/DELETE) aynen durur.

## Kurallar (tüm tool'lar)

- **MCP optional param → DEFAULT şart** (nullable yetmez; yoksa ArgumentException — bilinen tuzak).
- Dönüş birebir; boş sonuç dürüstçe boş (uydurma yok).
- İç GUID'ler yalnız tool çağrısında; yanıt insan diliyle (MCP sunucu kuralları).
- Wolverine handler keşfi: `Handler` ad-soneki yoksa `IncludeType` gerekebilir (yeni handler'da kontrol).
