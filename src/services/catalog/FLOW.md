# Catalog — Domain Süreci (EventStorming)

**BC ne yapar:** Satılabilir zengin ürünü (ad/fiyat/yazar/yayınevi/kategori/ölçü/SEO/özellik) tutar, vitrine açar
ve değişimi Storefront'a bildirir. Ürünler **first-party**: mağaza sahibi ekler/günceller (feed yok).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🟩 read-model ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **050 pivot notu:** Çok-tedarikçi feed (Procurement) söküldü; model first-party'ye geçti. Ürün **yazım
> yolu (ürün-CRUD)** sonraki feature'da gelir. Aşağıki adım 1 o girişi bekler; adım 2+ domain davranışları
> (aggregate metotları) ürün oluşturulunca aynen işler.

> **091 notu (JSON seed dönüşü + yayın makinesi sökümü):** 083 Excel import yolu SÖKÜLDÜ
> (`ImportSession`/`ImportRow`/token-link ekran/`publish_imported` gitti) — katalog açılışta **commit'li
> `books.json`**'dan dolar (051 deseninin dönüşü). **Taslak/yayın/gizleme ekseni de SÖKÜLDÜ**: ürün
> doğduğu an canlıdır; yayın kapısı yerine **fiyat>0 var oluş invariant'ı** (aggregate'te). Kapak
> **async** düşer (aşağıda adım 9a); File.Api kapak migration'ı AYNI seed dosyasını okur.

> **074 notu (MCP-only + elle giriş):** Domain iş REST yüzeyi söküldü — catalog admin/okuma tümüyle
> MCP (tek `/mcp`). Admin `create_product` (`AdminCreateProduct`) ile elle künye girilir (ISBN=Gtin
> çakışması reddedilir; 091: ürün CANLI doğar, ayrı yayın adımı yok). Düzenleme = `AdminUpdateProduct`
> (058 ekran ikizi). `AdminCreateProduct` `ProductAdded`'i (InitialStock=0) + `ProductChangedEvent`'i
> doğumda yayınlar — Stock bağı ve vitrin görünümü beklemez.

## Süreç

0. 🟪 **Açılışta katalog commit'li seed'den dolar (091)** → 🟧 **Ürün doğdu** —      `(BookSeedHostedService,`
   repo'daki `books.json` açılışta SATIR SATIR okunur: ISBN=Gtin lookup →          ` SeedBookRecord, SeedBookRow)`
   varsa ATLANIR (admin düzenlemesi ezilmez — upsert bilinçli reddedildi),
   yoksa ürün CANLI doğar; ürün+`ProductAdded`(satır stok)+`ProductChangedEvent`
   AYNI commit. Geçersiz satır (ISBN boş/fiyat≤0/stok<0) uyarıyla atlanır; dosya
   yoksa seed sessiz atlanır, parse hatası açılışı durdurur (fail-fast).
1. 🟦 **Ürünü oluştur/güncelle** → 🟨 **Ürün doğar** — admin `create_product`        `(AdminCreateProduct,`
   elle giriş, veya `update_product` düzenleme (ISBN/ad/fiyat girdi);              ` AdminUpdateProduct, Product.Create, Product.Rename)`
   ISBN=Gtin ile bulun-veya-kur (idempotent).
1a. 🟪 **Fiyat her gerçek değişimde geçmişe yazılır (058)** — ilk fiyat             `(Product.SetPrice,`
   ilk satırdır; aynı fiyatla kayıt satır düşürmez (append-only).                  ` ProductPriceChange)`
2. 🟨 **Yazar(lar) + yayınevi bulun-veya-doğur** — her yazar adı                   `(Author.Create, Product.SetAuthors,`
   normalize+aranır (çok-çok, Id listesi); yayınevi tek (çok-bir).                 ` Publisher.Create, Product.SetPublisher)`
   Yoksa girdiden doğar, Id ile referanslanır.
3. 🟦 **Kategoriyi seed'li ağaçtan çöz** — primary atama = seçilen                 `(NameNormalization.Normalize,`
   kategori; bayat atamalar düşürülür.                                             ` Product.AssignToCategory, Product.RemoveFromCategory)`
4. 🟦 **Kimlik + aileyi yaz** — SKU/GTIN girdiden, aile kodu opsiyonel             `(Product.SetIdentifiers,`
   (null = ailesiz).                                                               ` Product.SetFamilyCode)`
5. 🟦 **Ölçü + SEO'yu doldur** — ölçü girdiden (0 = bilinmiyor), SEO               `(Product.SetDimensions,`
   ad/açıklamadan türetilir.                                                       ` Product.SetSeo)`
