# Feature Specification: EC pay-by-link rewire (Ayşe kartla ödeme)

**Feature Branch**: `094-ec-pay-by-link-rewire`

**Created**: 2026-10-23

**Status**: Draft

**Input**: EC'yi PG ödeme sağlayıcısına yeniden kablola; 093'te sökülen ödeme girişini yeni
pay-by-link kontratıyla canlandır. Ayşe sepetten kartla uçtan uca ödeyip siparişini tamamlar.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ayşe sepetini kartla öder (Priority: P1)

Ayşe kendi AI istemcisinde "öde" der. Sistem sepetini tutara çevirir, güvenli bir hosted ödeme
sayfasının linkini chat'e döner. Ayşe linki açar, kartını PG sayfasında girer, ödeme geçer; siparişi
otomatik onaylanır, stok düşer. Kart bilgisi hiçbir an EC/agent/chat yüzeyinden geçmez.

**Why this priority**: Epiğin tüm değeri bu — bugün müşteri kartla alışveriş tamamlayamıyor
(`start_payment` = PAYMENT_DISABLED). Bu akış çalışmadan hiçbir satış kapanmaz.

**Independent Test**: start_payment çağrısı → geçerli hosted link döner → PG ödeme-başarılı
webhook'u → sipariş Confirmed + stok commit. Tek başına Ayşe'nin satın alımını uçtan uca doğrular.

**Acceptance Scenarios**:

1. **Given** Ayşe'nin Live mağazada dolu sepeti var, **When** start_payment çağrılır, **Then** Pending
   sipariş yaratılır ve chat'e hosted ödeme linki döner (kart alanı chat'te YOK).
2. **Given** hosted linkte geçerli kart girilir, **When** PG ödeme-başarılı webhook'u gelir, **Then**
   ödeme başarılı işaretlenir, sipariş Confirmed olur, stok düşer, müşteri bilgilendirilir.
3. **Given** ödeme reddedilir, **When** PG ödeme-başarısız webhook'u gelir, **Then** ödeme başarısız
   işaretlenir, sipariş iptal edilir, stok rezerve edilmez.

---

### User Story 2 - Hatalı/çift webhook sipariş bütünlüğünü bozmaz (Priority: P2)

PG webhook'u tekrar gönderebilir (retry) ya da sahte/değiştirilmiş bir çağrı gelebilir. Sistem imzasız
veya tutarı oynatılmış webhook'u reddeder; aynı başarı/başarısızlık webhook'u iki kez gelse de sipariş
yalnız bir kez ilerler.

**Why this priority**: Para ve sipariş bütünlüğü. İmza/idempotentlik olmadan çift-onay veya sahte-onay
mümkün olur; P1 çalışsa bile güvenli değildir.

**Independent Test**: Aynı başarı webhook'unu iki kez gönder → sipariş tek kez Confirmed. İmzasız/yanlış
imzalı webhook gönder → reddedilir, durum değişmez. Tutarı oynatılmış webhook → reddedilir.

**Acceptance Scenarios**:

1. **Given** geçerli bir ödeme beklemede, **When** aynı başarı webhook'u ikinci kez gelir, **Then**
   sipariş durumu değişmez (ilk işlem kalır), hata üretilmez.
2. **Given** webhook imzası geçersiz, **When** webhook gelir, **Then** istek reddedilir ve hiçbir durum
   değişmez (bilgi sızdırmadan).
3. **Given** webhook'taki tutar beklenen tutardan farklı, **When** webhook gelir, **Then** reddedilir
   (anti-tamper).

---

### User Story 3 - Satışa kapalı mağazada ödeme başlamaz (Priority: P3)

Mağaza-merchant PG'de aktif (Live) değilse, Ayşe ödeme başlatmaya çalıştığında sistem anlaşılır bir
"mağaza henüz satışa açık değil" cevabı döner — yarım sipariş veya kafa karıştıran hata olmadan.

