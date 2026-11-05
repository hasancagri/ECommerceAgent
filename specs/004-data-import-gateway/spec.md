# Feature Specification: Temel Veri + Gateway (Excel Import, Kapak Zinciri, Tek Origin)

**Feature Branch**: `004-data-import-gateway`

**Created**: 2026-11-04

**Status**: Draft

**Input**: User description: "Dilim 1 — Temel veri + Gateway: Admin Excel import (insert-only, ISBN skip) + kapak zinciri dönüşü (CoverIngested→Catalog) + YARP gateway (tek origin, kenar yetki). Kaynak: vault todos/11, 13, 12."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Excel ile kataloğu doldurur (Priority: P1)

Admin, Kitapyurdu kazıması Excel dosyasını (≈23.600 kitap, 21 alan) yükler; sistem arka planda satır satır işler, yeni kitapları kataloğa ekler ve sonunda rapor sunar. Bugün kataloğa ürün giren HİÇBİR yol yok — bu akış mağazanın içeriğini doğurur.

**Why this priority**: İçeriksiz katalogda hiçbir sonraki dilim (keşif, detay, sepet) çalışamaz; tek ürün giriş yolu bu.

**Independent Test**: Küçük bir deneme Excel'i (örn. 20 satır: yeni + mükerrer + hatalı satır karışık) yüklenir; eklenen/atlanan/hatalı sayıları ve kataloğa düşen kayıtlar doğrulanır.

**Acceptance Scenarios**:

