# Feature Specification: Catalog Kitap Domain Redesign (sade künye modeli)

**Feature Branch**: `095-catalog-book-domain`

**Created**: 2026-10-30

**Status**: Draft

**Input**: User description: "Catalog aggregate redesign (Kitapyurdu, PDF-only, kişiselleştirilmiş keşif sitesi için sade künye modeli) — kilitli tasarım: sade Product künyesi, tek-leaf kategori, kapalı kategori ağacı, ProductTag sökümü; seed hizası; pgvector/arama AYRI spec."

> **KAPSAM DEĞİŞİKLİĞİ (2026-10-31, implement sırasında kullanıcı kararı):** Discount BC bu branch'te
> KOMPLE SÖKÜLDÜ (kampanya/indirim modeli istenmedi). US2'nin ve FR-009/SC-002'nin Discount ayağı
> ÖNCE uygulandı (CategoryIds + izdüşüm + resolver), sonra BC ile birlikte kalktı. Event'teki
> `CategoryIds` alanı kontratta KALIR (üst-kategori eşleşmesi 096 keşif için de gerekli). US2 artık
> yalnız Library kesintisizliğini kapsar; checkout tutarı = liste fiyatı.

## Bağlam

Temiz-sayfa sökümü (PR #168) sonrası Catalog, nopCommerce mirası alanlar + fiziki-ürün ekseni
taşıyan şişkin bir modelle kaldı. Mağaza artık **PDF-only dijital kitapçı**, hedef yüzey
**kişiselleştirilmiş keşif sitesi**; veri gerçeği commit'li Kitapyurdu `books.json` (11.438 kitap,
kanonik 2 seviyeli kategori ağacı). Bu feature domain modelini o gerçeğe indirger.
**Arama/pgvector okuma yolu AYRI feature'dır (096)** — bu spec yalnız künye modeli.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Seed yeni modelle katalogu doldurur (Priority: P1)

Sistem açılışında `books.json`'daki 11.438 kitap yeni sade modele yüklenir: her kitap tek leaf
kategoriye bağlanır, ilgili kategoriler ayrı listede tutulur, kategori ağacı (31 üst + ~180 alt)
seed'den deterministik kurulur.

**Why this priority**: Katalog verisi her şeyin girdisi — model değişikliği seed'le birlikte
yaşamazsa sistem boş/yanlış doğar.

**Independent Test**: Temiz DB ile açılış; ürün sayısı, kategori ağacı ve örnek kitapların
leaf/ilgili kategori atamaları doğrulanır.

**Acceptance Scenarios**:

1. **Given** boş catalogDb, **When** sistem açılır, **Then** 11.438 kitap CANLI doğar; her ürünün
   TEK leaf kategorisi vardır; `relatedCategoryPaths` taşıyan kitapların ilgili kategori listesi dolar.
2. **Given** kategorisi "Kültür > Popüler Kültür" olan seed satırı, **When** yüklenir, **Then**
   ürün yalnız "Popüler Kültür" leaf'ine bağlanır; "Kültür" ataması ürüne YAZILMAZ (ağaçtan çözülür).
3. **Given** Gtin'i zaten var olan satır, **When** seed tekrar koşar, **Then** satır atlanır
   (mevcut davranış korunur).

---

### User Story 2 - Downstream tüketiciler kesintisiz çalışır (Priority: P2)

Library (fiyat alarmı) ve Discount (kampanya süzgeci için taksonomi izdüşümü) `ProductChangedEvent`
üzerinden beslenmeye aynen devam eder; üst kategoriye açılan kampanya alt-leaf kitaplarını bulmayı sürdürür.

**Why this priority**: Model sadeleşmesi iç iştir; dış kontrat kırılırsa iki BC sessizce bozulur.

**Independent Test**: Seed/ürün değişimi sonrası yayınlanan event'in kategori zinciri (leaf + üst)
ve fiyat alanlarının dolu geldiği; Discount izdüşümünün üst-kategori eşleşmesini koruduğu doğrulanır.

**Acceptance Scenarios**:

