# Contract: ScoutAgent Chat API (React → Gateway → ScoutAgent)

Gateway rotası `/api/chat/**` → ScoutAgent. Giriş varsa React bearer token'ı iletir; yoksa anonim.

## POST `/api/chat/v1/messages`
Sohbete mesaj gönder, cevap al.

- **Girdi:** `{ conversationId?: Guid, message: string }` — `conversationId` yoksa yeni sohbet (girişliyse).
- **Çıktı:** `{ conversationId, reply: { text, items[] } }`
  - `text`: Kâşif'in sohbet cümlesi (model).
  - `items`: tool dönüşünden birebir yapılı öğeler (tip + alanlar tool'a göre; boş olabilir).
- **Auth:** opsiyonel bearer. Token'lıysa kişisel tool'lar açık + sohbet kalıcı kaydedilir; anonimse yalnız katalog tool'u + kalıcı kayıt YOK.
- **Hata:** model/MCP erişilemezse `503` + anlaşılır mesaj (FR-008); sistem geri kalanı etkilenmez.

## GET `/api/chat/v1/conversations` (scope: giriş şart)
Kullanıcının sohbet geçmişi listesi.
- **Çıktı:** `{ conversations: [{ id, title, updatedAt }] }` — yalnız çağıranın (FR-010).

## GET `/api/chat/v1/conversations/{id}` (giriş şart, sahiplik)
Bir sohbetin tam transkripti (geçmişten açma).
- **Çıktı:** `{ id, title, messages: [{ role, text, createdAt }] }`.
- Başka kullanıcının sohbeti → `404` (sahiplik; varlık sızdırılmaz).

## Davranış notları
- Devamlılık sunucudan: `conversationId` ile gelen istekte ScoutAgent kaydı yükler, `[özet]+[son N]` bağlamını kurar (framework conversation state'ine güvenilmez — FR-012).
- v1 tek-parça yanıt (stream yok — FR-013); React istek sırasında "düşünüyor" gösterir.
- Anonim devamlılık: `conversationId` döner ama kalıcı değil (oturum belleği); sekme kapanınca kaybolur.
