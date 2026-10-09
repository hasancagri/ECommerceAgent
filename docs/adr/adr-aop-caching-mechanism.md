---
status: Kabul
---

# ADR: AOP Query Caching mekanizması — neden IMessageBus decorator?

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + `specs/002-aop-query-caching`.
> Cache kuralları (kim boşaltır, yazma-yolu yasağı, kardinalite) `../conventions.md` "Cache" bölümünde.
> İlgili: [adr-cache-vs-readmodel](adr-cache-vs-readmodel.md), [adr-bounded-context-per-service](adr-bounded-context-per-service.md).

**Durum:** Kabul edildi (yürürlükte) · **Feature:** 002-aop-query-caching

## Kısıt (pazarlık edilemez)

Caching **declarative bir cross-cutting aspect** olmalı: query/command **handler gövdesine 0 satır**
cache kodu (FR-007 / anayasa III / SC-005). Etkinleştirme tek bir bildirimsel işaretle: query
record'unun üstünde `[Cached("tag", ttl)]`, yazma record'unda `[InvalidatesCache("tag")]`.

## Değerlendirilen yollar

### 1) Wolverine middleware (Before/After) — ELENDI ❌

İlk sezgi: `ScopeAuthorizationMiddleware` gibi attribute-tetikli bir Wolverine middleware. Ama
**ampirik olarak** (scratch app, üretilen kod incelendi) şu kanıtlandı:

- `Before` middleware short-circuit'te (`HandlerContinuation.Stop`) **değer döndüremiyor** —
  üretilen kod düz `return;` yapıyor, middleware'in ürettiği değer atılıyor. `InvokeAsync<T>`
  sonucu `default(T)` oluyor.
- HybridCache'in tek okuma API'si `GetOrCreateAsync(factory)` — factory içinde handler'ı çağırmak
  gerekir; Before/After handler'ı **saramaz**. Yani middleware yolu iki koldan da çıkmaz.

### 2) Wolverine custom Frame (codegen) — REDDEDİLDİ (kırılgan) ⚠️

Handler çağrısını üretilen kodda `GetOrCreateAsync` ile saran bir `IChainPolicy`+`Frame` teknik
olarak mümkün. Ama generic-response tipi + blok-sarma codegen'i ileri seviye, Wolverine sürümüne
duyarlı, birim-test'i zor. Fayda/risk dengesi kötü.

### 3) Transparan IMessageBus decorator — SEÇİLDİ ✅

Wolverine'in `IMessageBus`'ını Scrutor `Decorate<IMessageBus, CachingMessageBus>()` ile sarıyoruz.
Sadece `InvokeAsync<T>` iki overload'unda cache/invalidation; kalan ~17 üye tek satır forward.

- **Endpoint'ler DE handler'lar DA değişmez** — decorator şeffaf. `bus.InvokeAsync<R>(query)` aynı.
- Cache mantığı **tek yerde** (decorator), attribute'lar davranışı sürer (FR-008: attribute'u
  kaldır → cache yok, kod değişmeden).
- **Stampede + iki katman + tag-invalidation HybridCache'ten native** gelir (elle kilit yok).
- Kırılgan codegen yok; düz C#. Uçtan uca DB'siz kanıtlandı: 2. okuma cache'ten, 100 eşzamanlı
  soğuk istekte kaynak 1 kez, RemoveByTagAsync iki katmanı boşaltıyor.

## Neden Scrutor

Decorator desenini DI'da kurmak için: Wolverine'in kaydettiği `IMessageBus` descriptor'ını bulup
`CachingMessageBus` ile sarar (`services.Decorate<...>()`). Elle descriptor değiştirmeye göre temiz.
Not: `Decorate` çağrısı `UseWolverine`'den SONRA olmalı (IMessageBus kaydı önce gelsin) — scratch'te
doğrulandı.

## Sonuçlar / bedel

- **Artı:** handler + endpoint el değmemiş; gerçek AOP; sağlam; HybridCache özellikleri bedava.
- **Bedel:** decorator IMessageBus'ın tüm üyelerini (~19) implemente etmek zorunda → ~17 satır
  boilerplate forward (mantık yok). Kabul edildi (tek satırlık, güvenli).
- Middleware short-circuit-değer sınırı ileride başka cross-cutting ihtiyaçları da etkiler
  (validation vb. için aynı ders geçerli).

## Evrim (rollout + backplane)

Mekanizmaya dokunan değişiklikler:

### Tag'ler de BC-prefix'li oldu
Anahtarlar prefix'liydi, tag'ler HAMDI — paylaşımlı Redis'te iki BC aynı tag adını kullansa
birbirinin girdisini boşaltırdı. Decorator artık yazımda ve boşaltmada `{prefix}:{tag}` kullanır.

### Anahtar hash'ine FullName girdi
`Queries/` ve `Agent/` slice'ları aynı kısa adlı query record'ları taşıyabiliyor (ör. iki ayrı
`GetAddressesQuery`). `type.Name` tek başına aynı anahtara düşürüp farklı Response tiplerini
çarpıştırırdı. `Fnv1a64(type.FullName + json)` — anahtar formatı görünürde aynı, çakışma imkânsız.

### CacheMetrics silindi
Hit/miss/invalidation sayaçları + OTel meter kaydı söküldü (kullanıcı kararı, YAGNI). Ölçüm
gerekirse `redis-cli INFO stats`.

### CacheInvalidator + Redis pub/sub backplane
Boşaltma tek kapıya indi: `CacheInvalidator.InvalidateAsync(tag)` = yerel L1+L2 temizliği +
`cache-inv:{prefix}` kanalına yayın. Her instance'taki `CacheBackplaneSubscriber` kanalı dinler,
kendi L1'ini düşürür. Çok-instance L1 tutarlılığı böylece ~ms'e iner; pub/sub at-most-once olduğu
için **≤5sn L1 TTL güvenlik ağı olarak kalır** (kaldırma!). Elle boşaltma HER ZAMAN
`CacheInvalidator` üzerinden — doğrudan `RemoveByTagAsync` backplane'e yayılmaz.

### Kapsam bugünkü hali
Altyapı 8 serviste. (Tarihsel: "Supplier.Gateway hariç" kaydı — Supplier.Gateway 041'de söküldü.) Tüketiciler:
**Customer** (addresses/cards, `[InvalidatesCache]`) + **Storefront** (`filters`; boşaltma
ProductChangedEvent handler'ında `CacheInvalidator` ile — projeksiyon-BC kuralının ilk örneği).
Catalog'un `[Cached]`/`[InvalidatesCache]`'leri REST GET yüzeyiyle birlikte silindi (ürün okuma
tek kapı: Storefront). Karar kuralları `../conventions.md` "Cache" bölümünde (kim boşaltır,
yazma-yolu yasağı, kardinalite, kaba-tag tercihi).

### MediatR paraleli (öğrenme)
`CachingMessageBus` ≈ MediatR `IPipelineBehavior` (around-interceptor; `innerCall` ≈ `next`).
Fark: MediatR hook'u birinci-sınıf verir; Wolverine middleware short-circuit'te değer
döndüremediğinden (yukarıdaki kanıt) desen bus decorator'ında elle kuruldu — forward boilerplate
bunun bedeli. Kapsam da dar: yalnız `InvokeAsync<T>`; saga/kuyruk/cascade decorator'ı görmez.
