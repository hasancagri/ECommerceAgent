# Data Model — Sesle Kâşif'e Soru (Phase 1)

## Yeni kalıcı varlık: YOK

Bu feature kalıcı veri EKLEMEZ:

- **Ses** geçicidir — tarayıcıda kaydedilir, uca yollanır, OpenAI'ye stream edilir, **hiçbir yerde saklanmaz** (İlke VIII + gizlilik). Diske/DB'ye yazılmaz.
- **Çevrilen metin** mevcut sohbet turuna girer (kullanıcı gönderince `SendMessage` yolundan) — ayrı depolama yok. Kayıtlı kullanıcıda mevcut `Conversation` transkript davranışı (017) aynen geçerli; sesle mi yazıyla mı girildiği AYRIM TUTULMAZ.

## Yeniden kullanılan varlık

- **`ScoutUsageDaily`** (mevcut Marten belgesi, `RateLimiting/`) — çevirme rate-cap'i için yeniden kullanılır. Yeni alan YOK; yalnız yeni partition anahtarı öneki (`tx:<userId|ip>`) ile ayrı gün-sayacı satırı oluşur. Sohbet kotasından bağımsız.

## Geçici (in-flight) şekiller (kalıcı değil)

- **Transcribe isteği**: multipart `file` (ses blob'u) — DTO değil, `IFormFile`.
- **Transcribe yanıtı**: `{ text: string }` — çevrilen metin (boşsa boş string + edge mesajı). Sözleşme `contracts/transcribe.md`.
- **İstemci taslağı**: `navigate` router-state `{ draft: string }` — ChatWorkspace composer'a konur, GÖNDERİLMEZ.

## Durum (istemci bileşeni — kalıcı değil)

`VoiceAsk` bileşen durumu: `idle → requesting-permission → recording(elapsed) → transcribing → (text→composer) | error`. İptal her an → `idle` (stream kapanır). Bu UI state'i, domain state'i değil.
