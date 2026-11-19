# Research — Unified Catalog Search (Phase 0)

Kaynak: spec.md + mevcut `Search/` kodu + ParadeDB/pg_search. Sürüm-duyarlı SQL sözdizimi **canlı doğrulanır** (kod yorumlarındaki FLAG deseni).

## D1 — Tek sorgu şekli: BM25 + yapısal filtre + sıralama tek SQL'de

**Decision**: Tek yürütücü tek SQL kurar: metin varsa `WHERE data @@@ $pdbQuery`, filtreler `WHERE` fast-field predicate'leri (AND), sıralama `ORDER BY` (metin→`pdb.score(id) DESC`; açık sort→fast-field; ikisi de yoksa `PurchaseCount DESC`), `LIMIT/OFFSET`. Sonuç `product_id` listesi → `ResolveOrderedAsync` (Marten, sıra korunur).

**Rationale**: pg_search BM25 index fast field'lerle `WHERE`/`ORDER BY` pushdown + Top-K yapar → aday kümesi belleğe yüklenmez (FR-005, SC-002). Tek SQL = tek yol (FR-001).

**Alternatives**: (a) iki ayrı sorgu + bellekte birleştir — bugünkü naive; reddedildi (ölçeklenmez). (b) RRF füzyon — semantik söküldüğü için gereksiz.

## D2 — Fast field'ler: filtre/sıralama alanları index'e

**Decision**: BM25 index `mt_doc_product (id, data)`'dan genişletilir; fast field olarak jsonb yolları eklenir: `Price.Amount` (decimal), `PageCount` (int), `PublishedOn` (date), `Binding` (int enum), `PublisherId` (uuid), `CategoryId` (uuid), `StockCount` (int), `DiscountRate` (decimal?), `PurchaseCount` (int). Metin alanları (`Name`, `Description`, `Tags.Value`) aranabilir text field.

**Rationale**: Filtre + sort bu alanlardan; fast field olmadan Top-K/pushdown çalışmaz (jsonb içinde gömülü kalırsa tam-tarama). FR-002/FR-004/FR-005.

**Alternatives**: Marten computed index + ayrı SQL — reddedildi (pg_search dışı, iki index yolu). **FLAG**: jsonb alt-alan fast field sözdizimi + tip dönüşümü pinned pg_search sürümünde canlı doğrulanır.

## D3 — Türkçe tokenizasyon (regresyon riski, kesme sırası)

**Decision**: Text field'lere Türkçe/ICU uyumlu tokenizer+normalizer (noktasız-ı katlama, lowercase) yapılandırılır. Bu, naive yol kaldırılmadan ÖNCE kurulur ve canlı doğrulanır (SC-004 sıfır regresyon).

**Rationale**: Bugünkü `ILIKE Contains` dotless-ı'yı literal eşler; BM25 default tokenizer Türkçe katlamıyor (kod FLAG'i). ICU'suz geçiş = regresyon. Tags ASCII-ikizi (009) köprüsü yardımcı ama Name/Description için yetersiz.

**Alternatives**: Default tokenizer + uygulama-katmanı fold — reddedildi (index eşleşmesini bozar). **FLAG**: `WITH (text_fields=...)` ICU config sözdizimi sürüme göre canlı doğrulanır.

## D4 — Kategori filtresi (facet değil)

