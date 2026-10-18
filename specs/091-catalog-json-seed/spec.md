# Feature Specification: Katalog JSON Seed Dönüşü + Excel Import ve Yayın Makinesi Sökümü

**Feature Branch**: `091-catalog-json-seed`

**Created**: 2026-10-18

**Status**: Draft

**Input**: User description: "Excel katalog import (083) yolunu sök, yerine commit'li JSON seed data ile katalog doluşu getir (051 deseninin dönüşü). Tedarikçi-tarzı staging/taslak import akışı kalksın. Publish etme özelliği de kalksın — vitrinden gizle/geri-aç dahil; tüm ürünlerde fiyat>0 olsun. InitialStock seed JSON'dan satır bazında. Kapak zinciri aynı seed JSON'dan beslensin."

**Kademe**: Tam — dış MCP tool kontratı kırılıyor (import/publish tool'ları silinir), BC-arası etki var (Catalog + File + AppHost), aggregate durum makinesi değişiyor.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Katalog açılışta seed'den dolar (Priority: P1)

Geliştirici sistemi Aspire'dan kaldırır; Catalog, repo'ya commit'li `books.json`'dan ürünleri (yazar/yayınevi/kategori dahil) idempotent kurar. Ürünler doğduğu an canlıdır; Stock bağı ve vitrin akışı kendiliğinden işler. Excel yükleme, token-link ekranı, staging yoktur.

**Why this priority**: Uygulamanın kalbi metin/chat ürün keşfi; keşfin çalışması için katalogun sıfır-tören dolması gerekir. Diğer her şey bunun üstüne.

**Independent Test**: Boş DB ile sistemi kaldır; Catalog loglarında seed tamamlanır, `search_products` ürün döner, Stock'ta satır başına OnHand seed değeridir.

**Acceptance Scenarios**:

1. **Given** boş catalogDb, **When** sistem açılır, **Then** seed JSON'daki her kayıt için ürün + yazar/yayınevi/kategori doğar ve ürün başına `ProductAdded` (satırın stok değeriyle) yayılır.
2. **Given** seed daha önce koşmuş (ürünler var), **When** sistem yeniden açılır, **Then** var olan ISBN'ler atlanır — çift kayıt, çift event yok.
3. **Given** seed kaydında fiyat ≤ 0, **When** seed o satırı işler, **Then** satır reddedilir (ürün doğmaz), seed kalanlarla devam eder.

---

### User Story 2 - Excel import ve yayın yüzeyi söküldü (Priority: P2)

Admin'in MCP yüzeyinde `admin_import_catalog`, `admin_publish_imported`, `admin_get_import_status`, `admin_set_published` yoktur. `admin_create_product` ile ürün doğrudan canlı doğar (fiyat>0 şartı). Taslak/yayın/gizleme kavramı kalmaz; her ürün değişimi vitrine akar.

**Why this priority**: Kullanıcının asıl rahatsızlığı bu yüzey; ama seed (US1) gelmeden söküm katalogu doldurulamaz bırakır.

**Independent Test**: MCP tool listesinde import/publish tool'ları görünmez; `admin_create_product` fiyat>0 ile canlı ürün üretir, fiyatsız istek reddedilir; `/catalog-import/{token}` rotası 404.

**Acceptance Scenarios**:

1. **Given** admin MCP bağlantısı, **When** tool listesi çekilir, **Then** import/publish/set_published tool'ları yoktur; kalan admin tool'ları çalışır.
2. **Given** fiyatı 0 olan `admin_create_product` isteği, **When** çağrılır, **Then** istek Result hatasıyla reddedilir (ürün doğmaz).
3. **Given** canlı bir ürün, **When** `admin_update_product` ile düzenlenir, **Then** değişiklik `ProductChangedEvent` ile vitrine akar — yayın-durumu kontrolü yoktur.

---

### User Story 3 - Kapaklar seed JSON'dan beslenir (Priority: P3)

File.Api açılış kapak migration'ı repo-dışı xlsx yerine AYNI commit'li seed JSON'dan (isbn+imageUrl) okur; kapaklar staging'e iner, `ProductAdded` üzerine `CoverIngested` köprüsü aynen çalışır, ürün kartında kapak görünür.

**Why this priority**: Görsel süs — katalog ve yüzey çalışmadan kapağın anlamı yok; kapak yoksa ürün placeholder'da kalır, akış bloklanmaz (mevcut FR-008 davranışı).

**Independent Test**: Temiz staging ile sistemi kaldır; migration seed JSON'u okur, kapaklar `{RootPath}/covers/` altına iner, ürünlerin ImageUrl'i dolar.

**Acceptance Scenarios**:

1. **Given** staging'de kapak yok ve seed JSON'da imageUrl dolu, **When** sistem açılır, **Then** migration kapağı indirir ve ürün `CoverIngested` zinciriyle kapağa kavuşur.
2. **Given** seed kaydında imageUrl boş, **When** migration o kaydı işler, **Then** kayıt atlanır; ürün placeholder'da kalır, başka hiçbir şey bloklanmaz.

---

### Edge Cases

- Seed JSON dosyası yok/bozuk → Catalog açılışı seed'i hata loglayıp atlar mı, fail-fast mı? Karar: dosya YOKSA atla (ör. prod'da seed kapalı), PARSE hatasında fail-fast (bozuk commit'li veri = geliştirme hatası, sessiz geçilmez).
- Aynı ISBN seed'de iki kez → ikinci satır atlanır (ilk kazanır), uyarı loglanır.
- Seed koşarken `admin_create_product` aynı ISBN'i girer → mevcut ISBN=Gtin çakışma reddi aynen korunur.
- Var olan DB'lerde taslak (yayınlanmamış) ürün kalmışsa → yayın kavramı kalktığı için hepsi canlı sayılır; vitrine ancak bir sonraki `ProductChangedEvent`'te (ör. `admin_republish_products` backfill) düşer.
- `ProductChangedEvent.IsDeleted` alanı kontratta kalır, Catalog artık hiç `true` göndermez — tüketiciler (Storefront/Library/Discount) değişmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Catalog açılışta commit'li seed JSON'dan katalogu doldurur: kayıt başına ürün + yazar(lar)/yayınevi/kategori bulun-veya-kur; ISBN=Gtin idempotency (var olan atlanır); ürün + `ProductAdded` aynı transaction.
- **FR-002**: Seed kaydındaki `stock` alanı `ProductAdded.InitialStock`'a taşınır; `admin_create_product` yolu InitialStock=0 ile sürer.
- **FR-003**: Ürün yaşam döngüsünde taslak/yayın/gizleme durumu YOKTUR: ürün canlı doğar, her değişim `ProductChangedEvent` yayar; `IsDeleted` hiçbir zaman `true` gönderilmez; event kontratı değişmez.
- **FR-004**: Fiyat>0 oluşturma/güncelleme invariant'ıdır (aggregate'te, Result ile reddedilir) — seed, elle giriş ve düzenleme dahil.
- **FR-005**: Excel import yüzeyi tamamen sökülür: `admin_import_catalog`, `admin_publish_imported`, `admin_get_import_status`, `admin_set_published` tool'ları, `/catalog-import/{token}` ekranı, ImportSession/ImportRow saklama ve arka plan işleyici.
- **FR-006**: Keşif/okuma yüzeyleri yayın filtresi uygulamaz (tüm ürünler görünür); yayında-olmayan verisi sızmaz kuralı anlamını yitirir ve kalkar.
- **FR-007**: File kapak migration'ı kaynak olarak aynı seed JSON'u (isbn+imageUrl) kullanır; repo-dışı xlsx bağımlılığı ve xlsx okuma kütüphanesi kalkar; R2/registry senkronları değişmez.
- **FR-008**: Seed ve migration'a dosya yolları merkezi host konfigürasyonundan verilir; seed açılış davranışı konfigürasyonla kapatılabilir.
- **FR-009**: Veri hazırlık zinciri (`scripts/book-import`) `stock` alanı üretir; nihai `books.json` repo'ya commit edilir.

