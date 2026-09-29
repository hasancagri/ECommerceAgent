# Catalog — Domain Süreci

**BC ne yapar:** Satılabilir zengin ürünü (ad/fiyat/yazar/yayınevi/kategori/ölçü/SEO/özellik) tutar, vitrine açar
ve değişimi Storefront'a bildirir. Ürünler **first-party**: mağaza sahibi ekler/günceller (feed yok).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **050 pivot notu:** Çok-tedarikçi feed (Procurement) söküldü; model first-party'ye geçti. Ürün **yazım
> yolu (ürün-CRUD)** sonraki feature'da gelir. Aşağıki adım 1 o girişi bekler; adım 2+ domain davranışları
> (aggregate metotları) ürün oluşturulunca aynen işler.

> **083 notu (Excel katalog import):** Açılış `books.json` seeder'ı (051) SÖKÜLDÜ — katalog artık
> **admin xlsx yükleme**siyle dolar. `import_catalog` token-linkli ekran üretir; yüklenen satırlar
> `ImportRow` staging'e alınır (Excel bir kez okunur), `ImportProcessor` bekleyenleri **TASLAK** ürüne
> çevirir (ISBN=Gtin idempotency; exactly-once). Import ürünü **draft doğar** (fiyatlı olsa da yayın
> ayrı: `publish_imported`). Kapak **async** düşer (aşağıda adım 9a).

> **074 notu (MCP-only + elle giriş):** Domain iş REST yüzeyi söküldü — catalog admin/okuma tümüyle
> MCP (`/mcp` keşif + `/mcp-admin` yönetim). Doktrin kayması: import-only değil — admin `create_product`
> (`AdminCreateProduct`) ile elle künye girilir (TASLAK doğar; ISBN=Gtin çakışması reddedilir;
> yayın ayrı `admin_set_published`). Düzenleme = `AdminUpdateProduct` (058 ekran ikizi). `AdminCreateProduct`
> `ProductAdded`'i taslak anında yayınlar (InitialStock=0) — Stock bağı yayına kadar beklemez.

## Süreç

0. **Katalog xlsx'ten alınır (083).** Admin `import_catalog` ile token-       `(ImportCatalog, ImportSession,`
   linkli ekrandan xlsx yükler; satırlar staging'e (`ImportRow` Pending),   ` ImportUploadEndpointExtension, ImportRow,`
   `ImportProcessor` bekleyenleri TASLAK ürüne çevirir (ISBN=Gtin           ` ImportProcessor, ProcessImportRow)`
   idempotency; ürün+`ProductAdded`+Processed AYNI commit). Var olan ISBN atlanır (additive).
1. **Ürün komutla oluşturulur/güncellenir** (admin `create_product`       `(AdminCreateProduct,`
   elle giriş, veya `update_product` düzenleme; ISBN/ad/fiyat girdi).      ` AdminUpdateProduct, Product.Create, Product.Rename)`
   ISBN=Gtin ile bulun-veya-kur (idempotent).
1a. **Fiyat her gerçek değişimde geçmişe yazılır (058).** İlk fiyat      `(Product.SetPrice,`
   ilk satırdır; aynı fiyatla kayıt satır düşürmez (append-only).       ` ProductPriceChange)`
2. **Yazar(lar) + yayınevi bulun-veya-doğur.** Her yazar adı           `(Author.Create, Product.SetAuthors,`
   normalize+aranır (çok-çok, Id listesi); yayınevi tek (çok-bir).      ` Publisher.Create, Product.SetPublisher)`
   Yoksa girdiden doğar, Id ile referanslanır.
3. **Kategori seed'li ağaçtan çözülür.** Primary atama = seçilen        `(NameNormalization.Normalize,`
   kategori; bayat atamalar düşürülür.                                  ` Product.AssignToCategory, Product.RemoveFromCategory)`
4. **Kimlik + aile yazılır.** SKU/GTIN girdiden, aile kodu opsiyonel    `(Product.SetIdentifiers,`
   (null = ailesiz).                                                    ` Product.SetFamilyCode)`
5. **Ölçü + SEO doldurulur.** Ölçü girdiden (0 = bilinmiyor), SEO       `(Product.SetDimensions,`
   ad/açıklamadan türetilir.                                            ` Product.SetSeo)`