**Why this priority**: Doğruluk ve temiz başarısızlık. Nadir ama kullanıcıya net mesaj + kirli veri
engeli sağlar. Gate PG'de zaten var; EC yalnız yüzeye taşır.

**Independent Test**: Live olmayan merchant ile start_payment → "mağaza satışa kapalı" cevabı, Pending
sipariş kalıcı olmaz.

**Acceptance Scenarios**:

1. **Given** mağaza-merchant Live değil, **When** start_payment çağrılır, **Then** anlaşılır
   "satışa kapalı" cevabı döner ve ödeme linki üretilmez.

---

### Edge Cases

- **Boş sepet / tutar ≤ 0**: start_payment ödeme başlatmaz, anlaşılır hata döner.
- **PG ulaşılamaz / link üretilemez**: Ayşe'ye geçici hata; kalıcı Pending sipariş kirliliği bırakılmaz.
- **Webhook siparişten önce gelir / bilinmeyen referans**: eşleşme yoksa reddedilir (fail-closed).
- **Aktif indirim süresi ödeme sırasında değişir**: tutar start_payment anındaki etkin fiyattan hesaplanır
  (link üretildikten sonra sabittir; webhook tutar guard'ı bunu doğrular).
- **Ödeme başarısızken stok**: stok yalnız ödeme başarısından sonra commit edilir; başarısızlıkta düşülmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, `start_payment` çağrıldığında müşterinin sepet kalemlerini okuyup **liste fiyatı −
  aktif kampanya indirimi** ile ödenecek tutarı hesaplamalı (adres chat-only akışında toplanmaz).
- **FR-002**: Sistem, ödeme başlatıldığında Pending bir sipariş + bir ödeme niyeti (payment intent)
  yaratmalı; sipariş referansı ödeme niyetiyle ilişkilendirilmeli.
- **FR-003**: Sistem, PG'den tek-kullanımlık hosted ödeme linki talep edip dönen opak ödeme referansını
  ödeme niyetine yazmalı ve hosted link'i müşteriye (chat) döndürmeli.
- **FR-004**: Kart bilgisi (PAN/CVV/son kullanma) **hiçbir** EC/agent/chat/MCP-dönüş/log/trace yüzeyinde
  görünmemeli — kart yalnız PG hosted sayfasında girilir (Anayasa İlke VIII).
- **FR-005**: Sistem, PG'den gelen ödeme-sonuç webhook'unu kabul etmeli; webhook imzası doğrulanmalı,
  imzasız/yanlış imzalı istek **reddedilmeli** (fail-closed, bilgi sızdırmadan).
- **FR-006**: Sistem, webhook'u opak ödeme referansıyla doğru ödeme niyetine eşlemeli ve webhook'taki
  tutarın beklenen tutara eşit olduğunu doğrulamalı (eşit değilse reddet — anti-tamper).
- **FR-007**: Başarılı webhook ödeme niyetini başarılı işaretlemeli ve ödeme-başarılı olayını yayınlamalı;
  başarısız webhook başarısız işaretleyip ödeme-başarısız olayını yayınlamalı.
- **FR-008**: Ödeme-sonuç işlemi **idempotent** olmalı — aynı başarı/başarısızlık webhook'u birden çok
  kez gelse de sonuç bir kez uygulanmalı (retry güvenli).
- **FR-009**: Ödeme-başarılı olayı var olan sipariş koreografisini (092) tetiklemeli: stok commit →
  sipariş onayı → tamamlanma yayını. Koreografi davranışı DEĞİŞTİRİLMEMELİ, yalnız tetiklenmeli.
- **FR-010**: Stok commit başarısız olursa sipariş iptal edilmeli ve (ödeme çekilmişse) manuel iade için
  iz bırakılmalı (092 mevcut davranışı).
- **FR-011**: Mağaza-merchant satışa kapalı (Live değil) ise start_payment ödeme linki üretmemeli ve
  müşteriye anlaşılır "mağaza satışa kapalı" cevabı dönmeli (PG'nin fail-fast kararı yüzeye taşınır).
- **FR-012**: `start_payment` tool adı dış sözleşme olarak KORUNMALI; yalnız davranışı canlandırılmalı
  (dış istemci sözleşmesi kırılmaz).
- **FR-013**: Sistem, hosted ödemeden sonra tarayıcı dönüşü için basit bir dönüş ucu sağlamalı; gerçek
  sipariş durumu dönüş ekranından DEĞİL webhook'tan belirlenmeli (akış chat-only).
- **FR-014**: Tutar yalnız TRY olmalı; tutar sıfırdan büyük olmalı (aksi halde ödeme başlatılmaz).

### Key Entities *(include if feature involves data)*

- **Sipariş (Order)**: Müşterinin satın alma kaydı; Pending doğar, ödeme+stok sonrası Confirmed ya da
  başarısızlıkta Cancelled. Ödeme niyetiyle sipariş referansı üzerinden ilişkili.
- **Ödeme niyeti (PaymentIntent)**: Bir ödemenin durum makinesi (beklemede → başarılı/başarısız); PG'nin
  opak ödeme referansını + beklenen tutarı tutar. Kart verisi TUTMAZ. (Payment BC'de mevcut.)
- **PG ödeme sonucu (webhook)**: PG'den gelen imzalı sonuç bildirimi (opak ödeme referansı, sipariş
  referansı, durum, tutar). EC bunu yalnız doğrulanmış biçimde işler.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ayşe, Live mağazada dolu sepetiyle "öde" dedikten sonra geçerli bir hosted ödeme linkini
  tek adımda (ek bilgi istenmeden) alır.
- **SC-002**: Hosted sayfada başarılı kart ödemesi sonrası sipariş, ek müşteri eylemi gerekmeden Confirmed
  olur ve stok düşer (uçtan uca tamamlanır).
- **SC-003**: Kart bilgisi hiçbir EC/agent/log/trace yüzeyinde görünmez (%0 sızıntı — tarama ile doğrulanır).
- **SC-004**: Aynı ödeme-sonuç webhook'u birden çok kez işlense de sipariş en fazla bir kez ilerler
  (çift-onay %0).
- **SC-005**: İmzasız, yanlış imzalı veya tutarı oynatılmış webhook'ların %100'ü reddedilir.
- **SC-006**: Ödeme başlatılamadığında (boş sepet, satışa kapalı mağaza, PG hatası) kalıcı Pending sipariş
  kirliliği oluşmaz.

## Assumptions

- **İlk sürüm NonSecure** (3D-Secure yok); gerçek banka yerine SimBank; yalnız TRY; dönüş akışı chat-only
  (thank-you web sayfası yok); settlement/komisyon/cüzdan kapsam dışı.
- **PG tarafı hazır ve canlı** (spec 089/090 + 091 onboarding): `POST /payment-links`, hosted kart sayfası,
  NonSecure charge, HMAC imzalı sonuç webhook'u, STORE_NOT_LIVE gate — bu epikte PG koduna dokunulmaz.
- **PaymentIntent aggregate + PaymentSucceeded/PaymentFailed fanout + 092 koreografi EC'de mevcut** (093'te
  korundu, uykuda); yeniden kullanılır, davranışları yeniden yazılmaz.
- **EC↔PG kontratı = REST + HMAC webhook** (gRPC değil — dış sağlayıcı sınırı); EC-içi Order↔Payment = S2S.
- **Merchant Active-gate PG'de** — EC kendi aktiflik kontrolünü kurmaz, PG'nin STORE_NOT_LIVE cevabını taşır.
- Dev sırları (StoreApiKey, CallbackSecret, MerchantId) user-secrets/config'te sağlanır; koda gömülmez.
- Çok-host dev koşumu: EC ve PG ayrı Aspire host'ları, EC→PG sabit localhost portuyla çözülür
  (cross-host service-discovery değil).
