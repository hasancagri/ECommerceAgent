# Feature Specification: Sesle Kâşif'e Soru (Voice Ask)

**Feature Branch**: `020-voice-ask`

**Created**: 2026-11-19

**Status**: Draft

**Input**: Kullanıcı herhangi bir müşteri sayfasındayken sesle Kâşif'e soru sorabilsin — belirli bir sayfaya gitmeye zorlamadan. Konuş → metne çevrilir → metin sohbet kutusuna düşer → kullanıcı gözden geçirip gönderir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sesle soru sor (Priority: P1) 🎯 MVP

Kullanıcı herhangi bir müşteri sayfasında (keşfet, kitaplık, sepet, kitap detayı…) aklına bir soru gelir. Üstteki mikrofona dokunur, sorusunu söyler, bitirir. Kısa bir an içinde söyledikleri yazıya dönüşüp sohbet giriş kutusuna düşer. Kullanıcı metni görür, gerekirse düzeltir, gönderir — Kâşif yanıtlar.

**Why this priority**: Çekirdek değer bu — yazmak yerine konuşarak soru sorma. Tek başına tam bir MVP: mikrofon → metin → gönder zinciri çalışırsa özellik değerini verir.

**Independent Test**: Keşfet sayfasında mikrofona bas, "Dostoyevski kitapları" de, bitir → metin sohbet kutusuna düşer → gönder → Kâşif sonuç döner. Sayfadan sayfaya gitmeye gerek kalmadan test edilir.

**Acceptance Scenarios**:

1. **Given** kullanıcı keşfet sayfasında ve mikrofon izni verilmiş, **When** mikrofona basıp konuşur ve bitirir, **Then** söyledikleri yazıya dönüşüp sohbet giriş kutusuna düşer (henüz gönderilmez).
2. **Given** metin kutuya düştü, **When** kullanıcı gönder'e basar, **Then** soru Kâşif'e gider ve yanıt/kartlar görünür.
3. **Given** kullanıcı kitaplık gibi başka bir müşteri sayfasında, **When** sesle soru sorar, **Then** sohbet yüzeyi açılır ve metin orada giriş kutusuna düşer (otomatik gönderilmez).
4. **Given** transcribe sonucu yanlış/eksik, **When** kullanıcı göndermeden metni düzeltir, **Then** düzeltilmiş metin gönderilir.

---

### User Story 2 - Kaydı iptal / vazgeç (Priority: P2)

Kullanıcı konuşmaya başlar ama yanlış söyler ya da vazgeçer. Kayıt sırasında iptal eder; hiçbir şey yazıya çevrilmez, kutu boş kalır, baştan deneyebilir.

**Why this priority**: Kontrol + güven. İptal olmazsa kullanıcı her yanlış başlangıçta istemsiz metin/istekle karşılaşır. Küçük ama günlük kullanımı rahatlatır.

**Independent Test**: Mikrofona bas, konuşmaya başla, iptale bas → kutu boş kalır, çevirme yapılmaz, mikrofon boşta konuma döner.

**Acceptance Scenarios**:

1. **Given** kayıt sürüyor, **When** kullanıcı iptale basar, **Then** kayıt atılır, çevirme yapılmaz, giriş kutusu değişmez.
2. **Given** kayıt sürüyor, **When** kullanıcı maksimum süreye (makul üst sınır) ulaşır, **Then** kayıt otomatik durur ve o ana kadarki ses çevrilir (kullanıcı kaybolmaz).

---

### User Story 3 - İzin yok / çevirme başarısız → nazik düşüş (Priority: P3)

Mikrofon izni reddedildi ya da çevirme servisi geçici erişilemez. Kullanıcı ne olduğunu anlar ve yazarak devam edebilir — akış kilitlenmez.

**Why this priority**: Dayanıklılık. Sesli yol hiçbir zaman tek yol değil; yazma her zaman açık. Ama sessiz başarısızlık kullanıcıyı şaşırtır.

