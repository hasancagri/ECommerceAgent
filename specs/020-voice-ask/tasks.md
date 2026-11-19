---
description: "Task list — Sesle Kâşif'e Soru (Voice Ask)"
---

# Tasks: Sesle Kâşif'e Soru (Voice Ask)

**Input**: `specs/020-voice-ask/` (plan.md, spec.md, research.md, data-model.md, contracts/transcribe.md, quickstart.md)

**Tests**: Yeni saf domain mantığı YOK (aggregate/event yok) → UI/endpoint/dış-çağrı canlı doğrulanır (İlke VI). Tek saf-test edilebilir birim = ses boyut/tip guard (çıkarılırsa test-first). TDD istenmedi; gerisi quickstart elle.

**İki repo**: Backend = `ECommerceAgent` (ScoutAgent.Api). Frontend = `ECommerceWeb` (ayrı repo, `~/dev/ECommerceWeb`). Gateway `/api/chat/**` → scout zaten var → yeni rota gerekmez.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup

- [X] T001 `ScoutOptions`'a çevirme ayarları ekle: `TranscribeModel` (varsayılan `gpt-4o-transcribe`), `TranscribeAnonPerDay`, `TranscribeUserPerDay`, `MaxAudioBytes` (ör. ~10MB) — `src/agents/ScoutAgent.Api/Options/ScoutOptions.cs`
- [X] T002 OpenAI `AudioClient` sağlayıcısı: `ScoutOptions.ApiKey` + `TranscribeModel`'den `AudioClient` üret (`ChatClientFactory` deseni; Singleton/factory) — `src/agents/ScoutAgent.Api/Agents/AudioClientFactory.cs`

---

## Phase 2: Foundational (Blocking) — çevirme ucu (US1-3 hepsinin ön koşulu)

