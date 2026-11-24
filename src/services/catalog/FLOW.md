# Catalog — Domain Süreci (EventStorming)

**BC ne yapar:** Kitap künyesini (ad/açıklama/fiyat/yazar/yayınevi/taksonomi/kapak) tutar ve değişimi
fat event'le duyurur. Ürünler **first-party** (feed yok); giriş yolu v11 Excel import (021 — çok-sayfalı,
INSERT-ONLY, iki-faz dry-run/confirm). Keşif ekseni kapalı-sözlük taksonomi (kategori 021'de SÖKÜLDÜ).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🟩 read-model ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; rename yapan anchor'ı da günceller (guard yok).

> **095 notu (sade künye redesign):** nop mirası eksen SÖKÜLDÜ — Sku (kimlik artık Gtin/ISBN),
> Dimensions/Seo/vitrin bayrakları/ProductType, **ProductTag aggregate'i** ve **çoklu kategori ataması**
> gitti. Ürün TEK leaf kategoriye bağlanır; üst kategori ürüne yazılmaz, **yayın anında ağaçtan çözülür**.
> Editoryal "bu kategoriyle DE ilgili" listesi ayrı yaşar (`RelatedCategoryIds` — ata zinciriyle karışmaz).
> **Kategori ağacı KAPALI listedir**: yalnız seed kurar (2 seviye), ebeveyn doğumda sabittir —
> taşıma/yeniden adlandırma davranışı YOKTUR.

> **021 notu (içerik-temelli keşif, v11 katalog — as-built):** kullanıcı-etkileşim temelli benzerlik
> TERK; sınıflandırma YAZMA ANINDA (v11 Excel LLM pipeline çıktısı), okuma anı deterministik.
> **Kategori ekseni SÖKÜLDÜ** (Category aggregate + RelatedCategoryIds + ağaç + CategoryNaming +
> categoryId filtresi) — yerine kapalı-sözlük taksonomi: `Genre`/`Moods`/`AudienceLevel`/`Themes`
> (+`EnrichmentConfidence`), değerler Türkçe sözlük (`BookTaxonomy`). **Etiketler (009 BookTag) SÖKÜLDÜ.**
> **Eser kimliği (WorkKey):** import anında yazar+parantezsiz-başlıktan türetilir; benzerlik aynı eserin
> baskılarını tekilleştirir. **Stok ikili sinyal:** "SATIŞ YOK*" satırı `ProductAdded(InStock:false)` →
> Stock 0; satıştaki random 20-50; `Product.InStock` hesaplanan (StockCount>0).

## Süreç

0. 👤 Admin Excel yükler (gateway /api/admin/catalog/imports, anahtar-korumalı)        `(StartImport,`
   → 🟦 **Import işini başlat** → 🟨 **Import işi doğar** → 🟪 **DRY-RUN arka planda**    ` ImportJob,`
   **koşar** — tüm satırlar denenir, HİÇBİR ŞEY yazılmaz; sebepli rapor birikir           ` ImportJobWorker,`
   (işlenecek/eklenecek/atlanacak/hatalı), iş ONAY BEKLER. Aynı anda TEK                  ` ExcelRowMapper)`
   aktif import; onay bekleyen eski iş yeni yüklemeyle geçersiz kılınır.
   Yüklenen dosya silinmez — iş kaydının diskteki izidir.
0b. 👤 Admin raporu görür ve onaylar (POST /imports/{id}/confirm)                     `(ConfirmImport,`
   → 🟦 **Import'u onayla** → 🟪 **Satırlar arka planda YAZILIR** — sayfa=yayınevi;      ` ImportJob.Confirm)`
   INSERT-ONLY (ISBN varsa DOKUNMA, "atlandı"); yazar/yayınevi/kategori
   bulun-veya-doğur; fiyatsız satır satış-dışı doğar; satır hatası işi durdurmaz;
   onayda dry-run sayaçları sıfırlanır, kalıcı rapor gerçek koşudan üretilir.
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
2d. 🟧 **Etkin indirim değişti** ⇦ Discount — ProductDiscountChanged tüketilir,            `(DiscountConsumers,`
   üründeki ORAN kopyası güncellenir/temizlenir (null = indirim yok; liste                 ` Product.SetDiscountRate)`
   fiyatı EZİLMEZ, indirimli fiyat okuyanda hesaplanır). DLQ deseni aynı.
   IsSalable'a GİRMEZ. _(Yayıncı yürürlük süreci Discount'ta sonraki dilim.)_
