# Feature Specification: Import Yan-Etkileri (Stok Tohumlama + İndirim İthali)

**Feature Branch**: `010-import-side-effects`

**Created**: 2026-11-16

**Status**: Draft

**Input**: Catalog Excel import'u sırasında eş zamanlı Stock + Discount BC'lerine broker event'leriyle kayıt aksın; tek re-import ürün+etiket+kapak+indirim+stok hepsini doldursun ve kitaplar satılabilir olsun.

> **Kademe (conventions):** Tam akış — 3 BC (Catalog yayıncı, Stock + Discount tüketici), yeni integration event, Campaign aggregate evrimi. Domain-TDD saf domain davranışına uygulanır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import edilen kitaplar satılabilir olur (Priority: P1) 🎯 MVP

Admin katalog Excel'ini import eder. Her yeni ürün doğduğunda Stock BC ona otomatik bir stok kaydı açar (demo/eğitim verisi için rastgele 30-50 adet). Stok adedi Catalog'daki ürün kopyasına yansır. Böylece fiyatı ve yazarı olan kitaplar artık **satılabilir** (IsSalable = fiyat>0 ∧ stok>0 ∧ ≥1 yazar) — mağaza işler hale gelir.

**Why this priority**: Stok olmadan hiçbir kitap satın alınamaz (IsSalable stok>0 ister). Mağazanın çalışır olması için en kritik yan-etki.

**Independent Test**: Boş sistem + Excel import → her ürün için bir stok kaydı (30-50) oluştu; Catalog ürün kopyasında stok adedi dolu; fiyatlı+yazarlı kitaplar IsSalable=true.

**Acceptance Scenarios**:

1. **Given** fiyatı ve yazarı olan bir kitap import edilir, **When** ürün doğar, **Then** stok kaydı 30-50 arası bir adetle açılır ve kitap satılabilir olur.
2. **Given** fiyatsız bir kitap import edilir, **When** ürün doğar, **Then** stok yine açılır ama kitap satılabilir DEĞİL (fiyat ayağı eksik).
3. **Given** aynı kitap (ISBN) ikinci kez import akışından geçer, **When** ürün zaten var (INSERT-ONLY atlar), **Then** yeni stok kaydı açılmaz (ürün başına tek kayıt).

---

### User Story 2 - İndirimli kitaplar indirimli görünür (Priority: P2)

Admin Excel'inde indirim oranı olan kitaplar var (Kitapyurdu'nun indirimi). Import, bu oranı "ithal" eder: indirimli kitap için Discount BC'de açık-uçlu bir kampanya oluşur ve indirim oranı Catalog'daki ürüne yansır. Böylece indirimli fiyat hesaplanabilir (liste fiyatı ezilmez).

**Why this priority**: Gerçek fiyatlandırma deneyimi için değerli ama satılabilirliğin ön koşulu değil; stoktan sonra gelir.

**Independent Test**: İndirim oranı olan bir kitap import → Catalog ürününde indirim oranı dolu; indirimi olmayan kitapta oran boş.

**Acceptance Scenarios**:

1. **Given** indirim oranı %15 olan bir kitap import edilir, **When** ürün yazılır, **Then** o ürün için açık-uçlu kampanya oluşur ve Catalog ürün kopyasındaki indirim oranı 0.15 olur.
2. **Given** indirim oranı 0/boş olan bir kitap, **When** import edilir, **Then** kampanya oluşmaz, ürünün indirim oranı boş kalır.
3. **Given** indirim oranı geçerli aralık dışında (ör. %95), **When** import edilir, **Then** o oran atlanır (kampanya oluşmaz) — iş durmaz.

---

### Edge Cases