1. **Given** "Popüler Kültür" leaf'indeki kitap, **When** `ProductChangedEvent` yayılır, **Then**
   event kategori adlarını ata zinciri DAHİL taşır ("Popüler Kültür" + "Kültür") — tüketici lookup yapmaz.
2. **Given** "Kültür" üst kategorisine açılmış kampanya, **When** izdüşüm event'ten güncellenir,
   **Then** "Popüler Kültür" kitabı kampanya kapsamına girer.
3. **Given** fiyat değişimi, **When** event yayılır, **Then** `OldPrice` davranışı değişmeden sürer
   (Library alarm tetiği etkilenmez).

---

### User Story 3 - Ölü model sökümü ve test hizası (Priority: P3)

Product'tan fiziki/nop mirası alanlar, çoklu kategori ataması ve ProductTag aggregate'i sökülür;
Category sade kapalı-liste düğümüne iner; Catalog testleri yeni modele hizalanır.

**Why this priority**: Sadeleşmenin kendisi — ama US1/US2 çalışmadan tek başına değer üretmez.

**Independent Test**: Çözüm derlenir, Catalog.Api.Tests yeşil; sökülen alan/aggregate'lere kod
tabanında referans kalmaz.

**Acceptance Scenarios**:

1. **Given** yeni model, **When** çözüm derlenir ve testler koşar, **Then** sökülen alanlara
   (Sku, Dimensions, ProductType, tag/spec…) hiçbir canlı referans kalmaz, testler yeşildir.
2. **Given** Category düğümü, **When** doğar, **Then** ebeveyni doğumda sabittir; taşıma/yeniden
   adlandırma davranışı YOKTUR; ağaç yalnız seed'den kurulur.

---

### Edge Cases

- Kategorisiz seed satırı (7 adet): mevcut "Genel" fallback davranışı korunur (tek leaf "Genel").
- `relatedCategoryPaths` çözülemeyen leaf içerirse: satır reddedilmez; çözülemeyen ilgili kategori
  atlanır (primary leaf çözülemiyorsa mevcut davranış: fallback/reject korunur).
