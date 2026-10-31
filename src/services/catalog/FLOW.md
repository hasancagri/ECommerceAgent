# Catalog — Domain Süreci (EventStorming)

**BC ne yapar:** Kitap künyesini (ad/açıklama/fiyat/yazar/yayınevi/tek-leaf kategori/kapak) tutar ve
değişimi fat event'le duyurur. Ürünler **first-party**: kaynak commit'li `books.json` seed'i (feed yok).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🟩 read-model ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **095 notu (sade künye redesign):** nop mirası eksen SÖKÜLDÜ — Sku (kimlik artık Gtin/ISBN),
> Dimensions/Seo/vitrin bayrakları/ProductType, **ProductTag aggregate'i** ve **çoklu kategori ataması**
> gitti. Ürün TEK leaf kategoriye bağlanır; üst kategori ürüne yazılmaz, **yayın anında ağaçtan çözülür**.
> Editoryal "bu kategoriyle DE ilgili" listesi ayrı yaşar (`RelatedCategoryIds` — ata zinciriyle karışmaz).
> **Kategori ağacı KAPALI listedir**: yalnız seed kurar (2 seviye), ebeveyn doğumda sabittir —
> taşıma/yeniden adlandırma davranışı YOKTUR.

## Süreç

0. 🟪 **Açılışta kapalı kategori ağacı seed'den kurulur (095 Faz A)** —              `(BookSeedHostedService,`
   `books.json`'daki TÜM kanonik yollar (primary+related) tek taramada               ` CategoryTreeSeed.Build,`
   deterministik ağaca döner (kök → leaf, ≤2 seviye); var olan düğüme               ` SeedCategoryTree, Category.Create)`
   dokunulmaz. Ad ağaç-global unique varsayımı İHLALDE açılış durur
   (fail-fast — sessiz çift kayıt yok). "Genel" kökü hep kurulur (fallback).
1. 🟪 **Katalog satır satır dolar (091/095 Faz B)** → 🟧 **Ürün doğdu** —            `(SeedBookRecord, SeedBookRow)`
   ISBN=Gtin lookup → varsa ATLANIR (admin düzenlemesi ezilmez — upsert
   bilinçli reddedildi), yoksa ürün CANLI doğar; ürün+`ProductAdded`+
   `ProductChangedEvent` AYNI commit. Geçersiz satır (ISBN boş/fiyat≤0)
   uyarıyla atlanır; dosya yoksa seed sessiz atlanır, parse hatası durdurur.
2. 🟦 **Ürünü oluştur/güncelle** → 🟨 **Ürün doğar** — ad/ISBN/fiyat/açıklama;       `(Product.Create, Product.Rename,`
   fiyat>0 var oluş invariant'ı. _(giriş yüzeyi yok, REST planlı.)_                 ` Product.SetDescription)`
2a. 🟪 **Fiyat her gerçek değişimde geçmişe yazılır (058)** — ilk fiyat             `(Product.SetPrice,`
   ilk satırdır (append-only).                                                      ` ProductPriceChange)`
3. 🟨 **Yazar(lar) + yayınevi bulun-veya-doğur** — her yazar adı                    `(Author.Create, Product.SetAuthors,`
   normalize+aranır (çok-çok, Id listesi); yayınevi tek (çok-bir).                  ` Publisher.Create, Product.SetPublisher)`
4. 🟦 **Tek leaf kategoriye bağla** — leaf kapalı ağaçtan adla çözülür              `(NameNormalization.Normalize,`
   (çözülemeyen → "Genel"); üst kategori ürüne YAZILMAZ. İlgili                     ` Product.SetCategory,`
   kategoriler editoryal liste olarak ayrı atanır (çözülemeyen atlanır).            ` Product.SetRelatedCategories)`
