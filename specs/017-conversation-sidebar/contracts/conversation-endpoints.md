# Contract — Conversation HTTP uçları (ScoutAgent, gateway `/api/chat/v1`)

Taban: `/api/chat/v1/conversations`. Hepsi **giriş şart** (`RequireAuthorization`) + **sahiplik**.
Kimlik JWT `sub`'tan; named-scope YOK (013 deseni, kişisel veri).

## Mevcut (değişen)

### GET /conversations
- Değişmez. Dönüş: `{ conversations: [{ id, title, updatedAt }] }`, `UpdatedTime` desc.

### GET /conversations/{id}
- **Genişler:** `MessageView`'a `itemsJson: string?` eklenir (assistant turunda dolu).
- Dönüş: `{ id, title, messages: [{ role, text, createdAt, itemsJson? }] }`.
- Sahiplik: başkasınınki/yok → **404** (varlık sızmaz).

## Yeni

### PATCH /conversations/{id}
- Gövde: `{ "title": "<yeni başlık>" }`.
- Başarı: **200** + güncel `{ id, title }` (ya da `FeatureResultModel` IsSuccess).
- Boş/boşluk başlık → **400** (resource kodu). Üst-sınır aşımı → **400**.
- Sahiplik yok/başkası → **404**.
- **UpdatedTime değişmez** (sidebar sırası korunur).

### DELETE /conversations/{id}
- Gövdesiz. Başarı: **204**.
- HARD delete (geri alınamaz). Sahiplik yok/başkası → **404**.

## Frontend istemci (`src/api/chat.ts`)

- `renameConversation(id, title, token): Promise<void|{id,title}>` → PATCH.
- `deleteConversation(id, token): Promise<void>` → DELETE (204).
- `ConversationDetail.messages[]` → `itemsJson?: string` (React `JSON.parse` → `ReplyItem[]`).

## Anon (sunucu DEĞİL — yerel)

- Rename/sil `anonChats.ts`: `renameAnon(id, title)` (YENİ) + `deleteAnon(id)` (mevcut). REST yok.