3. 🟨 **Yazar(lar) + yayınevi bulun-veya-doğur** — her yazar adı                    `(Author.Create, Product.SetAuthors,`
   normalize+aranır (çok-çok, Id listesi); yayınevi tek (çok-bir).                  ` Publisher.Create, Product.SetPublisher)`
4. 🟦 **Taksonomiyi ata** (021 — kategori ekseni SÖKÜLDÜ) — Excel tur/ton/seviye/     `(ExcelRowMapper,`
   konu/guven kolonları HAM okunur → `SetEnrichment` kapalı sözlüğe (BookTaxonomy)      ` Product.SetEnrichment)`
   kanonikleştirir (takma-ad eşle, karşılıksız DÜŞER, tur/seviye geçersiz→"belirsiz",
   kardinalite kırp, güven clamp). Okuma anında deterministik facet olur.
4e. 🟦 **Eser kimliğini türet** (021) — WorkKey = normalize(yazarlar sıralı) +        `(WorkKeyNormalizer.Derive,`
   normalize(parantezsiz başlık); import anında hesaplanır, ürüne yazılır              ` Product.SetWorkKey)`
   (Türkçe fold → ASCII). Kullanıcıya/LLM'e görünmez iç alan; benzerlik tekilleştirmesi bundan.
4c. 🟦 **İndirimi ithal et** (010) — Excel `İndirim %` okunur (ZATEN kesir, 0.285=%28.5);   `(ExcelRowMapper,`
   >0 ∧ ≤0.90 ise ürün yazıldıktan SONRA `ProductDiscountImported(productId, rate)`           ` ImportJobWorker)`
   yayılır ⇨ Discount (ithal: indirim bizim değil Kitapyurdu'nun). 0/boş → event yok.
6. 🟧 **Ürün doğdu** ⇨ File, Stock (010/021) — File kapak hattını tetikler; Stock      `(ProductAdded)`
   `ProductAdded`'i tüketir: `InStock` sinyaline göre adet (false→0, true→rastgele
   20-50) ile stok defteri açar. Gerçek adet İTHAL edilmez (Excel stok %97 boş); sinyal ikili.
5. 🟧⭐ **Ürün değişti** — değişim KANONİK yayınlanır: fiyat decimal; yazarlar        `(ProductChangedEvent)`
   (Id+ad) + yayınevi ADLA (fat event). `IsDeleted` kontratta kalır, hep `false`.
   **021: kategori alanları kontrattan SÖKÜLDÜ** (tüketicisiz, 097); taksonomi tüketici
   doğunca additive eklenir. **Tüketicisi ŞU AN YOK** — ileriye dönük kontrat olarak yayılır.
6. 🟧 **Ürün doğdu** ⇨ File — yalnız kapak hattını tetikler (ISBN→ürün):            `(ProductAdded)`
   File `ProductAdded`'i tüketir, kapağı çözer. YALNIZ doğumda yayılır.
6b. 🟧 **Kapak alındı** ⇦ File → 🟪 **Kapak ürüne bağlanır** (004 — 097 kapaksızlık      `(FileConsumers,`
   kararı TERSİNE) — `CoverIngested(isbn,url)` tüketilir, Gtin'le ürün bulunur,          ` Product.SetImage)`
   kapak adresi dolar (idempotent); kapak gelmeyen ürün kapaksız kalır (iş durmaz).
   DLQ deseni aynı (retry → error-queue).