**Decision**: `CategoryId` fast field; filtre tek leaf `CategoryId` eşleşmesi (opsiyonel `RelatedCategoryIds` içerme — v1'de yalnız `CategoryId`). Yazar-listeleme facet'i KAPSAM DIŞI.

**Rationale**: Clarify kararı — kategori FİLTRE dahil, facet ertelendi. Product tek-leaf `CategoryId` taşır (BC haritası).

**Alternatives**: facet/agregasyon — ertelendi (farklı sorgu şekli, yazar döner).

## D5 — Semantik altyapı söküm envanteri

**Decision**: Şunlar kaldırılır — `Search/Embedding/` (QueryEmbeddingClient, OpenAiEmbeddingOptions), `Search/RrfFusion.cs`, `ProductSearchQueries` {SemanticSql, SimilarSql, SemanticAsync, SimilarAsync, ToVectorLiteral}, `Features/Queries/HybridSearch.cs`, `Features/Queries/SimilarProducts.cs`, `Features/Commands/IngestEmbeddings.cs`, `SearchProducts` semantic mod, `ProductSearchSchema` {`product_embedding` tablosu, HNSW index, `CREATE EXTENSION vector`}. Endpoint: `/search?mode=semantic|hybrid`, `/{isbn}/similar`, `POST /api/admin/catalog/embeddings` düşer. Options/DI kayıtları (OpenAiEmbeddingOptions bind) + GlobalUsings temizlenir.

**Rationale**: Clarify — BM25 tek motor, semantik ayrı spec'e (096 deseni). Ölü kod/tablo/config bırakılmaz (SC-006).

**Alternatives**: dormant bırak — reddedildi (kullanıcı "sök" dedi; ölü-kod gürültüsü).

## D6 — İki yüzeyin yakınsaması + mod seçimi

**Decision**: REST `/search` ve MCP `search_books` aynı birleşik `SearchRequest` + executor'ı kullanır. `SearchBooks.cs` naive bellekte-filtre mantığı silinir, executor'a devreder (mevcut filtre parametreleri korunur). Mod seçimi (relevance vs field-sort vs default-popularity) saf karar fonksiyonu; niyet (NL→SearchRequest) ScoutAgent'ta kalır (FR-012).

**Rationale**: FR-001/FR-011 — tek yol, iki çağıran. Mod saf → İlke VI test-first.

**Alternatives**: iki ayrı executor — reddedildi (split devam ederdi).

## Canlı keşif — ÇÖZÜLDÜ (2026-11-18, Aspire + docker exec psql, catalogDb 22588 ürün)

- **Sürüm**: ParadeDB **0.26.6 / pg17**, `vector` 0.8.6. Şema adı **lowercase `catalogmanagement`** (Postgres unquoted fold).
- **Sorgu sözdizimi**: alan-nitelemeli ZORUNLU — `data @@@ 'Name:orwell'` (1), `data @@@ 'Description:aşk'` (1438), `CategoryId:"<uuid>"` (894) çalışır. Bare `data @@@ 'george'` → 0 (niteleme şart). AND/OR BÜYÜK harf. Skor: `pdb.score(id)` ve `paradedb.score(id)` ikisi de var.
- **KÖK SORUN (D2)**: default `bm25(id, data)` tüm json'u tek/text indeksliyor → numeric **range** (`PurchaseCount:[100 TO 200]`, `Price.Amount:[...]`) parse HATASI; nested `Price.Amount` hiç adreslenmez; `Binding:1` yalnız term-match (20387). `EXPLAIN ... ORDER BY (data->>'PurchaseCount')::int LIMIT` → **"not using Top K scan: ORDER BY columns cannot be pushed down"**.
- **ÇÖZÜM YOLU (ParadeDB'nin kendi remedy'si)**: "Numeric columns are fast by default. For string columns use `pdb.literal` tokenizer. Ensure ORDER BY columns are indexed." → Index typed-expression kolonlarıyla yeniden kurulur: numeric (Price.Amount::numeric, PageCount::int, PublishedOn::date, StockCount::int, DiscountRate::numeric, PurchaseCount::int) typed → fast/range/Top-K; exact-string (CategoryId, PublisherId, Binding) → `pdb.literal`; Türkçe text (Name, Description, Tags.Value) → ICU tokenizer. **AÇIK**: 0.26.6 çoklu-expression/field config tam DDL sözdizimi + "data defined more than once" (011 eski kısıtı) hâlâ geçerli mi — T003'te bir-iki iterasyonla netleşir.
- **Söküm güvenli**: `catalogmanagement.product_embedding` tablosu mevcut; `DROP TABLE IF EXISTS` + vector ext başka tüketicisi yok.

## KANITLANMIŞ sorgu reçetesi (0.26.6, canlı doğrulandı — T003/T011 temeli)

**Index DDL (çalışıyor, koda yazıldı):**
`CREATE INDEX product_bm25_idx ON catalogmanagement.mt_doc_product USING bm25 (id, data) WITH (json_fields='{"data":{"fast":true,"expand_dots":true}}');`
→ `paradedb.schema` yalnız `data` (JsonObject, fast) açar ama fast+expand_dots ile alt-alanlar range/term/sort için erişilir. Tek ParadeDB index kuralı (bir relation = bir bm25).

**Sorgu deseni — anchor `id @@@`, alan yolu `data.<path>` (tam önek ŞART):**
- Metin: `id @@@ 'data.Name:orwell'` (1), `data.Description:aşk` (1438). Bare `data @@@ 'george'`=0 (çalışmaz).
- Numeric range: `id @@@ 'data.PurchaseCount:[100 TO 200]'` (3908), nested `data.Price.Amount:[100 TO 300]` (5358). Builder: `paradedb.range('data.PurchaseCount', int4range(100,200,'[]'))`.
- Term (exact): `id @@@ 'data.CategoryId:"<uuid>"'` (894), `data.Binding:1`.
- Kombinasyon: `id @@@ 'data.Description:aşk AND data.Price.Amount:[100 TO 300] AND data.Binding:1'` (365). AND/OR BÜYÜK harf.
- Match-all (DefaultBrowse): `id @@@ paradedb.all()` (22588).
- Relevance Top-K: `ORDER BY paradedb.score(id) DESC LIMIT n` → Sort node YOK (pushdown ✓).
- Numeric/date sort: `ORDER BY (data->>'PurchaseCount')::int DESC` → Sort node (pushdown YOK). Filtered set'te kabul; DefaultBrowse-at-scale için backlog (duplicated typed kolon → Top-K). `data.X` SQL ORDER BY'da KULLANILAMAZ (FROM-clause hatası).

**Türkçe (T004) = PARİTE, regresyon değil:** ne default ne icu tokenizer İ/ı katlıyor (TARİH→"tari̇h", ı ayrı). Bugünkü ILIKE+InvariantGlobalization da katlamıyor → eşdeğer. Yaygın ASCII (`tarih`→"Tarih") çalışır. Kusursuz İ/ı folding = backlog (normalized search alanı + eşlemeli query fold; aggregate dokunur, ayrı iş).

## Veri şekli (fast-field yolları — doğrulandı)

Top-level jsonb: `Name`, `Description`, `Tags`, `Price` (nested `{Amount,Currency}` → `data->'Price'->>'Amount'`), `Binding`(int), `PublishedOn`("2026-03-11" date), `CategoryId`(uuid str), `PublisherId`, `StockCount`(int), `DiscountRate`(num), `PurchaseCount`(int), `AuthorIds`, `RelatedCategoryIds`.

## T010 kontrat kilidi — kalan FLAG'ler ÇÖZÜLDÜ (2026-11-19, canlı 22588 ürün)

Mevcut `bm25(id,data) WITH json_fields fast+expand_dots` index üstünde (T003 index değişmeden) tüm SearchRequest filtreleri doğrulandı:

- **bool/discount**: `data.IsSalable:true` (11049), `data.DiscountRate:>0` (11430) çalışır. OnlySalable/OnlyDiscounted bu POZİTİF term'lerle.
- **range açık uç**: `data.Price.Amount:>=100` / `:<=300` (açık) ve `[100 TO 300]` (kapalı) — ikisi de çalışır.
- **`NOT field:"x"` string query'de BOZUK** (exclude publisher = 0, yanlış sonuç). ExcludePublisher → **SQL predicate**: `... AND data->>'PublisherId' <> $n` (9403 ✓). Alternatif `paradedb.boolean(must_not=>...)` de 9403 ama SQL predicate daha sade.
- **`paradedb.term()` uuid'de 0** (tire tokenize eder) → uuid/exact HEP quoted phrase `CategoryId:"<uuid>"`.
- **Publisher ADI doc'ta YOK, yalnız `PublisherId`** (`data ? 'Publisher'`=false). Publisher/ExcludePublisher filtresi önce Catalog'da ad→PublisherId çözer (Marten), sonra `PublisherId` filtreler. SearchRequest.Publisher = ad string.
- **`paradedb.all()` string param OLAMAZ** (fonksiyon) → clause string boşsa SQL'de inline `@@@ paradedb.all()`, doluysa `@@@ $1` (iki şablon ya da koşullu birleştirme).

**SearchPredicateBuilder dönüş sözleşmesi (T010):** `(string? PdbClause, IReadOnlyList<string> SqlPredicates, IReadOnlyList<object> Parameters)`.
- `PdbClause` = pozitif term'ler `" AND "` ile (text OR-grupları + range + term + bool); null/boş → çağıran `paradedb.all()` kullanır.
- `SqlPredicates` = `@@@` dışı ek koşul (bugün yalnız ExcludePublisher `data->>'PublisherId' <> {param}`), `$n` sıralı param referansıyla.
- `Parameters` = pdb clause string ($1) + SQL predicate param'ları sıralı.

**SQL iskeleti (T011):**
```sql
SELECT id FROM catalogmanagement.mt_doc_product
WHERE id @@@ $1               -- clause; boşsa paradedb.all() (param'sız)
  [AND data->>'PublisherId' <> $2]   -- ExcludePublisher
ORDER BY <mode>              -- score DESC | (data->..)::type dir | (data->>'PurchaseCount')::int DESC
LIMIT $n OFFSET $m
```
