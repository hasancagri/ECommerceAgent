# Storefront — Domain Süreci (EventStorming)

**BC ne yapar:** Catalog+Stock+Reviews+Discount'tan akan **şişman event'leri** ürün-anahtarlı bir
**event-log'a** (Postgres, gerçek-kaynak) biriktirir; **async projeksiyonla** her ürünü katlayıp
**Elasticsearch** arama dokümanı üretir; vitrini asistana **tek ES-DSL sorgu kapısından** sunar.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> 🟩 read-model · 🩷 dış sistem · 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu
> backtick `(…)` = koda köprü, süreç değil. Süreç değişince (yeni/silinen adım-event-policy)
> bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟧 **Kaynak event gelir** ⇦ Catalog ⇦ Stock ⇦ Reviews ⇦ Discount — dört kaynak            `(storefront.events`
   TEK sıralı kuyruğa bağlanır → stream yarışı yok (086 CQRS+ES girişi).                       ` → Sequential)`
2. 🟪 **Event gelince stream'e eklenir** → 🟦 **Ürün stream'ine append et** —                 `(CatalogConsumers`
   consumer yalnız durable inbox'a yazar (fold/ES değil); gerçek-kaynak = Postgres             ` → Events.Append)`
   event-log. Dört kaynak dört ayrı consumer, aynı stream.
3. 🟪 **Projeksiyon daemon'u uyanır** → 🟩 **Etkilenen ürün taze katlanır** —                 `(StorefrontProjection`
   async; her batch'te ProductId'ler stream'den katlanır — saf fold.                           ` → AggregateStreamAsync)`
4. 🟩 **Katlanan doc'ta alan grupları birleşir** — her kaynak YALNIZ kendi                    `(StorefrontDocument.ApplyCatalog`
   alanını yazar; kısmi satır geçerli; mutlak değer son-yazan-kazanır.                         `/ApplyStock/ApplyReviewSummary`
                                                                                               `/ApplyDiscount)`
5. 🟪 **Satılabilirse yaz, değilse sil** → 🩷 **ES dokümanı yazılır/silinir** —               `(StorefrontProjection`
   IsDeleted ya da fiyatsız (Catalog raporlamadı) → ES'ten silinir; satılamaz                  ` → IsSellable)`
   kitap index'te HİÇ bulunmaz (projeksiyon-zamanı dışlama, FR-008).
6. 🟩 **Etkin fiyat yazım-anında hesaplanır** — indirim penceresi içindeyse                   `(EffectivePrice)`
   indirimli, değilse liste fiyatı — doc alanına yazılır.
7. 🟪 **Açıklama değişince anlamsal temsil tazelenir** → 🩷 **embedding üretilir** —          `(DecideEmbedding`
   karar saf: boş → yok, değişti/eksik → üret, aynı+var → koru (önceki vektör                  ` → embedding dense_vector)`
   ES'teki doc'tan okunur). Embedding ES dense_vector alanı.
8. 👤 **Asistan sorar** → 🟦 **Tek ES-DSL kapısından sorgula** — asistanın yazdığı            `(QueryStorefront`
   ham ES Query DSL önce minimal rail'den geçer (sabit index + size≤50 + _source               ` → AgentQueryLog)`
   whitelist + timeout), `{{EMBED}}` metni sistemce vektöre çevrilir (knn), sorgu
   çalışır; ret dahil her çağrı iz bırakır. Text+fuzzy+kNN+filtre tek sorguda;
   eşik-altı/boş sonuç = "bulunamadı". Keşif envanteri Catalog'dadır.
9. 🟪 **Açılışta index garanti edilir** → 🩷 **ES index kurulur** — yoksa mapping'le          `(StorefrontSearchIndex`
   kurulur (idempotent). Soğuk başlangıç + yeniden-kurulum = dev full-reset +                  ` → EnsureAsync)`
   kaynak BC republish (R8, event-log'u yeniden doldurur). Elle reindex
   (sil+kur+rebuild) BACKLOG.

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