**Independent Test**: Mikrofon iznini reddet → mikrofona bas → nazik bir uyarı görünür ("mikrofon erişimi kapalı"), yazma yolu etkilenmez.

**Acceptance Scenarios**:

1. **Given** mikrofon izni reddedilmiş, **When** kullanıcı mikrofona basar, **Then** izin gerektiğini söyleyen kısa bir uyarı görünür, uygulama çalışmaya devam eder.
2. **Given** çevirme servisi hata döner, **When** kullanıcı kaydı bitirir, **Then** "şu an çeviremedim, tekrar dene ya da yazarak sor" benzeri nazik bir mesaj görünür ve giriş kutusu bozulmaz.

---

### Edge Cases

- **Boş/sessiz kayıt**: kullanıcı hiç konuşmadan bitirir → boş metin; kutuya bir şey düşmez, nazik "bir şey duyamadım" uyarısı.
- **Çok uzun konuşma**: maksimum süre üst sınırına takılır; o ana kadarki kısım çevrilir (US2-2).
- **Gürültülü/anlaşılmaz ses**: düşük kalite metin dönebilir → kullanıcı composer'da düzeltir (US1-4 bunu korur; sistem "doğru duydum" garantisi vermez).
- **Çok hızlı art arda kayıt / abuse**: aşırı kullanım makul bir üst sınırla sınırlanır (gün/oturum başına), aşılırsa nazik "biraz sonra tekrar dene".
- **Kayıt sırasında sayfa değişimi/kapatma**: devam eden kayıt iptal sayılır, arka planda asılı iş kalmaz.
- **Giriş/admin ekranları**: sesli mikrofon oralarda görünmez (yalnız müşteri yüzeyinde).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Kullanıcı, müşteri yüzeyindeki HER sayfada erişilebilir tek bir sesli-soru girişi (mikrofon) görebilMELİ; giriş ve admin ekranlarında görünMEMELİ.
- **FR-002**: Kullanıcı mikrofonu başlatıp sesini kaydedebilMELİ; kayıt sırasında kaydın sürdüğü ve geçen süre görsel olarak belli OLMALI.
- **FR-003**: Kullanıcı kaydı BİTİREBİLMELİ (çevir) ve İPTAL EDEBİLMELİ (at); iptal hiçbir çevirme yapmaMALI ve giriş kutusunu değiştirmeMELİ.
- **FR-004**: Sistem, bitirilen kaydı metne çevirMELİ ve metni sohbet giriş kutusuna yerleştirMELİ — OTOMATİK GÖNDERMEMELİ (gönderme kararı kullanıcınındır).
- **FR-005**: Kullanıcı başka bir müşteri sayfasındayken sesle soru sorarsa, sistem sohbet yüzeyini açMALI ve metni orada giriş kutusuna düşürMELİ (otomatik göndermeden).
- **FR-006**: Kullanıcı çevrilen metni göndermeden önce düzenleyebilMELİ.
- **FR-007**: Sistem, mikrofon izni yoksa/reddedilmişse nazik ve anlaşılır bir uyarı gösterMELİ; yazma yolu etkilenmeMELİ.
- **FR-008**: Çevirme başarısız olursa sistem nazik bir hata göstermeli ve giriş kutusunu bozmaMALI (kullanıcı yazarak devam edebilMELİ).
- **FR-009**: Sistem kaydı makul bir maksimum süreyle sınırlaMALI; sınıra ulaşınca kaydı durdurup o ana kadarki sesi çevirMELİ.
- **FR-010**: Sesli-soru kullanımı makul bir üst sınırla (gün/oturum başına) korunMALI; aşımda nazik bir sınır mesajı dönMELİ (maliyet/kötüye-kullanım).
- **FR-011**: Ses verisi ve çevirme, kullanıcının tarayıcısından ÜÇÜNCÜ TARAF servise DOĞRUDAN gitmeMELİ; sistemin kendi sunucusu aracılık etMELİ (gizli kimlik/anahtar kullanıcı yüzeyine/istemciye SIZMAMALI — Anayasa İlke VIII).
- **FR-012**: Sesli yol anonim kullanıcıya da AÇIK olMALI (mevcut sohbet gibi — giriş zorunlu değil).
- **FR-013**: Sesli soruyla gelen metin, yazıyla sorulan soruyla AYNI Kâşif yoluna girMELİ (ayrı bir yanıt/arama davranışı doğurmaz).