1. **Given** boş katalog ve geçerli Excel, **When** admin dosyayı yükleyip import'u başlatır, **Then** tüm geçerli satırlar ürün olarak doğar; yazar/yayınevi/kategori yoksa oluşturulur, varsa yeniden kullanılır.
2. **Given** katalogda mevcut bir ISBN, **When** aynı ISBN'li satır import edilir, **Then** mevcut kayda DOKUNULMAZ; satır "atlandı (ISBN mevcut)" sayılır. (INSERT-ONLY)
3. **Given** fiyatı boş bir satır, **When** import edilir, **Then** ürün fiyatsız doğar — katalogda görünür ama satışa uygun değildir (satışa-uygunluk kuralı 001 aynen).
4. **Given** zorunlu alanı eksik/bozuk bir satır (örn. ISBN'siz), **When** import edilir, **Then** satır hata listesine sebebiyle düşer; iş durmaz, kalan satırlar işlenir.
5. **Given** süren bir import, **When** admin ilerlemeye bakar, **Then** işlenen/eklenen/atlanan/hatalı sayaçlarını canlı görür; iş bitince kalıcı sonuç raporu kalır.
6. **Given** tamamlanmış bir import, **When** aynı dosya tekrar yüklenir, **Then** tüm satırlar "atlandı" ile biter — tekrar güvenlidir (idempotent).

---

### User Story 2 - Tek kapıdan güvenli erişim (Gateway) (Priority: P2)

Gelecek React uygulaması (ve bugünkü admin istemcisi) tüm API'lere TEK adresten ulaşır; istekler rota bazında yetki süzgecinden geçer (admin rotaları admin ister, kişisel rotalar login ister, katalog/keşif anonim). Servislerin kendi yetki kontrolü ikinci kat olarak sürer.

**Why this priority**: İlk REST yüzeyi bu dilimde doğuyor (import ucu); kapı ve kenar-yetki deseni ilk yüzeyle birlikte kurulmazsa her dilimde origin/yetki düzeni yeniden ele alınır.

**Independent Test**: Gateway üzerinden (a) anonim katalog isteği 200, (b) token'sız admin isteği 401/403, (c) admin token'lı import isteği 200 alır; servise doğrudan gidilse bile servis kendi yetkisini uygular.

**Acceptance Scenarios**:

1. **Given** çalışan sistem, **When** istemci gateway adresinden bir servise rotalanan istek atar, **Then** istek doğru servise ulaşır ve yanıt döner; istemci servislerin gerçek adreslerini bilmez.
2. **Given** admin rotası, **When** istek geçersiz/eksik kimlikle gelir, **Then** kenarda reddedilir (servise ulaşmaz).
3. **Given** admin rotası ve geçerli admin kimliği, **When** istek gelir, **Then** gateway geçirir VE hedef servis kendi scope kontrolünü ayrıca uygular (çift kat).
4. **Given** gateway arkasındaki servisler, **When** istek proxy üzerinden gelir, **Then** şema/host bilgisi doğru taşınır (yönlendirme, kimlik ve URL üretimi bozulmaz).

---

### User Story 3 - Kitaplar kapaklarıyla görünür (Priority: P3)

Import edilen her kitabın kapağı, Excel'deki görsel adresinden obje deposuna alınır, kayıt defterine işlenir ve ürün künyesine kapak adresi düşer — kitaplar vitrine kapaklı çıkar.

**Why this priority**: Kapaksız katalog keşif deneyimini öldürür; ama içerik (US1) ve kapı (US2) olmadan tek başına değer üretmez.

**Independent Test**: Deneme import'u sonrası örnek ürünlerin kapak adresi dolu olur ve adres tarayıcıda gerçek görseli döner; obje deposunda zaten var olan kapak yeniden indirilmez.

**Acceptance Scenarios**:

1. **Given** görsel adresi olan bir satır, **When** ürün import'la doğar, **Then** kapak kaynaktan alınıp depoya yazılır, kayıt defterine işlenir ve ürünün kapak adresi dolar.
2. **Given** obje deposunda o ISBN için kapak ZATEN var, **When** ürün import edilir, **Then** indirme atlanır; mevcut kapak kayıttan çözülür ve ürüne bağlanır (idempotent).
3. **Given** görsel adresi erişilemeyen bir satır, **When** import edilir, **Then** ürün KAPAKSIZ doğar (iş durmaz); kapak eksikliği raporda görünür, sonradan tamamlanabilir.

---

### Edge Cases

- Excel İÇİNDE mükerrer ISBN (iki satır aynı kitap): ilki eklenir, ikincisi "atlandı" sayılır.
- Dosya hiç Excel değil / beklenen sütunlar yok: iş başlamadan anlaşılır hata; hiçbir satır işlenmez.
- Import ortasında süreç/uygulama kesilirse: tamamlanan satırlar kalır; aynı dosya yeniden yüklenince kalanlar eklenir (ISBN skip sayesinde güvenli).
- Aynı anda ikinci import başlatma girişimi: engellenir ya da kuyruğa alınır — iki iş aynı anda yazamaz.
- Çok büyük dosya (23.600 satır): istek zaman aşımına düşmez — işleme arka planda sürer, ekran ilerlemeden izler.
- Kategori satırda leaf+üst ikilisi gelir; ikisi de yoksa ikisi de doğar (kapalı 2 seviyeli ağaç kuralı korunur).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, admin'in Excel dosyasını yüklemesine ve import işini başlatmasına izin VERMELİ; iş arka planda koşmalı, yükleme yanıtı işin kimliğini dönmeli.
- **FR-002**: Import INSERT-ONLY olmalı: katalogda aynı ISBN varsa satır atlanır, mevcut kayda hiçbir alanda dokunulmaz.
- **FR-003**: Satırdaki yazar(lar), yayınevi ve kategori mevcutsa yeniden kullanılmalı, yoksa oluşturulmalı (get-or-create); aynı import içinde tekrar eden adlar tek kayda gitmeli.
- **FR-004**: Fiyatı olmayan satır ürün olarak doğmalı; satışa-uygunluk mevcut kural setiyle (fiyat>0 ∧ stok>0 ∧ ≥1 yazar) belirlenmeli — import bu kuralı değiştirmez.
- **FR-005**: Her import işi sonuç raporu üretmeli: işlenen / eklenen / atlanan (ISBN mevcut) / hatalı (satır no + sebep); rapor iş bittikten sonra da erişilebilir kalmalı.
- **FR-006**: Süren işin ilerlemesi sorgulanabilir olmalı (sayaçlar); iş durumu: kuyrukta / sürüyor / bitti / başarısız.
- **FR-007**: Satır hataları işi durdurmamalı; yalnız ilgili satır hata listesine düşmeli.
- **FR-008**: Aynı anda tek import işi koşmalı; ikincisi reddedilmeli ya da sıraya alınmalı.
- **FR-009**: Import edilen ürün ANINDA yayında olmalı (taslak/onay katmanı yok).
- **FR-010**: Ürün doğarken kapak zinciri tetiklenmeli: görsel adresi kaynaktan alınır → obje deposuna yazılır → kayıt defterine işlenir → ürünün kapak adresi dolar. Depoda mevcutsa indirme atlanmalı (idempotent).
- **FR-011**: Kapak alınamazsa ürün kapaksız doğmalı; eksik kapaklar raporlanmalı ve sonradan tamamlanabilmeli.
- **FR-012**: Tüm dış istemci trafiği tek kapıdan (gateway) geçmeli; kapı, rotayı doğru servise iletmeli ve servis adreslerini istemciden gizlemeli.
- **FR-013**: Kapıda rota-düzeyi yetki uygulanmalı: admin rotaları admin kimliği, kişisel rotalar login, katalog/keşif rotaları anonim; reddedilen istek hedef servise ULAŞMAMALI.
- **FR-014**: Kapı yetkisi geçse bile hedef servis kendi scope kontrolünü uygulamalı (savunma derinliği — mevcut `[RequiredScope]` düzeni bozulmaz).
- **FR-015**: Kapı arkasındaki servislerde şema/host bilgisi doğru taşınmalı; kimlik doğrulama, yönlendirme ve URL üretimi proxy arkasında bozulmamalı.
- **FR-016**: Import ucu admin yetkisi İSTEMELİ. Dilim 1'de admin kimliği = **geçici sabit API anahtarı** (config/secret'tan; istekte başlıkla taşınır, kapı admin rotasında bu anahtarı doğrular). Dilim 2'de yerel kimlik JWT'sine geçilir, anahtar sökülür. Anahtar değeri hiçbir ekranda/log'da görünmez (İlke VIII).