5. 🟧⭐ **Ürün değişti** ⇨ Library — değişim KANONİK yayınlanır:                     `(ProductChangedEvent,`
   fiyat decimal; kategori AD+ID zinciri = leaf + ata (yayın anında ağaçtan         ` CategoryNaming.ResolveChain)`
   çözülür — related GİRMEZ); yazarlar (Id+ad) + yayınevi ADLA (fat event;
   tüketici lookup yapmaz). `IsDeleted` kontratta kalır, hep `false` gider;
   `CategoryIds` additive (eski tüketici `CategoryId` fallback'ine düşer).
6. 🟧 **Ürün doğdu** ⇨ File — yalnız kapak hattını tetikler (ISBN→ürün):            `(ProductAdded)`
   File `ProductAdded`'i tüketir, kapağı çözer. YALNIZ doğumda yayılır.
7. 🟧 **Kapak alındı (083/095)** ⇦ File → 🟪 **kapak ürüne yazılır** → 🟧            `(FileConsumers, CoverIngested,`
   **Ürün değişti** — File kapağı R2'de çözer, `CoverIngested(isbn,url)`            ` Product.SetImage, ProductChangedEvent)`
   yayar; Catalog `FileConsumers` tüketir → `Product.SetImage` →
   `ProductChangedEvent`. Kapak yoksa event yok — ürün placeholder'da kalır.
8. 🟪 **Embedding reconcile süreci (096)** — BC-içi dayanıklı döngü: vektörü                `(EmbeddingReconcileProcess,`
   eksik/bayat (içerik-hash ∨ model farkı) kitapları KENDİ DB'sinden bulur, LOCAL           ` EmbeddingText,`
   embedding modeliyle (Ollama) toplu vektörleştirir, yan dokümana yazar (broker            ` ProductEmbedding)`
   yok; seed'den bağımsız, eventual). Model değişimi toplu tazelemeyi aynı
   döngüden tetikler; Ollama kapalıyken hiçbir akış bloklanmaz (kendi kendini onarır).
   🟥 Hotspot (closed: ad + açıklama + yazar adları + kategori zinciri — 096 FR-003):
   embed edilen metnin bileşimi.
9. 🟩 Keşif/arama yüzeyi _(planned, 096-sonrası)_ — vektörlerin tüketicisi (hibrit
   arama, benzer-kitap, danışman agent) SONRAKİ spec'ler; 096 yalnız omurgayı döşer.
   Yayın filtresi yok — her ürün keşfedilebilir olacak.

## Domain kuralları (süreci yöneten değişmezler)

- **ISBN = kimlik (095).** Ürün Gtin'iyle doğar ve bulunur (`Gtin` unique-index — DB-seviyesi guard); Sku yok.
- **Tek leaf kategori zorunlu.** Ürün TEK leaf'e bağlanır `(Product.SetCategory)`; üst kategori ürüne denormalize edilmez — "üst kategorideki tüm kitaplar" ağaçtan cevaplanır. Kategorisiz seed satırı "Genel"e düşer.
- **İlgili kategoriler editoryal (095).** `RelatedCategoryIds` yalnız "bu kategoriyle DE ilgili" ilişkisi; primary/ata zinciriyle karışmaz, event zincirine girmez `(Product.SetRelatedCategories)`.
- **Kategori ağacı kapalı + doğumda sabit (095).** Ağaç yalnız seed'den (`CategoryTreeSeed.Build` → ≤2 seviye); ebeveyn doğumda sabit, taşı/yeniden adlandır davranışı yok; ad ağaç-global unique — ihlalde seed deterministik hata verir.
- **Fiyat>0 = var oluş invariant'ı (091).** Fiyatsız ürün DOĞAMAZ, fiyat sıfıra ÇEKİLEMEZ `(Product.Create, Product.SetPrice)`.
- **Ürün var = canlı (091).** Taslak/yayın/gizleme yok; her ürün doğduğu an keşfedilebilir.
- **Fiyat geçmişi append-only (058).** İlk fiyat dahil her değişim aynı transaction'da satıra döner `(ProductPriceChange)`.
- **Silme yok (016 sürer).** `ProductChangedEvent.IsDeleted` kontratta durur, hep `false` gider.
- **Seed var olana dokunmaz (091).** Yalnız EKSİK ISBN eklenir; admin düzenlemesi ezilmez.
- **Dış dünyaya iki yol = event.** `ProductChangedEvent` (her değişim → Library; ad+id zinciri yayın anında `CategoryNaming.ResolveChain` ile), `ProductAdded` (yalnız doğumda → File kapak hattı).

## Sınır (bu BC'nin dokunmadığı)

Metin/semantik arama (096 pg-hibrit'te gelecek — şu an yok). İndirim/fiyatlandırma motoru, sipariş,
ödeme, stok, PDF teslimi yok.