### Key Entities

Yeni kalıcı varlık YOK. Ses geçicidir (çevrilince tutulmaz); çevrilen metin mevcut sohbet turuna girer. Mevcut sohbet kaydı davranışı (kayıtlı kullanıcıda transkript) değişmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kullanıcı, herhangi bir müşteri sayfasında başka sayfaya gitmeden, tek bir mikrofon etkileşimiyle sesli soru başlatabilir (keşiften gönderime ≤ 3 dokunuş: mikrofon → bitir → gönder).
- **SC-002**: Normal uzunlukta (≤ ~15 sn) bir soruda, kaydı bitirmekten metnin kutuya düşmesine kadar geçen süre kullanıcıya kabul edilebilir gelir (algılanan "birkaç saniye" bandında).
- **SC-003**: Açık Türkçe konuşmada, çevrilen metin kullanıcının göndermeden düzeltmesine gerek kalmadan kullanılabilir oranı yüksektir (ilk denemede gönderilebilir).
- **SC-004**: Mikrofon izni yok / çevirme hatası durumlarının %100'ünde kullanıcı anlaşılır bir mesaj görür ve yazarak kesintisiz devam edebilir (sessiz başarısızlık YOK).
- **SC-005**: Gizli anahtar/kimlik hiçbir koşulda istemciye/kullanıcı yüzeyine düşmez (İlke VIII — denetlenebilir).

## Assumptions

- **Çevirme sunucuda, mevcut sağlayıcıyla**: ses metne sistemin kendi sunucu tarafında, hâlihazırda yapılandırılmış AI sağlayıcısıyla (ScoutAgent'ın OpenAI bağlantısı) çevrilir — yeni sağlayıcı/hesap eklenmez. (Benim çağrım.)
- **Chunk (kaydet-sonra-çevir) model**: canlı/kelime-kelime dikte DEĞİL; kullanıcı konuşur, bitirir, metin toptan döner. Canlı streaming kapsam dışı. (Kararlaştırıldı.)
- **Metin composer'a düşer, kullanıcı gönderir** (otomatik gönderme yok) — yanlış çevirmeyi göndermeden düzeltme imkânı; gönder tuşu kullanıcının onayıdır. (Benim çağrım; kullanıcı onayladı.)
- **Giriş noktası paylaşılan müşteri yüzeyinde** (her müşteri sayfasının ortak kabuğu); giriş/admin yüzeyleri hariç.
- **Mevcut "soru → sohbet yüzeyi" yönlendirme deseni yeniden kullanılır** (keşif kartlarının zaten kullandığı yol), ama otomatik-gönder yerine metin kullanıcıya bırakılır.
- **Maksimum kayıt süresi** makul bir üst sınır (≈ 60 sn) — uzun dikte hedef değil, kısa soru.
- **Türkçe birincil dil**; diğer diller hedef değil (sağlayıcı yine de genel dil desteği sunar, ama ürün Türkçeye göre değerlendirilir).

### Kapsam Dışı (bu dilimde değil)

- Canlı/streaming dikte (konuşurken kelime-kelime), OpenAI Realtime, tarayıcı Web Speech API.
- Sesli yanıt (metin-okuma / TTS) — Kâşif konuşmaz, yalnız dinler.
- Ses kaydının saklanması/geçmişi, ses dosyası indirme.
- Giriş/admin ekranlarında sesli giriş.