- Dil/sayfa/tarih alanları boş satırlar (ör. 48 sayfasız, 9 dilsiz): alanlar opsiyonel — ürün doğar.
- Aynı ada sahip alt kategori iki üstte YOK (veriden doğrulandı) — ad ağaç-global unique varsayımı
  bozulursa seed deterministik hata verir (sessiz çift kayıt üretmez).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Product şu künyeyi taşır: Gtin (ISBN, benzersiz kimlik), Name, Description (tek
  açıklama alanı), Price (>0 var oluş invariant'ı sürer), PageCount?, PublishedOn?, Language?,
  Translators (ad listesi), AuthorIds, PublisherId, CategoryId (tek leaf), RelatedCategoryIds
  (editoryal ilişki listesi), ImageUrl?.
- **FR-002**: Product'tan şunlar sökülür: Sku, ShortDescription, Dimensions, ProductType +
  ParentGroupedProductId + FamilyCode, ManufacturerPartNumber, ShowOnHomepage, MarkAsNew,
  AllowCustomerReviews, çoklu kategori ataması, TagIds.
- **FR-003**: ProductTag aggregate'i ve tüm izleri (model + testler) komple sökülür.
- **FR-004**: Ürün TEK leaf kategoriye bağlanır; üst kategori bilgisi ürüne denormalize edilmez,
  kategori ağacından çözülür. "Üst kategorideki tüm kitaplar" sorgusu ağaç üzerinden cevaplanabilir.
- **FR-005**: RelatedCategoryIds yalnız editoryal "bu kategoriyle DE ilgili" ilişkisini taşır;
  ata zinciriyle karıştırılmaz.
- **FR-006**: Category sadeleşir: Name, NormalizedName, ParentCategoryId (en fazla 2 seviye).
  Description/Seo/Published/ShowOnHomepage/DisplayOrder sökülür.
- **FR-007**: Kategori ağacı KAPALI listedir: yalnız seed'den kurulur (31 üst + ~180 alt);
  ebeveyn doğumda sabittir, taşıma/oluşturma/düzenleme davranışı ve yüzeyi yoktur.
- **FR-008**: Seed `books.json` alanlarını yeni modele eşler: `categoryPath` leaf'i tek atamaya,
  `relatedCategoryPaths` leaf'leri RelatedCategoryIds'e; Gtin-varsa-atla ve kategorisiz satır
  fallback davranışları korunur.
- **FR-009**: `ProductChangedEvent` dış kontratı KIRILMAZ: CategoryId=leaf; kategori ad listesi
  (ata zinciri dahil) yayın anında ağaçtan çözülerek dolu gider; OldPrice/IsDeleted davranışı aynı;
  sökülen alanlar kontratta additive-uyumlu biçimde (hep null/boş) kalabilir.
- **FR-010**: Fiyat geçmişi (ProductPriceChange, append-only) etkilenmez.
- **FR-011**: Kapak hattı korunur: `ProductAdded` → File → `CoverIngested` → ImageUrl dolumu.
  Catalog'un `CoverIngested` tüketicisi (`FileConsumers`) temiz-sayfa sökümünde yanlışlıkla silinmişti;
  bu feature'da yeni modele göre geri yazılır (bkz. plan.md keşif bulgusu 2).
- **FR-012**: Author (Name, NormalizedName, Bio) ve Publisher modelleri değişmez.
- **FR-013**: Catalog FLOW.md yeni sürece hizalanır (İlke VII; aynı PR).

### Key Entities

- **Product**: Kitap künyesi — kimlik ISBN; tek leaf kategori + editoryal ilgili kategoriler;
  yazar/yayınevi/çevirmen ilişkileri; fiyat (fiyat geçmişiyle).
- **Category**: Kapalı 2 seviyeli ağacın düğümü; doğumda sabit ebeveyn; seed-doğumlu.
- **Author**: Yazar kimliği + biyografi (değişmiyor).
- **Publisher**: Yayınevi (değişmiyor).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Temiz açılışta 11.438 kitabın tamamı yeni modelle yüklenir; kategori ağacı 31 üst +
  ~180 alt düğümle, çift kayıtsız kurulur.
- **SC-002**: Üst kategori kampanyası alt-leaf kitaplarını yakalamaya devam eder (Discount izdüşüm
  eşleşmesi seed sonrası örneklem kontrolüyle %100).
- **SC-003**: Fiyat değişimi alarm tetiği davranışı değişmez (Library event tüketimi kesintisiz).
- **SC-004**: Çözüm 0 hatayla derlenir; tüm test projeleri yeşil; sökülen alan/aggregate'lere canlı
  referans kalmaz.

## Assumptions

- **Event kontratı fat kalır:** depoda tek leaf tutulsa da `ProductChangedEvent` kategori ad VE id
  zincirini çözüp taşır ("tüketici lookup yapmaz"). Library kodu değişmez; Discount izdüşüm+resolver
  additive `CategoryIds` ile güncellenir — mevcut tek-Guid eşleşmesi üst-kategori kampanyasını
  yakalayamıyor (bkz. plan.md keşif bulgusu 1 + research.md R1).
- **Kontrat alan sökümü additive-uyumlu:** FamilyCode gibi alanlar kontratta null olarak kalabilir;
  kontrat temizliği ayrı karar (bu feature zorunlu kılmaz).
- **Arama/pgvector 096'da:** Description bu modelde gelecekteki embedding kaynağı olarak korunur;
  okuma-yolu/index bu spec'in dışında.
- **Satış/PDF teslimi park:** ürünün dijital varlığına (dosya) dair hiçbir alan bu modele girmez.
- **Dış yüzey yok:** MCP söküldü, REST henüz yok — model değişikliğinin tek canlı tüketicisi seed +
  event tüketicileri; bu yüzden migration/geri-uyum ekseni dar.
- **Veri varsayımı:** alt kategori adı ağaç-global unique (11.438 kayıtta doğrulandı); seed buna güvenir.