8. 🟩 Birleşik arama (018) — ParadeDB pg_search BM25 TEK motor; anonim keşif,               `(ProductSearchSchemaInitializer,`
   mt_doc_product tek BM25 index (fast-field + ICU; Marten'e dokunmaz):                      ` ProductEndpointExtension)`
8a. 🟦 Ara (018 US1) — metin+filtre+sort+sayfalama TEK SearchRequest → tek SQL:             `(ProductSearchQueries.SearchAsync,`
   metin var → `@@@`+`paradedb.score` (alaka); açık sort → fast-field ORDER BY;             ` SearchPredicateBuilder, SearchProducts,`
   metin+sort yok → `paradedb.all()` + popülerlik. Filtre (yayınevi/fiyat/sayfa/cilt/        ` SearchBooks.Tool)`
   satışta/indirimli) `@@@` clause; **taksonomi facet (021) + stokta + exclude-yayınevi `@@@`
   DIŞI jsonb SQL predicate** (çok-kelime token çarpışması önlenir). Sıralı id → künye (Marten,
   sıra korunur). REST `/products/search` + MCP `search_books` AYNI yol (mode YOK).
   **018: semantik/embedding/RRF/pgvector SÖKÜLDÜ — BM25 tek motor; semantik ileride yeni spec.**
   Yayın filtresi yok — her ürün keşfedilebilir.
8b. 🟧 **Kullanıcı aradı** ⇨ Personalization (012/014) — login aramada `UserSearched(userId, query,   `(SearchBooks.Tool)`
   mode, resultCount, searchedAt)` yayınlanır (fanout). **014 (F1): yayın noktası ScoutAgent
   `search_books` MCP tool'u** — keşif sohbetten geçer; `/products/search` REST ucu saf okuma
   (yayınlamaz). Opsiyonel JWT'den userId; anonim/boş sorgu yayınlamaz. Publish ayrık + hata-yutar.
8c. 🟦 Facet'le ara (019) — tür/ton/seviye/konu filtreleri aramaya girer (çoklu değer OR; sözlük-dışı
   değer reddedilir); serbest metin BM25 grubu tur/ton/konu değerlerini de tarar (Etiketler'in
   yerine). categoryId filtresi ölür. REST + MCP aynı yol. _(planned)_
8d. 🟦 Benzer kitap bul (027 V2 — vektör) — similar_books MCP tool'u (yalnız Kâşif): çapa kitabın
   embedding'ine kNN (`<=>` kosinüs; anlam, ortak etiket şart değil) + hard filtre (dil, çocuk/yetişkin
   seviye grubu, kendi WorkKey baskıları hariç) + WorkKey tekilleştirme (eser başına 1 `DISTINCT ON`) +
   stok-önce sıralama. Çapa embed'i yoksa boş. 021 etiket-BM25 örtüşmesi + boost + fallback SÖKÜLDÜ (027).

🟥 Hotspot (closed: 021): `Tagged`→`Enriched` oldu; similar_books varsayılan 5/üst 20 + boost
   katsayıları `SimilarBooksOptions` (config); "stokta" REST+MCP filtresi (`OnlyInStock`) bu dilime girdi.

## Embedding omurgası (025 Dilim-1 — as-built)

> Semantik ayak BİLİNÇLİ döndü (096 "ezber oldu" diye söküldü; bu sefer dilimli/kanıtlı). Dilim-1
> YALNIZ omurga: kitapları vektörleştir + sakla. Tüketici (semantik arama/benzer V2/taste) sonraki
> dilimler. Sorgu mimarisi KİLİTLİ: katmanlı rol-ayrımı (filtre=jsonb · lexical=BM25 · semantik=vektör ·
> kişisel=taste) — RRF füzyon YOK. Model: OpenAI `text-embedding-3-small` (1536-d, çok-dilli).

