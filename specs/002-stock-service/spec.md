# Feature Specification: Stock.Api — Envanter BC'sinin Dönüşü

**Feature Branch**: `002-stock-service`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "Stock.Api servisini ekleyelim (Excel verisi sonra)."

**Kademe**: Tam — yeni BC (yeni aggregate + DB + integration event kontratı).

## Bağlam

Fiziki kitaba dönüş kararıyla stok kavramı geri geliyor (dijital pivotta sökülmüştü). Envanter
kurgusaldır (gerçek depo yok). 001'de satışa-uygunluk kriteri "fiyat>0 ∧ stok>0 ∧ ≥1 yazar" olarak
tanımlandı, stok ayağı boş geçiyordu — bu dilim o ayağı gerçek yapar. Karar (brainstorm'da kilitli):
adedin TEK yazarı Stock; Catalog, her adet değişiminde yayınlanan event'le kendi ürün satırındaki
adet KOPYASINI günceller (kompleks künye+stok sorguları tek yerde cevaplansın diye).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ürünün stok kaydı yaşar (Priority: P1)

Her ürünün stok kaydı vardır: adet girilir (mutlak değer), satışta düşer. Adet asla negatife inmez;
yetersiz stoğa düşüm reddedilir.

**Why this priority**: Envanterin çekirdeği; kriterin "stok>0" bacağı bu kayda dayanır.

**Independent Test**: Saf domain testleri — oluşturma, mutlak atama, düşüm, guard'lar.

**Acceptance Scenarios**:

1. **Given** ürün için stok kaydı yok, **When** 50 adetle kayıt açılır, **Then** eldeki adet 50'dir.
2. **Given** eldeki 50, **When** admin mutlak 20 atar, **Then** eldeki 20 olur (negatif atama reddedilir).
3. **Given** eldeki 3, **When** 2 adet düşülür, **Then** eldeki 1; **When** 5 adet düşülmek istenir, **Then** reddedilir, eldeki değişmez.
4. **Given** düşüm/atama, **When** miktar ≤0 (düşüm) ya da <0 (atama), **Then** reddedilir.

---

### User Story 2 - Adet değişimi katalogdaki kopyayı besler (Priority: P2)

Stok adedi her değiştiğinde değişiklik duyurulur; Catalog bu duyuruyu tüketip ürünündeki adet
kopyasını günceller. Böylece 001'deki satışa-uygunluk kriterinin stok bacağı AKTİFLEŞİR:
adedi 0 olan (ya da hiç stok kaydı olmayan) ürün satışa uygun sayılmaz.

**Why this priority**: Vitrin/sorgu tarafının stok gerçeğini tek yerden (Catalog satırı) okuması;
satışa-uygunluğun tamamlanması.

**Independent Test**: Catalog domain testi — adet kopyası 0 iken satışa-uygunluk kapalı, >0 olunca
açık; tüketici kablosu canlı (Aspire) doğrulanır.

**Acceptance Scenarios**:

1. **Given** fiyatlı+yazarlı ama adet kopyası 0 olan ürün, **When** satışa-uygunluk sorgulanır, **Then** KAPALI.
2. **Given** aynı ürün, **When** stok duyurusu adet 50 getirir, **Then** kopya 50 olur ve satışa-uygunluk AÇIK.
3. **Given** adet 1→0 düşümü, **When** duyuru işlenir, **Then** ürün satışa KAPANIR.

---

### Edge Cases

- Catalog'da olmayan ürün için stok duyurusu → sessizce atlanır (loglanır); Stock kaydı ürün yaşam döngüsünden bağımsız yaşayabilir.
- Aynı duyurunun iki kez işlenmesi (at-least-once) → kopya mutlak değer taşıdığından idempotent (son değer kazanır).
- Negatif adetli duyuru domain'den çıkamaz (guard'lar üretimde engeller).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Her ürün için tek stok kaydı tutulabilmeli (ürün başına teklik); eldeki adet ≥0 invariant'ı her davranışta korunmalı.
- **FR-002**: Adet mutlak değerle atanabilmeli (admin girişi; ≥0) ve satış düşümüyle azaltılabilmeli (pozitif miktar, yetersizse red).
- **FR-003**: Eldeki adet her değiştiğinde (atama/düşüm) değişiklik ürün kimliği + yeni adetle DUYURULMALI (yayıncı Stock).
- **FR-004**: Catalog duyuruyu tüketip üründeki adet kopyasını güncellemeli; işlem idempotent (mutlak değer) olmalı.
- **FR-005**: 001'deki satışa-uygunluk tanımının stok bacağı aktifleşmeli: adet kopyası >0 değilse ürün satışa uygun değildir. Tanım tek yerde kalır.
- **FR-006**: Dış yüzey (REST/admin ekranı) bu dilimde AÇILMAZ — davranışlar domain'de durur; stok girişi yüzeyi sonraki dilim.

### Key Entities

- **Stok Kaydı (StockItem)**: ürün kimliği (teklik anahtarı) + eldeki adet; Stock BC'nin kök aggregate'i, kendi DB'sinde.
- **Üründeki adet kopyası**: Catalog Product üzerinde düz alan — Stock duyurusuyla beslenen türetilmiş veri, yazarı yalnız tüketici kablosu.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Stok davranışlarının tümü (oluşturma, atama, düşüm, guard'lar) domain testleriyle test-first yeşil.
- **SC-002**: Satışa-uygunluk doğruluk tablosu stok bacağıyla genişler ve tüm kombinasyonlar beklenen sonucu verir; mevcut testler güncellenmiş beklentiyle yeşil kalır.
- **SC-003**: Aspire'da uçtan uca canlı doğrulama: stok ataması → duyuru → Catalog kopyası güncel → satışa-uygunluk değişti.
- **SC-004**: Tüm çözüm build + testler yeşil; yeni BC `.slnx`/AppHost kayıtları tam (servis Aspire'dan ayağa kalkar).

## Assumptions

- Envanter kurgusal; gerçek depo/rezervasyon/lokasyon kavramı yok. Rezervasyon modeli (012/017) GERİ GELMİYOR — 056 kararı sürer (stok gerçeği sipariş anında düşülür).
- Checkout'un stok düşümüne bağlanması (sipariş akışı entegrasyonu) SONRAKİ dilim — bu dilim yalnız BC + duyuru + Catalog kopyası.
- Stok kaydı doğumu da sonraki dilimlerin işi (Excel import / admin yüzeyi); bu dilimde üretim yolu yok, domain hazır olur.
- Duyuru her adet değişiminde yayınlanır (eşik-bazlı değil) — ölçek küçük, karar brainstorm'da kilitli.