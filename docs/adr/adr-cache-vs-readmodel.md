---
status: Kabul
---

# ADR: Cache mi, Read Model mi?

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + `.specify/memory/constitution.md`.
> Somut kararlar `specs/002-aop-query-caching` (cache) ve `specs/003-storefront-read-model` (read model).
> İlgili: [adr-bounded-context-per-service](adr-bounded-context-per-service.md).

**Durum:** Kabul edildi (yürürlükte)

## Bağlam

İki mekanizma da "okumayı hızlandırır" ama farklı problemleri çözer. Karıştırılınca ya yanlış yerde
cache (kırılgan invalidation, cross-context sızıntı) ya da gereksiz read model (fazla mühendislik) çıkar.
Bu not sınırı sabitler.

## Temel ayrım: kaynak (source of truth) kim?

- **Cache** = bir sorgu sonucunun **geçici, atılabilir kopyası**. Kaynak hâlâ aggregate/DB. Cache'i silsen
  sistem doğru çalışır, sadece yavaşlar. Şekil = sorgunun şekli; dönüşüm/birleştirme yok.
- **Read Model** = belirli bir görünüm için **kalıcı, amaca-özel projeksiyon**. Genelde birden çok
  aggregate/context'i birleştirir, event'lerle beslenir, kendi (eventual) tutarlılığını yönetir. O görünümün
  kaynağı odur.

## Ayrımı yapan tek soru

> Sorgunun cevabı **tek aggregate + tek context** içinden mi geliyor, yoksa **birden çok context'i
> birleştirmen** mi gerekiyor?

| Durum | Araç |
|---|---|
| Tek aggregate, tek context, sorgu şekli = aggregate'in hâli (`GetProductById`, `GetAllProducts`) | **Cache** (002) |
| Product + Stock'u tek "vitrin" ekranında birleştir | **Read Model** (003) |
| Cross-context join gerekiyor (anayasada yasak) | **Read Model** — event'le materialize et |

Cross-context join BC izolasyonuyla yasak → [adr-bounded-context-per-service](adr-bounded-context-per-service.md). Birleşik görünümü *sorgu
anında* kuramazsın; onu **önceden** event'lerle read model'e yazarsın. 003'ün varlık sebebi budur.

## Aggregate içindeki elemanlar için cache?

**Alt-elemanları ayrı ayrı cache'leme.** Gerekçe:

- **Aggregate = atomik yükleme + tutarlılık sınırı.** Marten `Product`/`Basket`'i tek document olarak yükler;
  `BasketItem`'ları tek tek cache'lemek bu sınırı böler.
- **Invalidation kâbusu:** alt-eleman değişince "hangi parça girdilerini boşaltayım?" doğar. Aggregate bütün
  olarak değiştiği için doğal boşaltma taneliği de aggregate (ya da context).

Doğru kullanım: **tek aggregate'i (veya DTO projeksiyonunu) döndüren query sonucunu, aggregate/context
taneliğinde cache'le** — alt-entity seviyesinde değil. 002 tam bunu yapar: tüm Product/liste sonucu
cache'lenir, tek kaba `catalog-products` etiketi her Catalog yazmasında hepsini boşaltır.

## Somut örnek (kod)

- `GetProductById` → tek `Product` aggregate, Catalog context, sorgu = aggregate'in hâli → **Cache (002)**.
- "Vitrin ürün kartı" = Product.fiyat + Stock.adet bir arada → tek context'te yok, join
  yasak → **Read Model (003)**; `ProductChanged`/`StockChanged` event'leriyle beslenir.
  (003'te üçüncü kaynak Discount'tu; 018'de kalktı, 079'da geri geldi — sınır kuralı değişmedi.)

> **086 not:** Bu karar (birleşik vitrin = read model) GEÇERLİ; yalnız read-model'in **depolama+besleme
> tekniği** değişti — Postgres denormalize doc yerine **event-sourcing (Marten event-log) + async
> projection → Elasticsearch** doc. "Event'le önceden materialize et" ilkesi aynen korundu; cross-context
> join hâlâ yasak.

## Gri bölge (bilinçli sınır)

Teknik olarak birleşik sonucu da cache'e koyabilirsin. Ama cross-context birleşik sorguyu cache'lemek
kırılgan: (a) sorgu anında başka context'ten okuman gerekir (yasak), (b) invalidation'ı birden çok context'in
event'ine bağlaman gerekir. Read model bunu **veri sahipliği + eventual consistency** ile temiz çözer.

**Kural:** birleştirme gerekiyorsa **read model**; tekrar-okuma hızlandırması gerekiyorsa **cache**.

## Sonuçlar

- 002 kapsamı doğru: tek-aggregate Catalog okumaları, kaba tag, AOP declarative cache (IMessageBus decorator
  + HybridCache). Alt-eleman ya da cross-context cache YOK.
- Birleşik/vitrin okumaları 003'e (read model) ait; cache ile çözülmeye çalışılmaz.