E1. 🟦 **Embedding şemasını kur** — açılışta `vector` eklenti + ayrı `product_embedding` tablosu       `(ProductSearchSchemaInitializer)`
   (native `vector(1536)` + input_hash) + HNSW index; idempotent (ParadeDB BM25 ile aynı dizi,
   mevcut DB'de 018 DROP izi kaldırıldı). Marten `data` jsonb'ye DOKUNMAZ (ham-SQL dünyası).
E2. 🟦 **Embed metnini kur** — SAF (İlke VI test-first): `Name` + `Description` (008 zengin) +           `(EmbeddingText.Build)`
   `Genre`/`Moods`/`Themes` → hafif-etiketli blob. Fiyat/stok/dil/sayfa/yayınevi/`AudienceLevel`/
   yazar GİRMEZ (değişken ya da filtre). "belirsiz" tür + boş çok-değerli satırlar atlanır.
E3. 🟪 **Girdi değişmediyse atla** — blob SHA256 hash'i `input_hash`'e; aynıysa yeniden-embed YOK        `(EmbeddingText.Hash,`
   (idempotent + maliyet). v1 worker yalnız "eksik"i yazar; hash ileride değişim-algılama için hazır.   ` EmbeddingDecision.ShouldGenerate)`
E4. 🟦⭐ **Eksik embedding'leri doldur** — worker her tick embedding'i OLMAYAN ürünü yoklar              `(EmbeddingBackfillWorker,`
   (`LEFT JOIN product_embedding IS NULL`) → ürünleri Marten'den yükle → batch OpenAI embed            ` EmbeddingClientFactory)`
   → upsert (vektör text-literal + `::vector` CAST; Npgsql vector plugin gerekmez). Resumable
   (durum izlemez, eksik-sorgusu kaldığı yerden sürer); hepsi embed → boşta bekler.
E5. 🟪 **Yeni ürün kendiliğinden yakalanır** ⇦ kendi `ProductAdded`'i — doğan ürün embedding'siz;        `(EmbeddingBackfillWorker)`
   sonraki tick "eksik" sorgusunda bulur. **Ayrı consumer YOK** (backfill + artımlı tek mekanizma).
E6. 🟧 **Embedding üretildi** — vektör `product_embedding`'e + HNSW'e girer (BC-içi; entegrasyon           `(EmbeddingBackfillWorker)`
   event'i YOK — dışarıdan tüketen yok). Okuma/kNN sonraki dilim (semantik arama / benzer V2 / taste).

🟥 Hotspot (closed: 025): backfill = **in-process resumable worker** (ImportJobWorker deseni, offline
   script değil — embedding süreklilik ister, yeni import'larda da). Depolama = **ayrı `product_embedding`
   tablosu** (Marten jsonb'ye dokunma; kNN zaten ham-SQL). Tekrar-embed tetiği = **yalnız "eksik"
   (worker)**; `ProductChangedEvent` sonraya (import INSERT-ONLY → değişim nadir; hash hazır). Boyut =
   **1536 sabit** (MRL kapalı, sonra ölç). Zayıf-açıklama 422 = **yine embed** (başlık+etiketten; hiç
   komşusuzdan iyi — blob boş-açıklamada Başlık+taksonomiden kurulur).

## Semantik arama (026 Dilim-2 — as-built)

> Embedding omurgası (025) hazır → ilk tüketici: anlam-temelli arama. Rol-ayrımı: **semantik=vektör**
> (betimleyici/kavramsal sorgu), lexical=BM25 (tam eşleşme/yazar/ISBN) AYRI — RRF füzyon YOK. Betimleyici
> sorgu ("umut veren hafif bir yaz romanı") BM25'te 0 döner; kNN anlamla eşler. Yüzey yalnız ScoutAgent.

SS1. 🟦 **Semantik ara** ⇦ ScoutAgent — betimleyici sorgu → OpenAI embed (`EmbeddingClientFactory`,       `(SemanticSearch.Tool,`
   025 omurga modeli) → `product_embedding` kNN (`<=>` kosinüs) + eşik (`MaxCosineDistance` 0.68 altı) →   ` SemanticSearchQueries)`
   mesafe artan sırala → künye (Marten, sıra korunur). Ham-SQL. Eşik dışı boş (alakasız doldurma yok).
SS2. 🟪 **Yapısal pre-filter kNN'e** — stokta/indirimli/fiyat/sayfa jsonb predicate (`mt_doc_product`        `(SemanticSearchQueries`
   JOIN; filtre=uygunluk, vektör=yakınlık). Yayınevi/yazar YOK (demir → lexical). BM25 metin KARIŞMAZ.      ` .AppendFilters, SemanticFilter)`
SS3. 🟦 **Ayrı yüzey** `semantic_search` MCP tool (yalnız ScoutAgent); `search_books`/REST `/products/       `(SemanticSearch.Tool)`
   search` lexical kalır, DOKUNULMAZ. Query + count + yapısal filtre param'ları.
SS4. 🟧 **Kullanıcı aradı** ⇨ Personalization — login + dolu-sorguda `UserSearched(mode="semantic")`        `(SemanticSearch.Tool)`
   yayınlanır (fanout; search_books deseni). Ayrık + hata-yutar; anonim yayınlamaz.

🟥 Hotspot (closed: 026): yüzey = **ayrı `semantic_search` tool** (rol-ayrımı; search_books lexical kalır).
   Routing = **ScoutAgent `/extract`** betimleyici → `SemanticQuery` (LLM); demir yoksa semantik
   (`SemanticIntent` guard; Text.Query sızıntısı katlanır). Sıra = **yapısal pre-filter → kNN** (067).
   Eşik = **0.68** (Options, canlı kalibre). Sorgu-embed cache = **sonra** (backlog; her arama 1 OpenAI çağrısı).

## similar_books V2 (027 Dilim-3 — as-built)

> V1 (021) benzerliği etiket-BM25 örtüşmesiyle kuruyordu (ortak token şart → etiket paylaşmayan anlam-
> yakınlarını kaçırır). Embedding omurgası (025) üstüne V2: benzerlik ÇAPA KİTABIN VEKTÖRÜNE kNN (anlam;
> ortak etiket şart değil). Etiket-BM25 + boost SÖKÜLDÜ; hard filtre + WorkKey tekilleştirme + stok-önce korunur.

SV1. 🟦 **Benzer bul V2** ⇦ ScoutAgent — çapa productId → `product_embedding` vektörü (anchor CTE) →          `(SimilarBooks.Tool,`
   kNN (`e.embedding <=> a.embedding` kosinüs); kendi WorkKey baskıları hariç + seviye uyum grubu +         ` SimilarBooksQueries)`
   (varsa) dil hard filtre → `DISTINCT ON WorkKey` (çapaya en yakın baskı) → stok-önce→yakınlık→popülerlik
   → LIMIT. Çapa embed'i yoksa anchor CTE boş → boş sonuç. Eşik YOK (çapa-temelli hep anlamlı komşu).

🟥 Hotspot (closed: 027): saf **vektör kNN** (boost YOK — vektör tür/ton/konuyu kodluyor; aynı-yazar hafif
   boost gerekirse sonra, ölçümle). Eşik **yok** (top-K + stok-önce yeter). V1 etiket-BM25 (`Overlap`/
   fallback/boost) + `SimilarBooksOptions` boost katsayıları **söküldü** (tüm ürün embed'li; embed'siz-çapa
   boş döner). Çapasız-fallback bilinçli kaldırıldı.

## Domain kuralları (süreci yöneten değişmezler)

- **ISBN = kimlik (095).** Ürün Gtin'iyle doğar ve bulunur (`Gtin` unique-index — DB-seviyesi guard); Sku yok.
- **Keşif ekseni = kapalı-sözlük taksonomi (021 — kategori ekseni SÖKÜLDÜ).** `Genre`/`Moods`/`AudienceLevel`/`Themes` değerleri `BookTaxonomy` sözlüğünden; yazma anında kanonikleşir (`Product.SetEnrichment` — geçersiz düşer/"belirsiz"), okuma anında facet. Serbest string üretimi imkansız.
- **Eser kimliği (021).** `WorkKey` = normalize(sıralı yazarlar)+parantezsiz başlık `(WorkKeyNormalizer.Derive)`; aynı eserin baskıları TEK anahtara iner. İç alan — kullanıcı/LLM yüzeyine çıkmaz; benzerlik tekilleştirmesinin temeli.
- **Fiyat opsiyonel, atanırsa >0 (001 — 097 revizesi).** Ürün fiyatsız doğabilir; fiyat sonradan atanır ve serbest değişir, her değer >0 `(Product.Create, Product.SetPrice)`. Fiyatı geri "fiyatsız"a çeviren yol yok; fiyat geçmişi (058) sökük kalır.
- **Satışa-uygunluk TEK tanım (001).** Satılabilir = fiyat>0 ∧ stok>0 ∧ ≥1 yazar `(Product.IsSalable)`; stok ayağı Stock dönene dek değerlendirme dışı. Kural hiçbir yüzeye kopyalanmaz.
- **Ürün var = keşfedilebilir (091 + 001 nüansı).** Taslak/yayın/gizleme yok; her ürün doğduğu an katalogda görünür — görünür ≠ satın alınabilir (IsSalable ayrı).
- **Silme yok (016 sürer).** `ProductChangedEvent.IsDeleted` kontratta durur, hep `false` gider.
- **Toplu giriş var olana dokunmaz (091 ilkesi sürer).** Yalnız EKSİK ISBN eklenir; admin düzenlemesi ezilmez (upsert bilinçli reddedildi).
- **Dış dünyaya iki yol = event.** `ProductChangedEvent` (her değişim; 021: kategori alanları sökük, tüketicisi ŞU AN yok, 097), `ProductAdded` (yalnız doğumda → File kapak + Stock seed; `InStock` ikili sinyali taşır).

## Sınır (bu BC'nin dokunmadığı)

Arama Catalog içi (011 pg-hibrit) — başka BC'ye senkron çağrı yapmaz. Reco/kişiselleştirme, zevk-vektörü,
gösterim logu/CTR (ayrı dilim). İndirim/fiyatlandırma motoru, sipariş, ödeme, stok, PDF teslimi yok.
