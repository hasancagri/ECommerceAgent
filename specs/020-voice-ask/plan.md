# Implementation Plan: Sesle Kâşif'e Soru (Voice Ask)

**Branch**: `020-voice-ask` | **Date**: 2026-11-19 | **Spec**: [spec.md](./spec.md)

**Input**: `specs/020-voice-ask/spec.md`

## Summary

Kullanıcı her müşteri sayfasında sesle Kâşif'e soru sorar. Tarayıcı sesi kaydeder (`MediaRecorder`), ScoutAgent'ın yeni `POST /api/chat/v1/transcribe` ucuna yollar; uç OpenAI `gpt-4o-transcribe` ile (sunucu-tarafı, mevcut key) metne çevirip döner. Metin `/sohbet` composer'ına düşer, kullanıcı gözden geçirip gönderir (otomatik gönderme yok). Yeni kalıcı veri yok — ses geçici, metin mevcut sohbet turuna girer. Çevirme stateless yardımcı uç; rate-cap `DailyUsageGuard` yeniden kullanılır.

## Technical Context

**Language/Version**: Backend .NET 10 (ScoutAgent.Api); Frontend React 19 + TypeScript + Vite + Tailwind v4 (ECommerceWeb, ayrı repo)

**Primary Dependencies**: Backend — `OpenAI` nuget `AudioClient` (transcription), ScoutAgent mevcut OpenAI kablosu (`ChatClientFactory`/`ScoutOptions.ApiKey`), Wolverine (gerekmez — slice minimal), Marten (yalnız DailyUsageGuard belgesi). Frontend — tarayıcı `MediaRecorder` API, mevcut gateway client, react-router (`navigate` + state)

**Storage**: Yeni tablo/belge YOK. Ses hiç saklanmaz. Rate-cap için mevcut `ScoutUsageDaily` Marten belgesi yeniden kullanılır (yeni partition anahtarı).

**Testing**: Saf birim (İlke VI) — ses/HTTP/OpenAI yolu host-gerekli → Aspire'da elle doğrulanır (çevirme ucu + React akışı). Saf test edilebilir birim azdır (ör. dosya-boyut/tip guard, partition anahtarı) → varsa test-first.

**Target Platform**: Modern tarayıcı (Chrome/Edge/Safari/Firefox — `MediaRecorder` + `getUserMedia`); masaüstü + mobil. Backend Linux (Aspire/k8s).

**Project Type**: web-service (ScoutAgent uç) + web-app (React global bileşen)

**Performance Goals**: Normal soru (≤15sn ses) için bitir→metin algılanan birkaç saniye (SC-002). Çevirme senkron tek çağrı.

**Constraints**: Gizli key istemciye SIZMAZ (İlke VIII, FR-011/SC-005) — çevirme hep sunucuda. Maks kayıt ≈60sn (FR-009). Rate-cap gün/oturum (FR-010). Anonim açık (FR-012).

**Scale/Scope**: 1 yeni backend uç + 1 React global bileşen (`VoiceAsk`) + PublicLayout header entegrasyonu + 2 composer'a metin enjeksiyonu (navigate/state). Canlı dikte/Realtime/TTS kapsam dışı.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden.*

- **İlke I (BC İzolasyonu):** Çevirme ucu ScoutAgent'ta yaşar (sohbetin sahibi BC). Yeni BC YOK; başka BC'nin DB/aggregate'ine dokunmaz. OpenAI = dış servis, ScoutAgent'ın mevcut kanalı. ✅
- **İlke III (VSA + CQRS):** Çevirme bir "query-benzeri" kullanıcı niyeti → `Domains/Conversations/Features` altında slice (`Transcribe`), endpoint-extension'a map. Minimal API. Repository yok. ✅
- **İlke IV (Result):** Uç `FeatureObjectResultModel<T>` döner; hatalar resource-kodlu (boş ses / çok büyük / sağlayıcı hata). ✅
- **İlke V (Scope):** Çevirme anonim-açık (sohbet gibi, FR-012); named-scope YOK. Rate-cap korur. ✅
- **İlke VI (Domain-TDD):** Yeni saf domain mantığı YOK (aggregate/event yok). Saf test edilebilir birim (ses tip/boyut guard, partition) varsa test-first; gerisi canlı doğrulama. ✅
- **İlke VIII (Sır insan-yüzeyde görünmez) — NON-NEGOTIABLE:** OpenAI key yalnız sunucuda (`ScoutOptions.ApiKey`); istemci yalnız kendi ucumuza ses yollar, key'i ASLA görmez (FR-011/SC-005). ✅
- **İlke VII (FLOW.md):** Çevirme domain süreci doğurmaz (stateless yardımcı, event yok) — ScoutAgent FLOW.md'ye "sohbet girişi sesle de olabilir" kenar notu yeter; yeni command-event-policy YOK.

Gate: **GEÇTİ** — ihlal yok.

## Project Structure

### Documentation (this feature)

```text
specs/020-voice-ask/
├── plan.md              # bu dosya
├── spec.md              # tamam
├── research.md          # Phase 0
├── data-model.md        # Phase 1 (yeni kalıcı veri yok — notlanır)
├── quickstart.md        # Phase 1
├── contracts/
│   └── transcribe.md    # POST /api/chat/v1/transcribe sözleşmesi
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code

```text
# Backend (ECommerceAgent repo)
src/agents/ScoutAgent.Api/
├── Domains/Conversations/
│   ├── ConversationEndpointExtension.cs          # + POST /transcribe map
│   └── Features/Queries/Transcribe.cs            # YENİ slice: ses → OpenAI AudioClient → {text}
├── Options/ScoutOptions.cs                       # + TranscribeModel + TranscribeAnon/UserPerDay (cap)
├── RateLimiting/  (DailyUsageGuard/ScoutUsageKey yeniden kullanılır — yeni partition "tx:")
└── (OpenAI AudioClient: ScoutOptions.ApiKey'den; ChatClientFactory deseni)

# Frontend (ECommerceWeb repo)
src/
├── components/VoiceAsk.tsx                        # YENİ: mic + kayıt şeridi + MediaRecorder + POST
├── layouts/PublicLayout.tsx                       # header'a <VoiceAsk/> (lg + mobil)
├── api/ (transcribe çağrısı — mevcut gateway client)
└── pages/ChatWorkspace.tsx + DiscoverWelcome.tsx  # router-state'ten gelen metni composer'a koy (gönderme yok)
```

**Structure Decision**: Backend uç ScoutAgent'ta (sohbet sahibi), VSA slice `Features/Queries/Transcribe` (kullanıcı-tetikli, okuma-benzeri). Frontend tek paylaşılan `VoiceAsk` bileşeni PublicLayout header'da → her müşteri sayfası; metin mevcut router-state deseniyle `/sohbet` composer'ına taşınır (otomatik-gönder KAPALI).

## Complexity Tracking

İhlal yok — tablo boş. (Yeni BC yok, saga yok, senkron uç meşru: dış sağlayıcı çağrısı ScoutAgent'ın kendi kanalı, tek-yön ses→metin.)
