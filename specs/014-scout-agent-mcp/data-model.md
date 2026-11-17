# Data Model — ScoutAgent (Kâşif)

ScoutAgent'ın TEK kalıcı verisi = login kullanıcının sohbeti (scoutDb). BC verisi MCP tool'undan
okunur, ScoutAgent'ta SAKLANMAZ. Anon sohbet kalıcı değil (istemci taşır).

## Conversation (Aggregate — scoutDb)

Login kullanıcının sohbeti. `: AggregateRoot` (Id + denetim). Yalnız sahibi görür/sürdürür.

| Alan | Tip | Kural |
|---|---|---|
| Id | Guid | kimlik |
| UserId | Guid | doğumda sabit; boş olamaz (`Start` guard) |
| Title | string | ilk kullanıcı mesajından türetilir |
| Messages | IReadOnlyList\<ConversationMessage\> | private koleksiyon; yalnız metotla büyür |
| Summary | string | rolling özet (pencere dışı eskiler damıtılır) |
| SummarizedUpTo | int | özetlenmiş mesaj indeksi (çift-özet engel) |

**Davranış (ResultDomain, test-first):**
- `Start(userId, firstMessage)` → yeni sohbet + ilk user mesajı; userId boşsa Error.
- `AddUserMessage(text)` → user mesajı ekle; boş metin Error.
- `AddAssistantMessage(text)` → assistant mesajı ekle.
- `ApplySummary(summary, upTo)` → özet + SummarizedUpTo ileri al (geri gitmez).

**İnvariant:** Messages yalnız bu metotlardan büyür; transkripte YALNIZ metin girer (hacimli tool
sonucu gömülmez). Kullanıcı yalnız KENDİ sohbetini okur (sahiplik query'de UserId süzer).

## ConversationMessage (VO — Conversation içinde)

| Alan | Tip | Kural |
|---|---|---|
| Role | ContextRole (User/Assistant/System) | enum, Conversation dosyasında |
| Text | string | yalnız metin |
| CreatedAt | DateTimeOffset | sıra |

## ConversationContext (domain-service — saf)

`Build(summary, messages, windowSize)` → `[System:özet] + [son N mesaj]`. LLM'e giden bağlamı kurar
(FR-011 sabit maliyet). Saf fonksiyon → test edilir.

## Tool okuma sonuçları (ScoutAgent'ta SAKLANMAZ — BC'den anlık)

Her biri ilgili BC read-model/aggregate'inden okunur, `{text, items}` içinde birebir döner, React
kart render eder. ScoutAgent yalnız geçirir.

| Tool | Dönüş (özet) | Kaynak BC | Auth |
|---|---|---|---|
| search_books | kitap listesi (ad/yazar/fiyat/satılabilir/kapak) + Total + Page | catalog | açık |
| get_book | tek kitap künyesi | catalog | açık |
| get_book_reviews | yorum listesi + özet (ortalama/adet) | reviews | açık |
| get_basket | sepet kalemleri | basket | login |
| get_my_orders | sipariş listesi + durum | order | login |
| get_my_library | kitaplık kayıtları | library | login |
| get_my_addresses | adres listesi | customer | login |
| get_search_history | tekilleştirilmiş arama geçmişi (query/mode/count) | personalization | login |

## McpEndpoint (config — ScoutOptions yanında)

Agent'ın bağlanacağı BC MCP uçları. Config-güdümlü; `RequiresAuth` araç-kutusu gating'i sürer.

| Alan | Değer örneği |
|---|---|
| Name/Url/RequiresAuth | catalog `http://catalog-api/mcp` false · reviews `http://reviews-api/mcp` false · basket `http://basket-api/mcp` true · order/library/customer/personalization `http://<bc>-api/mcp` true |

**Not:** 007 listesinde catalog/customer/library/order var; **basket + reviews + personalization eklenir** (reviews açık; basket/personalization login).
