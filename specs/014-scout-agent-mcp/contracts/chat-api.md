# Contract — ScoutAgent Chat HTTP yüzeyi

React → Gateway (`/api/chat/**`) → ScoutAgent.Api. v1 tek-parça yanıt (stream yok). Mesaj-güdümlü,
`?q=` URL yok.

## POST /api/chat/v1/messages

Bir sohbet turu. **Opsiyonel bearer** — token varsa login (kalıcı sohbet); yoksa anon (oturumluk).

**İstek:**
```json
{ "conversationId": "<guid|null>", "message": "Sabahattin Ali'nin kitaplarını listeler misin",
  "history": [ { "role": "user|assistant", "text": "..." } ] }
```
- `conversationId`: login'de mevcut sohbeti sürdürür; null → yeni.
- `history`: **anon** istemci-transkript (devamlılık/sayfalama bağlamı). Login'de yok sayılır (sunucu kaydı otoriter).

**Yanıt 200:**
```json
{ "conversationId": "<guid>",
  "reply": { "text": "…", "items": [ { "tool": "search_books", "result": { } } ] } }
```
- `items[].result` = tool dönüşünden BİREBİR (React tool adına göre kart render eder).
- `text` = sohbet dili; sayfa-izi burada ("… 40 kitaptan 1. sayfa").

**Hatalar:** 400 boş mesaj · 503 LLM/MCP ucu erişilemez ("Kâşif şu an hizmet veremiyor").

**Auth-gating:** login değilse araç kutusu yalnız açık tool'lar (search_books/get_book/get_book_reviews).
Kişisel istek anon gelirse model "giriş gerekli" der (tool görünmez).

## GET /api/chat/v1/conversations  *(login şart)*

Kullanıcının sohbet listesi (yalnız kendi). `{ conversations: [{id,title,updatedAt}] }`.

## GET /api/chat/v1/conversations/{id}  *(login şart, sahiplik)*

Tam transkript (yalnız metin). Başkasınınki → 404. `{ id, title, messages:[{role,text,createdAt}] }`.

## Gateway rotası

`/api/chat/**` → `scout-agent` (gateway config). Bearer aynen taşınır (ScoutAgent sub'ı çözer,
`ValidateAudience=false` — 007/012 deseni).

## React tüketimi (ECommerceWeb)

- `ChatWorkspace`: kutuya yaz → POST `/messages` → `reply.items` tool'a göre kart. `?q=` URL kalkar.
- Anon: tarayıcı son N turu `history`'de yollar (sayfalama/devam bağlamı).
- `ChatHistory` "tekrar ara": `/sohbet?q=` yerine sohbete `message` POST'lar.
- "devamını getir": kullanıcı yazar → aynı akışa sonraki sayfa eklenir (yeni kart, öncekiler durur).
