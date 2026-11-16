# Contract: MCP Tool Yüzeyi (query-only)

Her BC kendi `/mcp` ucunu host eder. Tool'lar `[McpServerTool]`; ScoutAgent MCP client olarak bağlanır.
Kapalı liste — bu spec dışı tool AÇILMAZ. Tümü OKUR, hiçbiri yazmaz.

## Catalog MCP (anonim — token gerekmez)

### `search_books`
- **Girdi:** `query` (string — ad/yazar serbest metin), `page` (int, default 1), `pageSize` (int, default 10, tavan 20).
- **Çıktı:** `{ items: [{ id, title, authors, price, isSalable, coverUrl }], total, page }`.
- **Davranış:** ad/yazar `ILIKE`; `IsSalable` bilgisi döner (satılamaz da listelenir, işaretli). GUID `id` ScoutAgent'a döner ama kullanıcıya gösterilmez (detay/link için iç kullanım).

### `get_book`
- **Girdi:** `isbn` (string) VEYA `id` (Guid).
- **Çıktı:** `{ id, title, authors, publisher, category, price, isSalable, pageCount, language, coverUrl, description }` ya da bulunamadı.

## Customer MCP (scope: `customer.read`)

### `get_my_addresses`
- **Girdi:** yok (kullanıcı token'dan).
- **Çıktı:** `{ addresses: [{ label, recipient, line, district, city, isDefault }] }` — yalnız çağıranın; iç addressId DÖNMEZ.

## Library MCP (scope: `library.read`)

### `get_my_library`
- **Girdi:** yok.
- **Çıktı:** `{ items: [{ title, authors, acquiredAt }] }` — çağıranın UserPurchase kayıtları.

## Order MCP (scope: `order.read`)

### `get_my_orders`
- **Girdi:** `status?` (opsiyonel filtre).
- **Çıktı:** `{ orders: [{ orderNo, status, placedAt, total, lineCount }] }` — çağıranın (BuyerId) siparişleri.

## Ortak kurallar

- **MCP optional param DEFAULT şart** (nullable yetmez — mcp-tool-optional-param-default tuzağı): `page=1`, `pageSize=10`, `status=null` gibi default verilir.
- Kişisel tool'lar token'sız çağrıda veri DÖNDÜRMEZ (401/boş) — ScoutAgent bunu "giriş gerekli"ye çevirir.
- Tüm `[McpServerTool]` metotları `Features/Agents/Queries/` slice'ında; Wolverine değil MCP tarar (`WithToolsFromAssembly`).
