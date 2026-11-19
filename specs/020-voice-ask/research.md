# Research — Sesle Kâşif'e Soru (Phase 0)

Kaynak: spec.md + ScoutAgent mevcut kodu + OpenAI SDK. Sürüm-duyarlı noktalar (model id, SDK API) build anında teyit edilir.

## D1 — Çevirme sağlayıcısı + model

**Decision**: OpenAI `gpt-4o-transcribe`, sunucu-tarafı (ScoutAgent). `OpenAI` nuget `AudioClient` (`GetAudioClient(model).TranscribeAudioAsync(stream, filename, options)`), key `ScoutOptions.ApiKey`'den (ChatClientFactory ile aynı kaynak).

**Rationale**: OpenAI zaten kablolu (yeni vendor/hesap yok); Türkçe güçlü; tek REST/SDK çağrısı. Model `ScoutOptions.TranscribeModel`'e alınır (varsayılan `gpt-4o-transcribe`) → ileride değiştirilebilir. Dil ipucu `tr` verilir (doğruluk).

**Alternatives**: Web Speech API (client, canlı ama Türkçe değişken + key'siz ama kapsam dışı canlı-dikte); OpenAI Realtime (streaming, karmaşık — kapsam dışı); self-host Whisper (lokal-ML iştahı yok). **FLAG**: `gpt-4o-transcribe` kesin model-id + `AudioClient` API imzası (10.7.0 / `OpenAI` nuget sürümü) build'de doğrulanır; gerekirse `whisper-1` fallback.

## D2 — Ses formatı + aktarım

**Decision**: Tarayıcı `MediaRecorder` ile kayıt → `audio/webm;codecs=opus` (varsayılan; Safari `audio/mp4` düşebilir → tip runtime seçilir `MediaRecorder.isTypeSupported`). `multipart/form-data` ile tek dosya alanı (`file`) olarak uca. Sunucu stream'i doğrudan OpenAI'ye geçirir (diske yazmaz).

**Rationale**: webm/opus yaygın + küçük; OpenAI çoklu format kabul eder. Multipart = standart dosya yükleme, .NET `IFormFile`. Diske yazmama = İlke VIII (ses geçici, iz bırakma) + basitlik.

**Alternatives**: base64 JSON (şişer), ham PCM (gereksiz). **FLAG**: Safari codec'i (mp4/m4a) canlı doğrulanır; uç format-agnostik (dosya adı uzantısı + content-type'ı OpenAI'ye iletir).

## D3 — Rate-cap + kötüye kullanım

**Decision**: Mevcut `DailyUsageGuard.TryConsumeAsync(partition, cap, ct)` yeniden kullanılır; partition `ScoutUsageKey.Partition(userId, ip)` + çevirme için ayrı önek (ör. `tx:`) → sohbet kotasından BAĞIMSIZ sayaç. Cap `ScoutOptions.TranscribeAnonPerDay`/`TranscribeUserPerDay` (anon düşük, login yüksek). Aşım → 429 nazik mesaj. Dosya boyutu üst sınırı + süre (≈60sn) sunucuda da guard (istemciye güvenme).

**Rationale**: STT çağrısı sohbetten PAHALI olabilir → ayrı kota. Mevcut guard deseni (gün-başı Marten sayaç) hazır. Boyut/süre guard = maliyet tavanı + İlke IV Result.

**Alternatives**: sohbet kotasıyla ortak (STT sohbeti yiyip bitirir — reddedildi); kota yok (maliyet riski — reddedildi).

## D4 — Metin nereye düşer (akış)

**Decision**: Çevrilen metin OTOMATİK GÖNDERİLMEZ. İstemci: zaten `/sohbet`teyse composer input'una yaz (`setInput`); başka müşteri sayfasındaysa `navigate('/sohbet', { state: { draft: text } })` → ChatWorkspace state'teki `draft`i composer'a koyar (göndermez). Mevcut DiscoverWelcome deseni "router-state ile /sohbet açıp OTOMATİK gönderiyor" → bunu uyarlarız: yeni `draft` state'i auto-send YAPMAZ (mevcut `q` auto-send yolu korunur, ayrı).

**Rationale**: FR-004/FR-006 — composer onay yüzeyi, yanlış-transcribe'ı göndermeden düzeltme. Mevcut yönlendirme altyapısı yeniden kullanılır, yalnız "gönder" yerine "taslak doldur" dalı eklenir.

**Alternatives**: otomatik gönder (yanlış-transcribe doğrudan aramaya gider — kullanıcı reddetti); yerinde overlay (yeni cevap yüzeyi + bağlam kopuk — reddedildi).

## D5 — Bileşen yeri (global)

**Decision**: Tek `<VoiceAsk>` bileşeni `PublicLayout` header'ında (lg + mobil). Kendi state'i: boşta / izin-isteniyor / kayıt (süre sayacı) / çeviriyor / hata. `getUserMedia` + `MediaRecorder`. Bitir → POST → metin → D4 akışı. İptal → stream durdur, POST yok.

**Rationale**: PublicLayout tam müşteri yüzeyini sarar (FR-001); giriş/admin layout'ları hariç → doğal kapsam. Tek bileşen = tek STT mantığı (DRY), composer'lara dokunmaz.

**Alternatives**: her composer'a ayrı mic (kopya + sayfaya bağımlı — reddedildi, kullanıcı "sayfaya hapsetme" dedi); App-kökü global (giriş/admin dahil — gereksiz).

## D6 — İzin + hata + edge

**Decision**: `getUserMedia` reddi → nazik uyarı (FR-007), yazma açık kalır. Boş/sessiz kayıt → sunucu boş metin → "bir şey duyamadım" (edge). Sağlayıcı hata → Result error-kodu → "şu an çeviremedim, yazarak dene" (FR-008). Maks süreye ulaşınca otomatik bitir + çevir (FR-009). Sayfa değişimi/unmount → MediaRecorder dur + stream track'leri kapat (asılı mic yok).

**Rationale**: Sessiz başarısızlık YOK (SC-004). Mic kaynağı her yolda serbest bırakılır (gizlilik + kaynak sızıntısı yok).