- [X] T003 [P] Test (saf birim): ses boyut/tip guard — boş/çok-büyük/desteklenmeyen tip reddi, geçerli kabul — `tests/ScoutAgent.Api.Tests/TranscribeGuardTests.cs` (FAIL)
- [X] T004 Çevirme guard'ını saf birime çıkar + implemente et (T003 yeşil): boyut/tip doğrulama — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Queries/TranscribeGuard.cs` (saf)
- [X] T005 `Transcribe` slice: `IFormFile` → guard → `AudioClient.TranscribeAudioAsync` (dil ipucu `tr`, model `TranscribeModel`) → `{ text }`; ses DİSKE YAZILMAZ; `FeatureObjectResultModel<Response>`; hata → Result error-kod — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Queries/Transcribe.cs`
- [X] T006 Endpoint map `POST /api/chat/v1/transcribe` (opsiyonel bearer, multipart): rate-cap `DailyUsageGuard.TryConsumeAsync("tx:"+partition, cap)` (anon/login cap `ScoutOptions`'tan) + guard → 400/429/503; hata'da `ReleaseAsync` (kota iade) — `src/agents/ScoutAgent.Api/Domains/Conversations/ConversationEndpointExtension.cs`
- [X] T007 Wolverine discovery + DI: `Transcribe` handler `*Handler` değilse `IncludeType`; `AudioClientFactory` kaydı (Scrutor/elle) — `src/agents/ScoutAgent.Api/Program.cs`

**Checkpoint**: `POST /api/chat/v1/transcribe` curl ile (ör. örnek webm) metin döner; kota + guard + hata yolları çalışır.

---

## Phase 3: User Story 1 - Sesle soru sor (Priority: P1) 🎯 MVP

**Goal**: Her müşteri sayfasından sesle soru → metin composer'a → kullanıcı gönderir.

**Independent Test**: Keşfet'te mic → "Dostoyevski kitapları" → bitir → metin composer'a → gönder → Kâşif yanıtlar (quickstart Senaryo 1-3).

- [X] T008 [P] [US1] Çevirme API istemcisi: multipart `file` → `POST /api/chat/v1/transcribe` → `{text}` (mevcut gateway client) — `ECommerceWeb/src/api/transcribe.ts`
- [X] T009 [US1] `VoiceAsk` bileşeni (mutlu yol): mic butonu → `getUserMedia`+`MediaRecorder` kayıt → bitir → T008 → metin; kayıt şeridi (süre sayacı + bitir↑); `MediaRecorder.isTypeSupported` ile webm/opus|mp4 seç — `ECommerceWeb/src/components/VoiceAsk.tsx`
- [X] T010 [US1] `VoiceAsk`'i `PublicLayout` header'a mount et (lg + mobil; AuthLayout/AdminLayout'a DEĞİL) — `ECommerceWeb/src/layouts/PublicLayout.tsx`
- [X] T011 [US1] Metin akışı: `/sohbet`teyse composer `setInput`; değilse `navigate('/sohbet',{state:{draft}})`. ChatWorkspace + DiscoverWelcome router-state `draft`→composer input (OTOMATİK GÖNDERME YOK; mevcut `q` auto-send yolu bozulmaz) — `ECommerceWeb/src/pages/ChatWorkspace.tsx` + `DiscoverWelcome.tsx` + `VoiceAsk.tsx`
- [ ] T012 [US1] Canlı doğrula (quickstart Senaryo 1+2+3): sesle soru→metin→composer→gönder; başka sayfadan; düzelt+gönder.

**Checkpoint**: Sesle soru uçtan uca çalışır (MVP).

---

## Phase 4: User Story 2 - Kaydı iptal / maks süre (Priority: P2)

**Goal**: İptal + 60sn cap; kaynak sızıntısı yok.

**Independent Test**: Kayıt sırasında iptal → kutu boş, çevirme yok, mic kapanır (Senaryo 4); 60sn+ konuş → otomatik durur+çevirir (Senaryo 5).

- [X] T013 [US2] `VoiceAsk`: iptal (çöp) butonu → kayıt at, POST yok, composer değişmez; maks süre (≈60sn) → otomatik bitir+çevir; iptal/bitir/unmount'ta `MediaStreamTrack.stop()` (mic serbest) — `ECommerceWeb/src/components/VoiceAsk.tsx`
- [ ] T014 [US2] Canlı doğrula (Senaryo 4+5): iptal temiz; maks-süre o ana kadarki sesi çevirir; mic ışığı söner.

**Checkpoint**: Kayıt kontrolü + kaynak temizliği.

---

## Phase 5: User Story 3 - İzin yok / hata → nazik düşüş (Priority: P3)

**Goal**: Sessiz başarısızlık YOK; yazma hep açık.

**Independent Test**: İzin reddi → nazik uyarı, yazma çalışır (Senaryo 6); sağlayıcı hata → "çeviremedim, yazarak dene"; kota aşımı → 429 nazik (Senaryo 7).

- [X] T015 [US3] `VoiceAsk` hata durumları: `getUserMedia` reddi → nazik uyarı; boş/sessiz ses (`text:""`) → "bir şey duyamadım"; 503 → "çeviremedim, yazarak dene"; 429 → "biraz sonra tekrar dene"; composer hiçbir durumda bozulmaz — `ECommerceWeb/src/components/VoiceAsk.tsx`
- [ ] T016 [US3] Canlı doğrula (Senaryo 6+7): izin reddi · sağlayıcı hata (key geçici boz) · kota aşımı (anon cap); yazma yolu her durumda çalışır.

**Checkpoint**: Dayanıklı — her hata nazik + yazma kesintisiz.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T017 İlke VIII doğrula (quickstart Senaryo 8): DevTools Network'te OpenAI'ye DOĞRUDAN istek YOK (yalnız `/api/chat/v1/transcribe`); yanıt/konsol/sunucu-log'da OpenAI key YOK; ses log'lanmaz.
- [X] T018 [P] ScoutAgent `FLOW.md` kenar notu: "sohbet girişi sesle de olabilir (transcribe → composer, auto-send yok)"; domain süreci değişmedi — yalnız not — `src/agents/ScoutAgent.Api/FLOW.md`
- [ ] T019 [P] Giriş/admin dışlama doğrula: `/giris` (AuthLayout) + admin ekranlarında mic GÖRÜNMEZ.
- [ ] T020 `dotnet build && dotnet test` yeşil (backend); `npx tsc --noEmit` yeşil (ECommerceWeb); tam quickstart (Senaryo 1-8) Aspire'da elle.

---

## Bağımlılıklar

- **Setup (T001-T002)** → Foundational.
- **Foundational (T003-T007)** → US1-3 hepsinin ön koşulu (çevirme ucu). T003 test → T004 impl (test-first).
- **US1 (T008-T012)** = MVP; Foundational sonrası. T008 (API client) ∥ T009 başlar ama T009 T008'e bağlı.
- **US2 (T013-T014)** + **US3 (T015-T016)** US1'deki `VoiceAsk`'e eklenir (aynı dosya → US1 sonrası, sıralı).
- **Polish (T017-T020)** en son.

## Paralel fırsatlar

- Backend (ECommerceAgent: T003-T007) ∥ Frontend API client (T008) — ayrı repo, bağımsız. Foundational ucu bitmeden T012 canlı test edilemez.
- T018/T019 [P] bağımsız (farklı dosya/repo).

## MVP

**US1 (T001-T012)** — sesle soru uçtan uca. US2/US3 dayanıklılık+kontrol artışı; sonra eklenir.