### Key Entities

- **Import İşi**: bir Excel yüklemesinin yaşam döngüsü — durum (kuyrukta/sürüyor/bitti/başarısız), sayaçlar (işlenen/eklenen/atlanan/hatalı), hata listesi (satır + sebep), başlangıç/bitiş zamanı.
- **Ürün (mevcut)**: sade künye — import yeni alan EKLEMEZ (02 kararı: kağıt cinsi/boyut/ekol yok); 21 Excel alanından yalnız mevcut künye alanları eşlenir, gerisi yok sayılır.
- **Yazar / Yayınevi / Kategori (mevcut)**: get-or-create hedefleri; kategori kapalı 2 seviyeli ağaç kuralına uyar.
- **Kapak Kaydı (mevcut — File)**: ISBN anahtarıyla tekil kapak + depo konumları; import zinciri bu kayıt defterini kullanır.
- **Rota Tablosu (gateway)**: dış yol → iç servis eşlemesi + rota başına yetki sınıfı (anonim / login / admin).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, 23.600 satırlık gerçek dosyayı tek oturumda yükleyip raporunu alabilir; iş tamamlanır, uygulama kesintiye uğramaz.
- **SC-002**: Aynı dosyanın ikinci import'u 0 yeni kayıt üretir (tamamı "atlandı") — katalog bozulmaz.
- **SC-003**: Import sonrası rastgele örneklenen ürünlerin %95+'ında kapak görseli ekranda açılır; kapağı eksik olanlar raporda listelidir.
- **SC-004**: Dış istemci tüm işlevlere tek adres üzerinden ulaşır; token'sız admin isteği kenarda reddedilir, hedef servise hiç ulaşmaz.
- **SC-005**: Hatalı satırlar işi durdurmaz: içinde bozuk satırlar olan dosyada geçerli satırların tamamı eklenir, her hata sebepli raporlanır.

## Assumptions

- Excel şeması `Kitapyurdu_Katalog_v2.xlsx` ile sabittir; 21 alan → sade künye eşlemesi plan aşamasında dosya başlıklarıyla netleşir (künyeye girmeyen alanlar yok sayılır — 02 kararı).
- Kapak görsel adresleri erişilebilir dış adreslerdir; obje deposunda (R2) geçmişten kalan kapaklar ve kayıt defteri geçerlidir — mevcutlar yeniden indirilmez.
- Import anında yayına açıktır (taslak yok) — eski backlog kararı geçerli.
- Stok bu dilimin dışıdır: import stok yazmaz; ürünler stok 0 doğar (satış-dışı), stok ayrı akışla atanır.
- Gateway iş mantığı/birleştirme taşımaz; BFF/sohbet katmanı sonraki dilimlerin konusudur.
- Tek admin kullanıcı varsayımı (site sahibi); çoklu admin/rol yönetimi Dilim 2 kimlik işinin konusudur.