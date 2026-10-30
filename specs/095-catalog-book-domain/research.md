# Research — 095 Catalog Kitap Domain Redesign

Phase 0 kararları. Her madde: Decision / Rationale / Alternatives.

## R1 — Discount üst-kategori eşleşmesi: additive `CategoryIds`

- **Decision**: `ProductChangedEvent`e `List<Guid>? CategoryIds = null` eklenir (leaf + ata zinciri
  id'leri; related HARİÇ — kullanıcı kararı, FR-005 editoryal ayrım). Discount `ProductCatalogRef.
  CategoryId` → `List<Guid> CategoryIds`; `CampaignSelectionResolver` Category kolu `Contains(scopeRef)`.
- **Rationale**: Bugünkü kod (`p.CategoryId == scopeRef`) üst-kategori kampanyasını leaf kitapla HİÇ
  eşleyemiyor — US2/SC-002 mevcut yapıyla imkansız. Spec'in "Discount kodu değişmez" varsayımından
  zorunlu sapma; değişiklik izdüşüm+resolver ile sınırlı, kampanya aggregate'i dokunulmaz.
- **Alternatives**: (a) Kampanyayı leaf kategorilere kısıtla → US2 ihlali, red. (b) Kategori ağacını
  Discount'a kopyala/seed'le → taksonomi BC'ye sızar, çift-seed bakımı, red. (c) Ad-bazlı eşleşme
  (`Categories` listesi) → scopeRef Guid kontratı kırılır, red.

## R2 — Event `Categories` (ad listesi) semantiği: leaf + ata adları

- **Decision**: `Categories` yayın anında ağaçtan çözülür = leaf adı + ata adları (US2 acceptance 1
  birebir: "Popüler Kültür" + "Kültür"). Related adları ARTIK girmez.
- **Rationale**: FR-009 "ata zinciri dahil, tüketici lookup yapmaz". Alanın canlı tüketicisi yok
  (Storefront söküldü) — semantik daraltma güvenli; id listesi (R1) ile tutarlı.
- **Alternatives**: Mevcut davranışı koru (related adları dahil) → id/ad listeleri farklı anlam taşır,
  kafa karışıklığı; tüketicisi olmayan genişlik, red.

## R3 — FileConsumers restorasyonu (kapak hattı)

- **Decision**: Silinen `Catalog.Api/FileConsumers.cs` (7c641d9'da gitti; kuyruk dinlemesi
  `MessagingExtensions`'ta duruyor) yeni modele göre geri yazılır: ISBN→ürün (`Gtin` lookup) →
  `SetImage(url)` → fat `ProductChangedEvent`. Kategori ad+id zinciri çözümü `Helpers/` altında ortak
  helper'a alınır; seed ile paylaşılır (saf, unit-test edilebilir).
- **Rationale**: FR-011 kapak hattının sürmesini şart koşar; handler yokken `CoverIngested` sessizce
  error-queue'ya düşer. Eski içerik git tarihinde referans (`git show 7c641d9~1:…/FileConsumers.cs`).
- **Alternatives**: Ayrı fix-PR → aynı event şekillendirme koduna iki kez dokunulur; model değişimiyle
  atomik gitmesi doğru, red.

## R4 — İki-fazlı seed

- **Decision**: Faz A: `books.json` tek tarama → tüm `categoryPath` + `relatedCategoryPaths`'ten kapalı
  ağaç (31 üst + ~180 alt) deterministik kurulur; alt-kategori adı ağaç-global unique varsayımı ihlalde
  seed deterministik hata verir. Faz B: satır satır ürün — Gtin-varsa-atla, kategorisiz satır "Genel"
  leaf fallback, çözülemeyen related leaf atlanır (satır reddedilmez).
- **Rationale**: FR-007 "ağaç yalnız seed'den kurulur"; satır-içi get-or-create'in leaf==üst / çift-kayıt
  tuzakları (bkz. memory: leaf==mid unique ihlali) faz ayrımıyla ölür; Category.Create-only modeline uyar.
- **Alternatives**: Mevcut satır-içi `GetOrCreateCategoryChainAsync` korunur → ağaç "seed'den kurulur"
  garantisi zayıf, sıra-bağımlı; red.

## R5 — Product.Create imzası: kimlik Gtin

- **Decision**: `Product.Create(name, gtin, price, description)`; Sku ve `SetIdentifiers` ölür. Gtin
  required + unique-index (mevcut index kalır). Tek açıklama alanı `Description`.
- **Rationale**: SKU=ISBN aynılığı zaten vardı (seed `sku: isbn` yazıyordu) — iki alan tek gerçeğin
  kopyasıydı. FR-001/002.
- **Alternatives**: Sku'yu "iç kod" olarak tut → kullanılmıyor, nop mirası, red.

## R6 — Kontrat alan temizliği yapılmaz (additive-uyum)

- **Decision**: `FamilyCode` kontratta kalır, hep null; `IsDeleted` hep false sürer; `OldPrice`
  davranışı aynen (yalnız fiyat değişiminde dolu).
- **Rationale**: Spec assumption — kontrat temizliği ayrı karar; eski tüketici kırılmaz.
- **Alternatives**: Alan söküp kontrat versiyonla → bu feature'ın işi değil, red.

## R7 — DB/migration yok

- **Decision**: Migration yazılmaz; hedef temiz-DB açılışı (SC-001). Doc-store'da sökülen alanlar
  deserialize'da yok sayılır, ama eski-DB üstünde çoklu-kategori verisi anlamsız kalacağından dev
  ortamlarında DB reset önerilir (quickstart'ta).
- **Rationale**: Tek canlı tüketici seed + event tüketicileri; dış yüzey yok → geri-uyum ekseni dar
  (spec assumption). Marten şeması `ApplyAllDatabaseChangesOnStartup`.
- **Alternatives**: Veri taşıma script'i → taşınacak kıymetli veri yok (seed yeniden doldurur), red.
