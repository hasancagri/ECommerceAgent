# Data Model: MCP Query-Only Dönüşü + ScoutAgent

## Yeni kalıcı veri — yalnız ScoutAgent'da

### Conversation (ScoutAgent, Marten, alias `conversation`)

Girişli kullanıcının bir sohbeti. Root belge.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | sohbet kimliği |
| UserId | Guid | sahip (sub claim); index |
| Title | string | ilk kullanıcı mesajından türetilir |
| Messages | IReadOnlyList\<ChatMessage\> | sıralı; private liste + readonly expose |
| Summary | string? | eskiler damıtıldıkça dolan rolling özet |
| SummarizedUpTo | int | özete dahil edilmiş son mesaj indeksi (çift-özetleme engeli) |
| CreatedAt / UpdatedAt | DateTimeOffset | denetim |

**ChatMessage (VO):** `Role` (user/assistant), `Text`, `CreatedAt`. (Yapılı `items` sunucuda saklanmaz —
cevap anında tool'dan üretilir; transkriptte yalnız metin tutulur, bağlam zehirlenmesini önler.)

**Davranış (aggregate metotları, İlke II):**
- `Start(userId, firstMessage)` → Title türet, ilk user mesajını ekle.
- `AddUserMessage(text)` / `AddAssistantMessage(text)`.
- `ApplySummary(summary, upToIndex)` → özet bloğunu günceller, `SummarizedUpTo` ilerletir.
- Invariant: UserId değişmez; Messages yalnız metotlardan büyür.

**Index:** UserId (geçmiş listeleme — yalnız kendi sohbetleri, FR-010).

### ConversationContext (VO / saf hesap — İlke VI test-first)

Marten belgesi DEĞİL; `(Summary, Messages, N)` → modele verilecek bağlam dizisi üreten saf fonksiyon.
Birim test edilir (pencere kırpma + özet başa ekleme + tool-sonucu dışlama).

## Mevcut BC belgeleri — DEĞİŞMEZ, yalnız OKUNUR

Yeni query slice'ları bunları okur; aggregate'lere alan/ davranış EKLENMEZ.

| BC | Belge | Okuma anahtarı | MCP tool niyeti |
|---|---|---|---|
| Catalog | Product (+Author/Category/Publisher) | ad/yazar arama; ISBN/Id detay | `search_books`, `get_book` (anonim) |
| Customer | AddressBook | UserId | `get_my_addresses` (scope: customer.read) |
| Library | UserPurchase (read-model) | UserId | `get_my_library` (scope: library.read) |
| Order | Order | BuyerId | `get_my_orders` (scope: order.read) |

**Not:** Catalog'da ürün arama query'si YOK — yeni yazılır (ad/yazar `ILIKE` + `IsSalable` bilgisi +
sayfalama). Diğer üçünde query handler yok; UserId/BuyerId index'li düz Marten sorgusu.