6. **Özellikler registry'den Id'ye çözülüp TAM yazılır.**              `(Product.SetSpecifications)`
   Bilinmeyen ad opsiyoneldir — yok sayılır, satır spec'siz ilerler.
7. **Yayın anahtarı admin'dedir (058) — kapı fiyat>0.** Fiyatsız         `(AdminSetPublished, PublishImported,`
   yayına alma reddedilir; yayından kaldırma vitrini gizler (silmez).   ` Product.Publish, Product.Unpublish)`
   Düzenleme yayın durumunu DEĞİŞTİRMEZ (koruma). Import taslakları
   toplu yayın: `publish_imported` yalnız import-kökenli + fiyat>0 taslakları canlıya çıkarır (083).
8. **Değişim Storefront'a KANONİK yayınlanır.** Fiyat decimal,          `(ProductChangedEvent)`
   kategori = primary; yazarlar (Id+ad çifti) + yayınevi ADLA taşınır
   (fat event; tüketici lookup yapmaz — özellik/spec taşınmaz). Yalnız YAYINDAKİ
   ürün yayar; yayından kaldırma `IsDeleted:true` ile gizletir (058).
9. **Stock'a bağ ürün DOĞARKEN kurulur.** Barkod→ürün eşlemesi + ilk      `(ProductAdded)`
   OnHand yazılır — taslak dahil ürün doğar doğmaz. Excel import'ta InitialStock=satırın
   Stock kolonu; elle `create_product` girişinde InitialStock=0 (admin sonra
   `admin_set_stock`/`admin_adjust_stock` ile gerçek adedi girer). Event olmadan
   taslak yayına alınsa bile Stock'ta satır olmaz, checkout CommitStock RECORD_NOT_FOUND reddederdi.

9a. **Kapak async File.Api'den düşer (083).** `ProductAdded`'i File.Api        `(FileConsumers, CoverIngested,`
   tüketir, kapağı R2'de çözer/yükler, `CoverIngested(isbn,url)` yayar;      ` Product.SetImage, ProductChangedEvent)`
   Catalog `FileConsumers` tüketir → `Product.SetImage` → `ProductChangedEvent`
   (kapak vitrine yansır). Kapak yoksa event yok — ürün placeholder'da kalır, import bloklanmaz.

10. **Keşif envanteri agent'a sunulur.** Yayındaki ürünlerde fiilen     `(ListCategories,`
   geçen kategori (üst-kategori ağacıyla), yazar ve yayınevi            ` ListAuthors,`
   listeleri; yayında olmayanın verisi sızmaz.                          ` ListPublishers)`

## Domain kuralları (süreci yöneten değişmezler)

- **Barkod = kimlik.** Ürün GTIN'iyle bulunur; aynı barkod tek ürüne düşer.
- **Kategori zorunlu, spec opsiyonel.** Kategori çözülemezse reddedilir; bilinmeyen spec sessiz düşer.
- **Yayın kapısı = fiyat>0 (051).** Fiyatsız ürün yayınlanamaz (satılamaz kart); taslak kalır, event yayılmaz.
- **Fiyat geçmişi append-only (058).** Her gerçek fiyat değişimi (ve ilk fiyat) fiyatla aynı transaction'da satıra döner; satır silinmez/değişmez `(ProductPriceChange)`.
- **Silme yok (016 sürer).** Ürün silinmez; `IsDeleted:true` yalnız yayından-kaldırmanın vitrin-gizleme bayrağıdır (058) — kayıt Catalog'da yaşamaya devam eder.
- **Dış dünyaya iki yol = event.** Storefront'a `ProductChangedEvent` (her değişim, yalnız yayındaki), Stock'a `ProductAdded` (ürün doğduğunda, taslak dahil — tek-seferlik, ISBN çakışma guard'ı tekrar tetiklenmeyi engeller).

## Sınır (bu BC'nin dokunmadığı)

Stok miktarı (OnHand) Stock BC'de; vitrin okuma-modeli + facet/arama Storefront'ta. İndirim/fiyatlandırma-
motoru, sipariş, ödeme yok.