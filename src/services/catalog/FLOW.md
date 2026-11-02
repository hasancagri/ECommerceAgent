# Catalog — Domain Süreci (EventStorming)

**BC ne yapar:** Kitap künyesini (ad/açıklama/fiyat/yazar/yayınevi/tek-leaf kategori/kapak) tutar ve
değişimi fat event'le duyurur. Ürünler **first-party** (feed yok); 091 JSON seed'i SÖKÜLDÜ (098) —
giriş yolu Excel import olacak (spec yazılmadı, bugün ürün doğuran yol yok).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🟩 read-model ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; rename yapan anchor'ı da günceller (guard yok).

> **095 notu (sade künye redesign):** nop mirası eksen SÖKÜLDÜ — Sku (kimlik artık Gtin/ISBN),
> Dimensions/Seo/vitrin bayrakları/ProductType, **ProductTag aggregate'i** ve **çoklu kategori ataması**
> gitti. Ürün TEK leaf kategoriye bağlanır; üst kategori ürüne yazılmaz, **yayın anında ağaçtan çözülür**.
> Editoryal "bu kategoriyle DE ilgili" listesi ayrı yaşar (`RelatedCategoryIds` — ata zinciriyle karışmaz).
> **Kategori ağacı KAPALI listedir**: yalnız seed kurar (2 seviye), ebeveyn doğumda sabittir —
> taşıma/yeniden adlandırma davranışı YOKTUR.

## Süreç

