# Feature Specification: Fiyat değişmezliği (sadeleştirme)

**Feature Branch**: `097-price-immutability`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "Fiyat değişmezliği (097): ürün fiyatı doğumda sabitlenir, sonradan
DEĞİŞMEZ. Catalog'dan Product.SetPrice davranışı + 058 append-only fiyat geçmişi (ProductPriceChange
dokümanı + seed'deki ilk-fiyat satırı) sökülür; ProductChangedEvent.OldPrice alanı kontrattan düşer.
Library PriceAlarm UYUR (model/aggregate DURUR, sökülmez) — OldPrice tetiği öldüğü için tetik yolu
pasifleşir; UserPurchase etkilenmez. Fiyat>0 doğum invariant'ı ve checkout'un liste fiyatı okuması
aynen kalır. Sadeleştirme serisinin ilk adımı (kullanıcı kararı 2026-10-31)."

## Bağlam

Kullanıcı projeyi sadeleştiriyor; fiyat-değişimi ekseninin tamamı (değiştirme davranışı, 058 geçmişi,
eski-fiyat/yeni-fiyat kavramı, alarm tetiği) kullanım dışı karmaşıklık. Karar: **fiyat doğumda
sabitlenir** — değişim kavramı modelden ve kontrattan çıkar. PriceAlarm aggregate'i SÖKÜLMEZ,
tetiksiz UYUR (ileride fiyat değişimi geri gelirse aynı modelle uyanır).
**Artefakt kademesi: Küçük** (yeni aggregate/tablo/event YOK — yalnız söküm; spec+tasks).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fiyat doğumda sabitlenir (Priority: P1)

Ürün, fiyatıyla doğar (fiyat>0 invariant'ı sürer) ve o fiyat ürünün ömrü boyunca değişmez. Sistemde
fiyat değiştirme davranışı, fiyat geçmişi kaydı ve "eski fiyat/yeni fiyat" kavramı KALMAZ.

**Why this priority**: Feature'ın kendisi — sadeleştirmenin tanımı.

**Independent Test**: Kod tabanında fiyat-değiştirme davranışı ve fiyat-geçmişi kaydı aranınca
bulunamaz; temiz açılışta seed 11.438 ürünü fiyatlarıyla doğurur, fiyat geçmişi tablosu hiç oluşmaz.

**Acceptance Scenarios**:

1. **Given** ürün doğarken, **When** fiyat>0 verilir, **Then** ürün o fiyatla doğar (mevcut davranış).
2. **Given** doğmuş ürün, **When** domain yüzeyi taranır, **Then** fiyatı değiştirecek davranış YOKTUR.
3. **Given** temiz açılış + seed, **When** dolum biter, **Then** fiyat geçmişi kaydı/tablosu üretilmemiştir.
4. **Given** ürün değişim event'i, **When** kontrata bakılır, **Then** eski-fiyat alanı yoktur; fiyat tek alandır.

---

### User Story 2 - Alarm tetiği pasifleşir, model uyur (Priority: P2)

Fiyat değişimi kavramı öldüğü için Library'nin fiyat-alarmı tetiği (eski fiyat ≠ yeni fiyat dalı)
koddan çıkar; PriceAlarm aggregate'i, kayıtları ve `PriceAlarmTriggered` kontratı DURUR (uyur) —
yayıncısı kalmaz.

**Why this priority**: P1'in zorunlu sonucu; tek başına değer taşımaz ama tutarlılık gerektirir.

**Independent Test**: Ürün değişim event'i tüketildiğinde Library alarm tetikleme yoluna hiç girmez;
UserPurchase akışı (OrderCompleted) etkilenmez.

**Acceptance Scenarios**:

1. **Given** ürün değişim event'i (ör. kapak dolumu), **When** Library tüketir, **Then** alarm
   tetiklenmez, hata/DLQ oluşmaz.
2. **Given** PriceAlarm modeli/kayıtları, **When** feature biter, **Then** model ve veri yerinde durur
   (söküm YOK), yalnız tetik yolu kalkmıştır.

---

### Edge Cases

- Seed yeniden koşarsa (ikinci açılış): Gtin-varsa-atla sürer; fiyat geçmişi yazımı artık hiçbir
  dalda yok — davranış farkı yalnız "geçmiş satırı üretilmez".
- Eski DB'de kalan fiyat-geçmişi tabloları: dev full-reset'le gider (Marten tablo düşürmez — 095
  emsali not düşülür).
- Embedding (096) etkilenmez: fiyat compose metnine girmiyor.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Ürün fiyatı yalnız doğumda atanır; doğum sonrası fiyatı değiştiren hiçbir domain
  davranışı/yüzeyi kalmaz (fiyat>0 doğum invariant'ı aynen sürer).
- **FR-002**: 058 append-only fiyat geçmişi sökülür: geçmiş kaydı modeli, şema kaydı ve seed'deki
  ilk-fiyat satırı yazımı kalkar.
- **FR-003**: Ürün değişim kontratından eski-fiyat alanı düşer; tüm yayıncı/tüketiciler aynı PR'da
  hizalanır (dışarıda bağımsız tüketici yok — kontrat repoda kapalı).
- **FR-004**: Library'nin alarm tetikleme yolu (eski-fiyat karşılaştırma dalı) kaldırılır;
  PriceAlarm aggregate'i, mevcut kayıtları ve PriceAlarmTriggered kontratı DURUR — silinmez,
  yayıncısız uyur.
- **FR-005**: UserPurchase (OrderCompleted tüketimi) ve checkout'un liste fiyatı okuması davranış
  değiştirmez.
- **FR-006**: FLOW.md'ler gerçeğe hizalanır: Catalog fiyat-geçmişi adımı düşer, Library alarm süreci
  "uykuda (tetiksiz)" olarak işaretlenir (İlke VII — aynı PR).

### Key Entities

- **Product (Catalog)**: fiyat artık doğum-anı sabiti; davranış yüzeyinden fiyat mutasyonu çıkar.
- **ProductPriceChange (Catalog)**: ÖLÜR — model + şema kaydı + yazım yolları.
- **ProductChangedEvent (Shared kontrat)**: eski-fiyat alanı düşer; kalan alanlar aynen.
- **PriceAlarm / PriceAlarmTriggered (Library/Shared)**: UYUR — dokunulmaz, yayıncısı kalmaz.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kod tabanında fiyat-değiştirme davranışı ve fiyat-geçmişi modeli 0 referans (build
  temiz, ölü kod yok).
- **SC-002**: Tüm testler + iki guard yeşil; fiyat-geçmişi testleri sökümle birlikte düşer.
- **SC-003**: Temiz açılışta seed 11.438 ürünü doğurur; fiyat geçmişi tablosu oluşmaz; kapak/embedding
  akışları (ProductChanged tüketicileri) hatasız sürer, DLQ boş.
- **SC-004**: Library alarm tetiği koddan çıkmış; PriceAlarm modeli ve varsa kayıtları yerinde.

## Assumptions

- Kontrat alanı düşürme güvenli: tüm yayıncı/tüketici bu repoda, aynı PR'da hizalanır (dış abone yok
  — MCP/Storefront söküldü).
- "Uyku" tanımı: kod + model durur, çağıran/yayıncı kalmaz; geri getirme ayrı bilinçli karar.
- Eski DB tabloları (fiyat geçmişi) dev full-reset'e bırakılır; migration yazılmaz (091/095 emsali).
- Canlı doğrulama mevcut Aspire koşusuyla yapılır (temiz-DB reset kullanıcı onayına tabi).
