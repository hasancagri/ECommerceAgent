# Contract — POST /api/chat/v1/transcribe

Ses → metin çevirme ucu. ScoutAgent'ta; gateway `/api/chat/**` → scout-agent. Sohbetle aynı grup (`/api/chat/v1`).

## İstek

```
POST /api/chat/v1/transcribe
Content-Type: multipart/form-data
Authorization: Bearer <token>   (OPSİYONEL — anonim serbest, FR-012)

form-data:
  file: <ses blob>   (audio/webm;codecs=opus | audio/mp4 — tarayıcıya göre)
```

- **Auth**: opsiyonel bearer (sohbet deseni — ConversationEndpointExtension `/messages` gibi). Token varsa `sub`→userId (kota partition + yüksek cap); yoksa anonim (IP partition + düşük cap).
- **Boyut/süre guard (sunucu)**: dosya boyutu üst sınırı (ör. ~10 MB) + ses süresi makul (≈60sn istemci cap'i; sunucu boyutla da korur). Aşımı reddet.

## Yanıt

**200 OK**
```json
{ "text": "Dostoyevski kitapları" }
```
- `text`: çevrilen metin. Boş/sessiz ses → `{ "text": "" }` (istemci "bir şey duyamadım" gösterir).

**400 Bad Request** — dosya yok / boş / çok büyük / desteklenmeyen tip. `FeatureObjectResultModel` hata gövdesi (resource-kod).

**429 Too Many Requests** — gün/oturum kota aşıldı (`DailyUsageGuard`). Nazik mesaj.

**503 Service Unavailable** — OpenAI erişilemez/çevirme hatası (FR-008). "Şu an çeviremedim, yazarak dene."

## Davranış

1. Dosya + boyut/tip guard → geçmezse 400.
2. Rate-cap `TryConsumeAsync("tx:"+partition, cap, ct)` → aşımda 429.
3. Ses stream'i OpenAI `AudioClient.TranscribeAudioAsync` (model `ScoutOptions.TranscribeModel`, dil ipucu `tr`) → metin. Ses DİSKE YAZILMAZ.
4. Hata → 503 + kota iade (`ReleaseAsync`, cevapsız kalan kota yanmasın — `/messages` deseni).
5. `{ text }` döner. Metin GÖNDERİLMEZ — istemci composer'a koyar (FR-004).

## İlke VIII (NON-NEGOTIABLE)

OpenAI key istemciye/yanıta/log'a ASLA girmez — yalnız sunucuda `ScoutOptions.ApiKey`. İstemci yalnız bu uca ses yollar; OpenAI'yi görmez. Ses yanıtta/log'da ekolanmaz.
