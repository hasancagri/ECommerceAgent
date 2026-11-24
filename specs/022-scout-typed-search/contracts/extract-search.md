# Contracts — ScoutAgent iki-faz arama uçları (Faz 1)

Gateway: `/api/chat/**` → scout-agent. İkisi de opsiyonel bearer (anonim serbest; mevcut `/messages`
deseni), rate-limit "chat". React iki-tur oto-relay.

## POST /api/chat/v1/extract  (Tur 1 — LLM niyet-çıkarma)

Ham sorguyu tipli NESTED niyet objesine çevirir. Durumsuz (Conversation'a yazmaz).

**Request**
```json
{ "query": "Mo Yan'ın ucuz kitapları" }
```

**Response 200** — `BookSearchFilters` (grup null = konuşulmadı):
```json
{
  "text": { "query": null, "author": "Mo Yan" },
  "publisher": null,
  "priceHint": "ucuz",
  "sortHint": null,
  "bindingHint": null,
  "pageHint": null,
  "taxonomy": null,
  "flags": null,
  "page": 1,
  "pageSize": 10
}
```
**Hata:** çıkarma başarısızsa 200 + boş/çıplak-query fallback objesi (aramayı bloklamaz) ya da 503
(model erişilemez) — mevcut `/messages` 503 deseni. Kullanıcı özür GÖRMEZ, akış sürer.

## POST /api/chat/v1/search  (Tur 2 — deterministik yorum + arama)

Tipli objeyi `search_books` argümanlarına düzler (FilterInterpreter), Catalog yorumlar + arar, kart döner.
LLM YOK. Conversation'a yazar (017 geçmiş/sidebar); "devamını getir" = `page+1`.

**Request** — `BookSearchFilters` + opsiyonel `conversationId`:
```json
{
  "conversationId": "…optional…",
  "filters": { "text": {"author":"Mo Yan"}, "priceHint":"ucuz", "page":1, "pageSize":10 }
}
```

**Response 200** — mevcut sohbet cevabı şekli (reuse): `conversationId`, `reply.text` (şablon intro,
"'Mo Yan' için N kitap"), `reply.items[]` (kartlar; `tool="search_books"`, echo `filters`).

## Slot → search_books argüman eşlemesi (FilterInterpreter)

| BookSearchFilters | search_books arg | Not |
|---|---|---|
| text.query | query | Catalog facet+BM25 |
| text.author | author | yalnız o yazar (jsonb `?|`) |
| publisher.include / exclude | publisher / excludePublisher | |
| sortHint | sort (metin) | Catalog ParseSort |
| bindingHint | binding (metin) | Catalog binding parse |
| priceHint | minPrice / maxPrice | FilterInterpreter metin→sayı |
| pageHint | minPageCount / maxPageCount | FilterInterpreter metin→sayı |
| flags.* | onlyDiscounted/onlyInStock/onlySalable | doğrudan |
| page / pageSize | page / pageSize | doğrudan |

## Değişmeyen
- Diğer niyetler (sepet/yorum/kitaplık) → mevcut `POST /messages` (router+sub-agent) yolunda kalır.
- `search_books` MCP tool'u + Catalog arama + yazar filtresi hazır (bu oturumda eklendi).