- Excel "İndirim %" hücresi metin/bozuk → oran okunamaz, indirim yok (iş durmaz).
- Stok kaydı açılırken geçici hata → retry/DLQ (ürün Catalog'da kalır, stok sonra onarılır); stoksuz ürün satılamaz ama katalogda görünür.
- İndirim event'i ürün yazılmadan ulaşırsa olmaz — event ürün yazıldıktan SONRA yayılır (ProductId garantili).
- Mevcut ürün (INSERT-ONLY atlanan) → ne stok ne indirim event'i yayılır (yalnız yeni ürün).

## Requirements *(mandatory)*

### Functional Requirements

**Stok (US1)**

- **FR-001**: Yeni bir ürün doğduğunda, Stock BC o ürün için bir stok kaydı açmalı (ürün başına tek kayıt).
- **FR-002**: Açılan stok adedi 30-50 arası (dahil) rastgele bir değer olmalı — demo/eğitim verisi; Excel'den okunmamalı (kaynak güvenilmez).
- **FR-003**: Açılan stok adedi Catalog'daki ürün stok kopyasına yansımalı (mevcut adet-kopyası mekanizması).
- **FR-004**: Aynı ürün için stok kaydı tekrar açılmamalı (idempotent; ürün başına tek kayıt).

**İndirim (US2)**

- **FR-005**: Import, Excel "İndirim %" değerini okumalı ve orana (kesir) çevirmeli (ör. %15 → 0.15).
- **FR-006**: İndirim oranı > 0 olan yeni ürün için, ürün yazıldıktan sonra indirim bilgisi Discount BC'ye iletilmeli (ürün kimliğiyle).
- **FR-007**: Discount BC bu bilgiden açık-uçlu (bitişi olmayan) bir kampanya oluşturmalı; indirim oranı geçerli aralıkta (%1-%90) değilse kampanya oluşturulmamalı.
- **FR-008**: Oluşan kampanyanın etkin oranı Catalog'daki ürün indirim kopyasına yansımalı (liste fiyatı ezilmez; indirimli fiyat okuma anında hesaplanır).
- **FR-009**: İndirim oranı 0/boş olan üründe kampanya oluşmamalı, indirim kopyası boş kalmalı.

**Ortak**

- **FR-010**: Yan-etkiler yalnız YENİ ürün için tetiklenmeli; INSERT-ONLY ile atlanan mevcut ürün için stok/indirim event'i yayılmamalı.
- **FR-011**: Yan-etkiler Catalog ile Stock/Discount arasında yalnız broker event'leriyle akmalı; senkron çağrı eklenmemeli.
- **FR-012**: Geçici hatada kayıp olmamalı (retry → ölü-mesaj kuyruğu); bir yan-etkinin hatası import'u veya diğer yan-etkiyi durdurmamalı.

### Key Entities *(include if feature involves data)*

- **ProductDiscountImported (yeni entegrasyon event'i)**: Catalog'dan Discount'a; hangi ürün (kimlik) + indirim oranı. İndirimin kaynağının import olduğunu işaretler.
- **Campaign (mevcut, evrilir)**: Tek ürün + oran + zaman penceresi; bu dilimde **açık-uçlu** (bitişi olmayan) kampanya desteklenir — import kampanyası süresiz aktif.
- **StockItem (mevcut)**: Ürün başına tek stok kaydı + adet; import yan-etkisiyle 30-50 arası açılır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tam re-import sonrası, fiyatı ve en az bir yazarı olan kitapların en az %99'u satılabilir (IsSalable=true) olur.
- **SC-002**: Her yeni ürün için tam olarak bir stok kaydı vardır (ürün sayısı = stok kaydı sayısı; mükerrer yok).
- **SC-003**: Excel'de indirim oranı geçerli olan kitapların indirim oranı, import sonrası Catalog ürün kopyasında birebir (kesir olarak) görünür.
- **SC-004**: İndirimi olmayan kitaplarda indirim kopyası boştur (yanlış indirim sızmaz).
- **SC-005**: İkinci kez aynı dosya import edilince ne yeni stok kaydı ne yeni kampanya oluşur (idempotent; INSERT-ONLY).

## Assumptions

- **Taze/re-import**: Katalog boş; import yeni ürün doğurur. Yan-etkiler yalnız yeni ürün yolundan akar (mevcut ürüne backfill yok).
- **İndirim ithal**: Excel "İndirim %" Kitapyurdu'nun indirimidir; "ithal" kabul edilir (bizim fiyat kararımız değil). İndirim bitişi yok → açık-uçlu kampanya.
- **Stok kurgusal**: Excel stok verisi kullanılamaz (çoğu boş + "10+" metni); stok bizim kurgumuz, rastgele 30-50. Gerçek mal-kabul yüzeyi (elle adet) sonraki dilim.
- **Mevcut kablolar**: Catalog'un stok adet-kopyası (StockChanged tüketici) + indirim oran-kopyası (ProductDiscountChanged tüketici) ZATEN kurulu; bu dilim yayıncı tarafını tamamlar.
- **Genel indirim yürürlük süreci kapsam dışı**: Zaman-pencereli kampanyaların otomatik başlat/bitir süreci (CampaignWindowProcess) ertelenmiş kalır; bu dilim yalnız import kampanyasının (açık-uçlu, hemen aktif) oranını yayınlar.
- **Kapak kapsam dışı**: Ürün doğunca kapak akışı (File) zaten kurulu; bu dilim ona dokunmaz.
- **Checkout/admin yüzeyi kapsam dışı**: Satış düşümü ve admin stok/indirim ekranları sonraki dilimler.