0. 🟪 Excel import katalogu doldurur _(planned, 098 — 091 JSON seed'in yerine)_ —
   21-alan Kitapyurdu yeniden-kazıması (Kitapyurdu_Katalog_v2.xlsx) kategori ağacını
   kurup ürünleri satır satır doğurur; "Gtin varsa atla" + fail-fast ilkeleri
   091'den devralınır, ayrıntı spec'te. Bugün ürün/kategori doğuran yol YOK.
2. 🟦 **Ürünü oluştur/güncelle** → 🟨 **Ürün doğar** — ad/ISBN/fiyat/açıklama;       `(Product.Create,`
   fiyat>0 var oluş invariant'ı. _(giriş yüzeyi yok, REST planlı.)_                 ` Product.SetDescription)`
2b. 🟪 **Satışa-uygunluk türetilir (001/002)** — ürün fiyatsız/yazarsız da doğabilir       `(Product.IsSalable,`
   (KATALOGDA görünür); satışa-uygun = fiyat>0 ∧ stok>0 ∧ ≥1 yazar. Fiyat                  ` Product.SetPrice,`
   sonradan atanır/serbest değişir (097 revizesi); künyede cilt tipi +                     ` Product.SetBinding,`
   popülerlik sayaçları (dış kaynak anlık görüntüsü — satışa-uygunluğa                     ` Product.SetPopularity)`
   etkisiz). "Satışa çıktı" event'i/bildirimi SONRAKİ dilim.
2c. 🟧 **Adet değişti** ⇦ Stock — StockChanged tüketilir, üründeki adet KOPYASI            `(StockConsumers,`
   güncellenir (otorite Stock, tek yazar; mutlak değer → idempotent). Anomali              ` Product.SetStockCount)`
   yutulmaz: retry merdiveni → error-queue (DLQ, 088 deseni).
3. 🟨 **Yazar(lar) + yayınevi bulun-veya-doğur** — her yazar adı                    `(Author.Create, Product.SetAuthors,`
   normalize+aranır (çok-çok, Id listesi); yayınevi tek (çok-bir).                  ` Publisher.Create, Product.SetPublisher)`
4. 🟦 **Tek leaf kategoriye bağla** — leaf kapalı ağaçtan adla çözülür              `(NameNormalization.Normalize,`
   (çözülemeyen → "Genel"); üst kategori ürüne YAZILMAZ. İlgili                     ` Product.SetCategory,`
   kategoriler editoryal liste olarak ayrı atanır (çözülemeyen atlanır).            ` Product.SetRelatedCategories)`
5. 🟧⭐ **Ürün değişti** — değişim KANONİK yayınlanır: fiyat decimal;                `(ProductChangedEvent,`
   kategori AD+ID zinciri = leaf + ata (yayın anında ağaçtan çözülür —              ` CategoryNaming.ResolveChain)`
   related GİRMEZ); yazarlar (Id+ad) + yayınevi ADLA (fat event). `IsDeleted`
   kontratta kalır, hep `false` gider. **Tüketicisi ŞU AN YOK (097):** Library
   aboneliği söküldü (alarm tetiği öldü) — event ileriye dönük kontrat olarak
   yayılmaya devam eder (keşif/ML tüketicileri gelecek).
6. 🟧 **Ürün doğdu** ⇨ File — yalnız kapak hattını tetikler (ISBN→ürün):            `(ProductAdded)`
   File `ProductAdded`'i tüketir, kapağı çözer. YALNIZ doğumda yayılır.
   **Kapak dönüş ayağı öldü (097 serisi):** Catalog `CoverIngested` tüketmez —
   `Product.ImageUrl` dolmaz (kapaksızlık kullanıcı kararı); File yayını tüketicisiz.
8. 🟩 Keşif/arama yüzeyi _(planned)_ — hibrit arama, benzer-kitap, danışman agent
   SONRAKİ spec'ler. **096 embedding omurgası SÖKÜLDÜ (099 öncesi, kullanıcı kararı):**
   ProductEmbedding + Ollama hattı + reconcile süreci gitti; semantik ayak ileride
   yeniden, bilinçli kurulur. Yayın filtresi yok — her ürün keşfedilebilir olacak.

## Domain kuralları (süreci yöneten değişmezler)

- **ISBN = kimlik (095).** Ürün Gtin'iyle doğar ve bulunur (`Gtin` unique-index — DB-seviyesi guard); Sku yok.
- **Tek leaf kategori zorunlu.** Ürün TEK leaf'e bağlanır `(Product.SetCategory)`; üst kategori ürüne denormalize edilmez — "üst kategorideki tüm kitaplar" ağaçtan cevaplanır. Kategorisiz giriş satırı "Genel"e düşer.
- **İlgili kategoriler editoryal (095).** `RelatedCategoryIds` yalnız "bu kategoriyle DE ilgili" ilişkisi; primary/ata zinciriyle karışmaz, event zincirine girmez `(Product.SetRelatedCategories)`.
- **Kategori ağacı kapalı + doğumda sabit (095).** Ağaç yalnız toplu girişle kurulur (≤2 seviye; 091 seed'i söküldü, 098 Excel import devralacak); ebeveyn doğumda sabit, taşı/yeniden adlandır davranışı yok; ad ağaç-global unique — ihlalde kuruluş deterministik hata verir.
- **Fiyat opsiyonel, atanırsa >0 (001 — 097 revizesi).** Ürün fiyatsız doğabilir; fiyat sonradan atanır ve serbest değişir, her değer >0 `(Product.Create, Product.SetPrice)`. Fiyatı geri "fiyatsız"a çeviren yol yok; fiyat geçmişi (058) sökük kalır.
- **Satışa-uygunluk TEK tanım (001).** Satılabilir = fiyat>0 ∧ stok>0 ∧ ≥1 yazar `(Product.IsSalable)`; stok ayağı Stock dönene dek değerlendirme dışı. Kural hiçbir yüzeye kopyalanmaz.
- **Ürün var = keşfedilebilir (091 + 001 nüansı).** Taslak/yayın/gizleme yok; her ürün doğduğu an katalogda görünür — görünür ≠ satın alınabilir (IsSalable ayrı).
- **Silme yok (016 sürer).** `ProductChangedEvent.IsDeleted` kontratta durur, hep `false` gider.
- **Toplu giriş var olana dokunmaz (091 ilkesi sürer).** Yalnız EKSİK ISBN eklenir; admin düzenlemesi ezilmez (upsert bilinçli reddedildi).
- **Dış dünyaya iki yol = event.** `ProductChangedEvent` (her değişim; ad+id zinciri yayın anında `CategoryNaming.ResolveChain` ile — tüketicisi ŞU AN yok, 097), `ProductAdded` (yalnız doğumda → File kapak kayıt hattı).

## Sınır (bu BC'nin dokunmadığı)

Metin/semantik arama (pg-hibrit gelecek — şu an yok; 096 embedding omurgası söküldü). İndirim/fiyatlandırma motoru, sipariş,
ödeme, stok, PDF teslimi yok.
