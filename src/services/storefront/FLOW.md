# Storefront — Domain Süreci

**BC ne yapar:** Catalog+Stock+Reviews+Discount'tan akan **şişman event'leri** ürün-anahtarlı bir
**event-log'a** (Postgres, gerçek-kaynak) biriktirir; **async projeksiyonla** her ürünü katlayıp
**Elasticsearch** arama dokümanı üretir; vitrini asistana **tek ES-DSL sorgu kapısından** sunar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Dört kaynak event'i TEK sıralı kuyruğa akar.** Catalog, Stock,     `(storefront.events`
   Reviews, Discount aynı kuyruğa bağlanır → stream yarışı yok.          ` → Sequential)`
2. **Her event ürün stream'ine append edilir.** Consumer yalnız          `(CatalogConsumers`
   durable inbox'a yazar (fold/ES değil); gerçek-kaynak = Postgres        ` → Events.Append)`
   event-log. Dört kaynak dört ayrı consumer, aynı stream.
3. **Async projeksiyon etkilenen ürünleri taze katlar.** Daemon           `(StorefrontProjection`
   her batch'te ProductId'leri stream'den katlar — saf fold.              ` → AggregateStreamAsync)`
4. **Katlanan doc alan grupları birleşir.** Her kaynak YALNIZ            `(StorefrontDocument.ApplyCatalog`
   kendi alanını yazar; kısmi satır geçerli; mutlak değer                 `/ApplyStock/ApplyReviewSummary`
   son-yazan-kazanır.                                                     `/ApplyDiscount)`
5. **Satılabilir doc ES'e yazılır, değilse silinir.** IsDeleted ya        `(StorefrontProjection`
   da fiyatsız (Catalog raporlamadı) → ES'ten silinir; satılamaz          ` → IsSellable)`
   kitap index'te HİÇ bulunmaz (projeksiyon-zamanı dışlama).
6. **Etkin fiyat yazım-anında hesaplanır.** İndirim penceresi            `(EffectivePrice)`
   içindeyse indirimli, değilse liste fiyatı — doc alanına yazılır.
7. **Açıklama değişince anlamsal temsil tazelenir.** Karar saf: boş       `(DecideEmbedding`
   → yok, değişti/eksik → üret, aynı+var → koru (önceki vektör            ` → embedding dense_vector)`
   ES'teki doc'tan okunur). Embedding ES dense_vector alanı.
8. **Asistan sorusu TEK ES-DSL kapısından yanıtlanır.** Asistanın        `(QueryStorefront`
   yazdığı ham ES Query DSL önce minimal rail'den geçer (sabit            ` → AgentQueryLog)`
   index + size≤50 + _source whitelist + timeout), `{{EMBED}}`
   metni sistemce vektöre çevrilir (knn), sorgu çalışır; ret dahil
   her çağrı iz bırakır. Text+fuzzy+kNN+filtre tek sorguda; eşik-
   altı/boş sonuç = "bulunamadı". Keşif envanteri Catalog'dadır.
9. **Arama index'i açılışta garanti edilir.** ES index yoksa            `(StorefrontSearchIndex`
   mapping'le kurulur (idempotent). Soğuk başlangıç + yeniden-kurulum     ` → EnsureAsync)`
   = dev full-reset + kaynak BC republish (R8, event-log'u yeniden
   doldurur). Elle reindex (sil+kur+rebuild) BACKLOG.

## Domain kuralları (süreci yöneten değişmezler)

- **Gerçek-kaynak = Postgres event-log; ES türetilmiş.** ES her zaman log'tan yeniden kurulabilir; kaynak BC'ye uzanmadan rebuild olur (İLKE I).
- **Rich aggregate DEĞİL.** `StorefrontDocument` invariant taşımaz; dört kaynağın ürün-anahtarlı katlanmış birleşimi (read-model/projeksiyon istisnası).
- **Kısmi satır geçerli.** Her kaynak yalnız kendi alanını yazar; `Price`/`Name` yok = "Catalog raporlamadı" → satılamaz (ES'e yazılmaz).
- **Push-only, geri-çekiş YOK.** Yalnız şişman event tüketir; hiçbir kaynağa dış çağrı yapmaz.
- **Sıra = append order; idempotency = mutlak değer.** Tek kuyruk + Sequential stream sırasını korur; tekrar teslim aynı değer → doc değişmez (optimistic-concurrency retry yok, append-only).
- **Satılabilirlik projeksiyon-zamanı.** Satılamaz/silinmiş kitap ES'te HİÇ yok — LLM filtresi unutsa da sızmaz (çözüm-zamanı filtre değil).
- **Etkin fiyat yazım-anında türetilir.** İndirim penceresi event'siz biterse bir sonraki event'e dek bayat kalabilir (vitrin yaklaşık/bayat-toleranslı; kesin satış kararı checkout'ta).
- **Sorgu kapısı salt-okur + iz bırakır.** Ham ES DSL çalışır; minimal rail (sabit index + size tavan + _source whitelist + timeout) ucuz felaketi önler; ret dahil her sorgu `AgentQueryLog`'a geçer. Tam JSON guard ayrı iş.
- **Alakasızlık/boşluk dürüstlük kuralıdır.** Boş/eşik-altı sonuç "bulunamadı"dır; uydurma yok.

## Sınır (bu BC'nin dokunmadığı)

Ürün yazımı/CRUD, fiyatlandırma, sepet, sipariş yok. Kişisel satın-alma kaydı (UserPurchase) **Library
BC'ye taşındı** — sorgu yüzeyi satın-almayı görmez. Keşif envanteri (kategori/yazar/yayınevi) Catalog'da.