### Key Entities

- **Seed kaydı (books.json satırı)**: isbn, title, authors[], publisher, priceTry (>0), imageUrl, categoryMid, categoryLeaf, stock. Tek dosya; Catalog (ürün doluşu) ve File (kapak kaynağı) aynı dosyayı okur — sözleşme alan adlarıdır.
- **Product**: yayın/taslak/gizleme durumu kalkar; fiyat>0 invariant'ı eklenir. Diğer davranışlar (fiyat geçmişi, kapak, tag/spec) aynen.
- **ImportSession / ImportRow**: SİLİNİR (saklama + şema dahil).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Boş ortamda tek komutla (sistem açılışı) katalog tamamen dolar — elle hiçbir adım (Excel yükleme, yayına alma) gerekmez; ikinci açılış sıfır yeni kayıt üretir.
- **SC-002**: Seed'deki TÜM kayıtlar (fiyat>0 olanlar) açılış sonrası asistan aramasında bulunabilir ve stok değerleri satır bazında doğrudur.
- **SC-003**: Admin MCP yüzeyinde import/yayın tool'u kalmaz; ürün oluşturma tek adımda (oluştur = canlı) tamamlanır.
- **SC-004**: Kapak migration'ı repo-dışı hiçbir dosyaya bağımlı değildir; temiz makinede repo + açılış kapakları indirir.

## Assumptions

- Seed verisi dev/demo içindir; prod doluşu ayrı konu — seed konfigürasyonla kapatılabilir (FR-008).
- `ProductChangedEvent` kontratı additive kuralla korunur; `IsDeleted` alanı ölü ağırlık olarak kalır (tüketici kırılmaz). İleride kontrat temizliği ayrı feature.
- Var olan dev DB'leri sıfırlanabilir (dev full-reset zaten olağan yol, 086); taslak-ürün göçü yazılmaz.
- MCP tool silme dış kırılmadır; tüketici kendi Claude Desktop kayıtları — kabul edildi.
- Anayasa İlke V capability-link istisnasının yaşayan tek örneği (083 ImportSession) kalkar → istisna UYKUDA işaretlenir (anayasa PATCH, telemetri istisnası emsali). İlke kalkmaz.
- `specs/083` tarihsel kalır; CLAUDE.md BC haritası + iki FLOW.md aynı PR'da güncellenir (İlke VII).
