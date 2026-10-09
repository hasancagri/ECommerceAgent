---
status: Superseded
---

# ADR/Deep-Dive: SearchStorefrontProducts — hibrit aramanın ince işçiliği (019)

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + `specs/019-hybrid-product-search/`
> (özellikle `research.md` R1-R8). Güncel ikame: `query_storefront` tool (069→086 ES-DSL) bu slice'ı tam ikame eder.
> İlgili: [adr-cache-vs-readmodel](adr-cache-vs-readmodel.md).

> ⚠️ **SUPERSEDED (2026-09-08, 069):** Bu notun anlattığı slice ve iki-yol tasarımı TAM İKAME ile
> SÖKÜLDÜ. Evrim: 019 hibrit slice → 067 revizesi (embedding AYRI sidecar doküman + kNN tek
> yardımcıda, eşik 0.68) → **069 tek serbest-sorgu kapısı `query_storefront`** (PR #97): LLM
> salt-okur `storefront_sellable` VIEW'ına SQL yazar; `SearchStorefrontProductsForAgent` +
> `FindSimilarBooksForAgent` + REST `/search` silindi. (086: VIEW/SQL de söküldü, LLM artık ham ES
> Query DSL yazar.) Güncel gerçek: kod + `../../CLAUDE.md` storefront satırı. Aşağısı 019 döneminin
> TARİHSEL kaydıdır — satır numaraları ve dosya yolu artık geçersiz.

**Durum:** SÖKÜLDÜ (069 tam ikame, 086 ES-DSL) · **Feature:** 019-hybrid-product-search (PR #30, merge `ed40458`)

## Büyük resim: tek slice, iki yürütme yolu

Handler `SearchText` var/yok ayrımıyla iki tamamen farklı yürütme stratejisi seçer (satır 144-146):

| | Filtre-yalnız yol (`SearchByFiltersAsync`) | Anlamsal/hibrit yol (`SearchBySimilarityAsync`) |
|---|---|---|
| Motor | Marten LINQ + saf in-memory çekirdek | Ham SQL (NpgsqlCommand) |
| Sıralama | `Name ASC` — **deterministik** (FR-008) | Kosinüs mesafesi `<=>` — anlamsal (FR-006) |
| Filtreler | `FilterAndOrder` saf fonksiyonu | SQL WHERE (hard, DB tarafında) (FR-007) |
| Dış bağımlılık | Yok | OpenAI embedding çağrısı (1 kez, sorgu için) |

Neden ikiye ayrıldı: iki yolun *doğruluk gereksinimleri* farklı. Anlamsal yolda top-K + eşik
**DB'de** uygulanmak zorunda (önce limit sonra filtre = eksik sonuç hatası — research R3'te
`VectorSearchAsync` bu yüzden reddedildi). Filtre yolunda ise böyle bir zorunluluk yok; test
edilebilir saf çekirdek daha değerli.

## İnce işçilik noktaları (satır referanslı)

### 1. Saf, test edilebilir çekirdekler (31-79, 83-108)

`Validate` ve `FilterAndOrder` statik + IO'suz: 23 birim testin tamamı DB/host olmadan koşuyor
(anayasa: saf domain birim testi). Handler yalnız orkestrasyon yapar. `NormalizeMaxResults`
(27-28) tek yerde: null→8, 1..20'ye `Math.Clamp` — endpoint, MCP ve SQL LIMIT aynı değeri görür.

### 2. Doğrulama Result pattern'iyle, exception'sız (31-79)

"En az bir kriter" (FR-003) önce kontrol edilir ve **erken döner** — kriter yokken aralık
hatalarını da yığmak anlamsız. `query is { MinPrice: >= 0, MaxPrice: >= 0 }` deseni (64):
negatif değerler zaten INVALID_VALUE aldıysa aynı istekte bir de INVALID_RANGE üretmemek için
aralık kıyası yalnız iki uç da geçerliyken yapılır. Kodlar resource sabiti (serbest metin yasak).

### 3. Marka OR — case-insensitive, iki yolda aynı semantik (88-95 ↔ 191-199)

In-memory: `Trim().ToLowerInvariant()` + HashSet. SQL: `lower(d.data->>'Brand') = any(@brands)`
ve parametre dizisi **C# tarafında** aynı normalize işleminden geçer. Normalizasyon tek dilde
(C#) yapılıp SQL'e hazır veri gönderilir; iki yol arasında davranış sapması olmaz.

### 4. `StockQuantity` null semantiği: "bilinmiyor" satılabilir sayılır ama MinStock'ta elenir

In-memory `x.StockQuantity >= query.MinStock` (102): null >= n → false. SQL'de
`(d.data->>'StockQuantity')::int >= @minStock` (216): `->>` null döner, null karşılaştırması
false. **İki yol bilerek aynı**: stok filtresi istendiyse stoğu bilinmeyen ürünü göstermek
oversell duruşuna aykırı olurdu (bkz. fail-closed ilkesi, 012).

### 5. Ham SQL neden ve nasıl güvenli (164-246)

- **Neden ham SQL:** Marten LINQ vektör `ORDER BY` üretemez; paketin `VectorSearchAsync`'i
  WHERE alamaz (R3). Repository/abstraction eklemek yerine framework çağrısı çıplak (kullanıcı
  tercihi: dolaylama yerine düz kod).
- **Sorgu vektörü TEXT parametre + server-side cast** (220-221, 236): `[f1,f2,...]` literal'i
  `cast(@queryVector as vector(1536))` ile cast edilir. `Pgvector.Vector`'ı binary parametre
  bağlamak Npgsql pg_type cache yarışına açık (R3) — text+cast, Marten.PgVector'ın kendi
  kullandığı bağışık desen. `CultureInfo.InvariantCulture` şart: TR locale'de `0,5` yazar, SQL patlar.
- **Filtreler koşullu string + HER değer parametre** (188-218): SQL metnine yalnız sabit
  parçalar eklenir; kullanıcı girdisi asla interpolate edilmez → injection yüzeyi yok.
- **Subquery + dış WHERE** (226-246): `distance` alias'ı WHERE'de kullanılamaz (SQL kuralı);
  iç select hesaplar, dış katman `distance <= @maxDistance` + `order by` + `limit` uygular.
  Mesafe ifadesi tek yerde yazılır, tekrarlanmaz.
- **INNER JOIN = US2-S3 kuralının kendisi** (238): embedding'i olmayan ürün anlamsal sonuca
  *yapısal olarak* giremez — if'le değil, join türüyle garanti.

### 6. Şema adı tuzağı: Postgres identifier folding (237-238)

`SchemaConstants.StorefrontSchemaName` = `"storefrontManagement"` ama Marten şemayı
**küçük harfle** (`storefrontmanagement`) yaratır. SQL'de ad **tırnaksız** interpolate edildiği
için Postgres onu da küçük harfe katlar → doğru eşleşir. ⚠️ Biri "düzeltme" diye çift tırnak
eklerse (`"storefrontManagement"`) sorgu kırılır. Bilinçli tırnaksız.

### 7. Marten session'ının bağlantısını ödünç alma (248-252)

Yeni NpgsqlConnection açmak yerine `session.Connection` kullanılır: aynı scope, havuz baskısı
yok, `UsePgVector()`'ın data source'a kaydettiği vector tipi hazır. Null guard + gerekirse
`OpenAsync` — Marten bağlantıyı tembel açar, garanti değil.

### 8. Dayanıklılık: embedding hatası = hata Result, çökme değil (172-186)

`GenerateVectorAsync` tek try/catch'te; `OperationCanceledException` **bilerek dışarıda**
(iptal iptal gibi davranmalı, hata Result'ına yutulmamalı). Hata kodu servise özel sabit:
`STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE`. Filtre-yalnız yol bu try'a hiç girmez → SC-005
(anlamsal altyapı çökükken filtreli arama %100) yapısal olarak sağlanır.

### 9. Eşik 0.7 = taban filtresi, hassasiyet mekanizması değil (16-17)

Kosinüs **mesafesi** ≤ 0.7 (benzerlik ≥ ~0.3). text-embedding-3-small skorları ada-002'den
düşük; eşik yalnız bariz alakasızı ("uzay mekiği contası" → boş) keser. Canlıda görüldü:
yakın kategori (tişört, "şort" sorgusunda) eşiği geçebiliyor — sıralama yine doğru; gerekirse
sabit sıkılaştırılır. Top-K altında taban filtre yaklaşımı: arXiv 2408.04887 (R6).

### 10. Boş sonuç = NotFound, otomatik (161, 274)

`FeatureListResultModel.Ok(boş liste)` kendiliğinden NotFound üretir — handler'da if yok.
Endpoint yalnız `IsSuccess` → 200/400 çevirir; MCP tool ince sarmalayıcı, Agent slice'ının
kendi query'sini `IMessageBus` ile çağırır (slice self-contained; iş mantığı sıfır).

## Bilinçli ödünler

- Filtre-yalnız yol tüm satılabilir satırları çekip bellekte filtreler (153-159) — facet
  sorgusuyla aynı desen; binler ölçeğinde sorun değil, test edilebilirlik kazandırır.
  Büyürse: fiyat/stok filtreleri Marten LINQ'e itilir, marka OR yine bellekte kalabilir.
- HNSW index YOK (R2): cast-scan binler için yeterli; büyüyünce expression index eklenir.
- Embedding kapsamı olaya bağlı: yalnız feed'den (yeniden) geçen ürünün embedding'i olur.
  Tam kapsam istenirse supplier snapshot temizliği + feed tetikleme yeter (Docker reseti değil).
