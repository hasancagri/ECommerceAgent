# Feature Specification: Discount.Api — Kampanya BC'sinin Dönüşü

**Feature Branch**: `003-discount-service`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "Discount.Api servisini ekleyelim (Excel verisi sonra)."

**Kademe**: Tam — yeni BC (yeni aggregate + DB + integration event kontratı).

## Bağlam

Discount 095'te sökülmüştü; kullanıcı sökümü hata ilan etti — yeni modelle dönüyor (tarihçe eski 079).
Kilitli ilkeler: indirim AYRI kavramdır, ürünün fiyatını EZMEZ (liste fiyatı Catalog'da değişmeden
durur; indirim oranı ayrı yaşar, indirimli fiyat okuyanda hesaplanır). Vitrin deseni Stock'la aynı:
Discount gerçeğin sahibi, Catalog üründe event-fed ORAN KOPYASI tutar — kompleks sorgular
("%20+ indirimli romanlar") tek SQL'de cevaplanır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin kampanya tanımlar (Priority: P1)

Admin bir ÜRÜNE yüzde indirim kampanyası tanımlar: oran + bitiş zamanı (başlangıç = tanım anı ya da
ileri tarih). Bir üründe aynı anda EN FAZLA BİR etkin indirim olur — yeni kampanya eskisini ezer
(son-gelen-kazanır, 079 kuralı).

**Why this priority**: Kampanyanın çekirdeği; duyuru/kopya bunun üstüne kurulur.

**Independent Test**: Saf domain testleri — oluşturma guard'ları (oran aralığı, zaman penceresi),
etkinlik hesabı.

**Acceptance Scenarios**:

1. **Given** geçerli oran (%1-%90) ve gelecekte bitiş, **When** kampanya tanımlanır, **Then** kampanya doğar; penceresi içindeyse ETKİNDİR.
2. **Given** oran 0, negatif ya da %90 üstü, **When** tanımlanır, **Then** reddedilir.
3. **Given** bitiş ≤ başlangıç, **When** tanımlanır, **Then** reddedilir.
4. **Given** etkin kampanya, **When** admin iptal eder, **Then** kampanya etkinliğini yitirir (pencere bitmeden).

---

### User Story 2 - İndirim oranı katalogdaki kopyayı besler (Priority: P2)

Bir ürünün ETKİN indirim oranı değiştiğinde (kampanya başladı/bitti/iptal edildi/ezildi) değişiklik
ürün kimliği + güncel oranla duyurulur; Catalog üründeki oran kopyasını günceller (oran yoksa
temizler). İndirimli fiyat saklanmaz — okuyan yüzey liste fiyatından hesaplar (fiyat değişse bile
tutarlı kalır).

**Why this priority**: Vitrinin "indirimli ürünler" filtresinin ve indirimli fiyat gösteriminin temeli.

**Independent Test**: Catalog domain testi — oran kopyası atanır/temizlenir, aralık guard'ı; kablo
canlı (Aspire) doğrulanır.

**Acceptance Scenarios**:

1. **Given** üründe etkin kampanya yok, **When** %20 kampanya etkinleşir, **Then** duyuru yayılır ve Catalog kopyası %20 olur.
2. **Given** kopya %20, **When** kampanya biter/iptal edilir, **Then** duyuru oran-yok taşır, kopya temizlenir.
3. **Given** kopya %20, **When** %30 kampanya ezer, **Then** kopya %30 olur.

---

### Edge Cases

- Zamanla etkinleşme/bitme: pencere sınırları geçildiğinde duyurunun yayılması bir iç süreç gerektirir (tanım anında değil YÜRÜRLÜK anında duyuru — brainstorm kararı). Süreç dakika hassasiyetinde yeterli.
- Catalog'da olmayan ürüne kampanya duyurusu → DLQ deseni (retry → error-queue), Stock'la aynı.
- Aynı duyurunun tekrar işlenmesi → mutlak oran taşıdığından idempotent.
- İndirim satışa-uygunluğu ETKİLEMEZ (IsSalable kriterine girmez).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Kampanya tek ürünü hedefler; oran %1-%90 aralığında, zaman penceresi geçerli (bitiş > başlangıç) olmalı. (Kategori/yazar hedefli kampanya SONRAKİ dilim.)
- **FR-002**: Bir üründe aynı anda en fazla bir ETKİN indirim; yeni etkin kampanya eskisini ezer (son-gelen-kazanır).
- **FR-003**: Kampanya iptal edilebilmeli (pencere bitmeden etkinliği düşer).
- **FR-004**: Ürünün etkin oranı her değiştiğinde (başlama/bitme/iptal/ezilme) ürün kimliği + güncel oranla (yokluk dahil) DUYURULMALI; duyuru YÜRÜRLÜK anında yayılır (tanım anında değil) — zaman sınırları iç süreçle taranır.
- **FR-005**: Catalog duyuruyu tüketip üründeki oran kopyasını güncellemeli/temizlemeli; idempotent; anomalide DLQ deseni (yutma yok).
- **FR-006**: İndirimli fiyat HİÇBİR yerde saklanmaz — liste fiyatı × (1−oran) okuyanda hesaplanır; Catalog liste fiyatı değişmeden kalır (indirim fiyatı EZMEZ).
- **FR-007**: Dış yüzey (admin REST) bu dilimde AÇILMAZ — davranışlar domain'de durur.

### Key Entities

- **Kampanya (Campaign)**: hedef ürün + oran + zaman penceresi + iptal durumu; Discount BC'nin kök aggregate'i, kendi DB'sinde.
- **Üründeki oran kopyası**: Catalog Product üzerinde opsiyonel oran alanı — duyuruyla beslenen türetilmiş veri.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kampanya davranışları (guard'lar, etkinlik penceresi, iptal, ezme kararı) domain testleriyle test-first yeşil.
- **SC-002**: Catalog oran kopyası davranışları (atama/temizleme/aralık guard'ı) testlerle yeşil; IsSalable ETKİLENMEZ (testle doğrulanır).
- **SC-003**: Tüm çözüm build + testler yeşil; yeni BC .slnx/AppHost kayıtları tam.
- **SC-004**: Canlı (Aspire) doğrulama: kampanya etkinleşir → duyuru → Catalog kopyası doldu; pencere bitince kopya temizlendi.

## Assumptions

- Checkout'un indirimli tutarla bağlanması (sipariş tutarı hesabı) SONRAKİ dilim — bugün checkout liste fiyatı kullanır.
- Yürürlük taraması dakika-hassasiyetli periyodik iç süreçtir (Process/ deseni); kampanya hacmi küçük.
- Oran üst sınırı %90 (tam bedava/negatif fiyat kazalarına guard); kullanıcı farklı isterse spec güncellenir.
- Kampanya geçmişi silinmez (iptal/bitiş durum değişimidir) — raporlama ileride.