6. 🟦 **Özellikleri registry'den Id'ye çöz + TAM yaz** — bilinmeyen ad             `(Product.SetSpecifications)`
   opsiyoneldir — yok sayılır, satır spec'siz ilerler.
7. _(091: yayın anahtarı adımı SÖKÜLDÜ — taslak/yayın/gizleme yok; fiyat>0 kapısı var oluşa taşındı, bkz. Domain kuralları.)_
8. 🟧⭐ **Ürün değişti** ⇨ Storefront, Library, Discount — değişim KANONİK          `(ProductChangedEvent)`
   yayınlanır: fiyat decimal, kategori = primary; yazarlar (Id+ad çifti) + yayınevi
   ADLA taşınır (fat event; tüketici lookup yapmaz — özellik/spec taşınmaz). 091:
   HER ürün değişimi yayar (yayın filtresi yok); `IsDeleted` kontratta kalır, hep `false` gider.
9. 🟧⭐ **Ürün doğdu** ⇨ Stock, File — Stock'a bağ ürün DOĞARKEN kurulur:           `(ProductAdded)`
   barkod→ürün eşlemesi + ilk OnHand yazılır — ürün doğar doğmaz, YALNIZ doğumda
   (tekrar yayım satışla düşen stoğu ezerdi). Seed'de InitialStock=satırın stock
   alanı; elle `create_product` girişinde InitialStock=0 (admin sonra
   `admin_set_stock`/`admin_adjust_stock` ile gerçek adedi girer). Event olmadan
   Stock'ta satır olmaz, checkout CommitStock RECORD_NOT_FOUND reddederdi.
9a. 🟧 **Kapak alındı (083)** ⇦ File → 🟪 **kapak ürüne yazılır** → 🟧 **Ürün        `(FileConsumers, CoverIngested,`
   değişti** — kapak async File.Api'den düşer: `ProductAdded`'i File.Api           ` Product.SetImage, ProductChangedEvent)`
   tüketir, kapağı R2'de çözer/yükler, `CoverIngested(isbn,url)` yayar; Catalog
   `FileConsumers` tüketir → `Product.SetImage` → `ProductChangedEvent` (kapak
   vitrine yansır). Kapak yoksa event yok — ürün placeholder'da kalır, seed bloklanmaz.
10. 🟩 **Keşif envanteri agent'a sunulur** — ürünlerde fiilen geçen                `(ListCategories,`
   kategori (üst-kategori ağacıyla), yazar ve yayınevi listeleri                   ` ListAuthors,`
   (091: yayın filtresi yok — her ürün keşfedilebilir).                            ` ListPublishers)`

## Domain kuralları (süreci yöneten değişmezler)

- **Barkod = kimlik.** Ürün GTIN'iyle bulunur; aynı barkod tek ürüne düşer (091: `Gtin` unique-index — DB-seviyesi guard).
- **Kategori zorunlu, spec opsiyonel.** Kategori çözülemezse reddedilir; bilinmeyen spec sessiz düşer.
- **Fiyat>0 = var oluş invariant'ı (091; 051 yayın kapısının halefi).** Fiyatsız/sıfır fiyatlı ürün DOĞAMAZ ve fiyat sıfıra ÇEKİLEMEZ `(Product.Create, Product.SetPrice)` — satılamaz kart hiç var olmaz.
- **Ürün var = canlı (091).** Taslak/yayın/gizleme durumu yok; her ürün doğduğu an keşfedilebilir ve satılabilir.
- **Fiyat geçmişi append-only (058).** Her gerçek fiyat değişimi (ve ilk fiyat) fiyatla aynı transaction'da satıra döner; satır silinmez/değişmez `(ProductPriceChange)`.
- **Silme yok (016 sürer).** Ürün silinmez; 091'de vitrin-gizleme de kalktı — `ProductChangedEvent.IsDeleted` kontratta durur, hep `false` gider.
- **Seed var olana dokunmaz (091).** Açılış seed'i yalnız EKSİK ISBN'i ekler; admin düzenlemesi seed'le ezilmez (upsert bilinçli reddedildi).
- **Dış dünyaya iki yol = event.** Storefront'a `ProductChangedEvent` (her değişim), Stock'a `ProductAdded` (yalnız ürün doğduğunda — tekrar yayım stok ezerdi; ISBN guard'ı tekrar tetiklenmeyi engeller).

## Sınır (bu BC'nin dokunmadığı)

Stok miktarı (OnHand) Stock BC'de; vitrin okuma-modeli + facet/arama Storefront'ta. İndirim/fiyatlandırma-
motoru, sipariş, ödeme yok.
