# Research: Hibrit Arama

## 1. Vektör saklama — ayrı `product_embedding` tablosu (Marten bump YOK)

- **Decision**: Vektör ayrı tabloda (`product_embedding`: productId + `vector(1536)`), raw SQL custom `ISchemaObject` + HNSW index. Marten `Product` doc'una gömülmez.
- **Rationale**: Marten native pgvector (embedding ON doc) **Marten.PgVector 9.36+** ister; biz 9.5'teyiz → solution-geneli (8 servis) bump riski bir arama feature'ı için orantısız. Ayrıca 1536 float × 22.5k Product JSONB'yi şişirir (her ürün okumasında taşınır). Ayrı tablo: Marten'e dokunmaz, doc ince kalır, hem BM25 hem vektör uniform raw-SQL/ParadeDB olur.
- **Alternatives**: Marten.PgVector fluent `VectorIndex<T>` + embedding on doc — temiz ama bump riski + doc şişmesi, reddedildi. Vektörü Product JSONB'de float[] olarak Marten.PgVector'suz tutmak — gerçek `vector` kolonu/index olmaz, kNN yavaş/imkânsız, reddedildi.

## 2. BM25 index — mt_doc_product JSONB üstünde custom ISchemaObject

- **Decision**: `CREATE INDEX ... USING bm25 (id, (data->>'Name'), (data->>'Description'), ...) WITH (...)` — Marten'in `mt_doc_product` tablosunun JSONB alanları üstünde. Idempotent (`IF NOT EXISTS`) `ISchemaObject` → `opts.Storage.ExtendedSchemaObjects.Add(...)`; `ApplyAllDatabaseChangesOnStartup` kurar, Marten düşürmez.
- **Rationale**: pg_search JSON/expression index destekler → ayrı materyalize metin kolonu GEREKMEZ (US1 yeni tablo yok). Index **MVCC-entegre**: INSERT/UPDATE/DELETE transaction içinde otomatik güncellenir → Marten yazımları index'i bedava senkron tutar, reindex işi yok.
- **Sorgu**: `@@@` eşleşme + `paradedb.score(id)` (yeni alias `pdb.score`) ile BM25 skoru; skora göre `ORDER BY ... DESC`. `key_field` deprecated (yeni index'te gereksiz, geri-uyum için kabul).
- **Alternatives**: Native FTS (`tsvector`+`ts_rank`) — BM25 değil, ranking kaba, kullanıcı BM25 istedi; reddedildi. Ayrı materyalize arama tablosu — gereksiz kopya, reddedildi.

## 3. ParadeDB image

- **Decision**: AppHost postgres `pgvector/pgvector:pg17` → `paradedb/paradedb:<ver>-pg17`. pg_search + pgvector (+ PostGIS/pg_cron/pg_ivm) birlikte gelir. Eklenti per-DB: catalogDb'de `CREATE EXTENSION pg_search` + `vector` (ExtendedSchemaObjects `Extension(...)`).
- **Rationale**: Tek image iki eklentiyi verir; postgres üst-kümesi → basket/order/… etkilenmez (onlar eklentiyi kurmaz). Volume zaten sıfır (fresh).
- **FLAG (implement doğrula)**: Kesin sürüm tag'i Docker Hub'dan sabitlenir (`latest-pg17`'ye güvenme). `USING bm25` vs yeni `USING paradedb` yazımı sürüme göre teyit edilir.

## 4. Türkçe tokenizer — ICU + ascii_folding (İ/ı FLAG)

- **Decision**: pg_search alanlarında **ICU tokenizer** (`::pdb.icu`) + lowercasing + `ascii_folding=true`. Türkçe snowball stemmer yok; aksan-katlama + ICU ile idare.
- **Rationale**: Built-in Türkçe dil config YOK; ICU Unicode/locale-duyarlı, default'tan iyi. `InvariantGlobalization` (010 kuralı) + `NameNormalization` zemini uyumlu.
- **FLAG (implement doğrula — bilinen tr-TR tuzağı)**: Generic ASCII lowercase İ/ı'yı yanlış katlar; ICU'nun per-alan Türkçe LOCALE ayarı sunup sunmadığı belgeden NET DEĞİL → implement'te küçük örnekle ("İstanbul"/"istanbul"/"ISTANBUL") doğrula; gerekirse sorgu-öncesi `NameNormalization`/manuel fold uygula (BookTag AsciiVariant deseni).

## 5. Offline embedding üretimi + ingest

- **Decision**: `~/dev/book-crawl/embed_books.py` (008 deseni) — OpenAI `text-embedding-3-small`, girdi metni = ad+açıklama+etiket (009 çıktısı/enriched Excel ya da Catalog export), ISBN-anahtarlı resumable cache, çıktı `{isbn: [float×1536]}`. Catalog **admin ingest ucu** batch {isbn, vector} alır → `product_embedding`'e upsert (Gtin→productId çözer). Vektör büyük → Excel'e sığmaz, ayrı JSON/ingest yolu.
- **Rationale**: 008 deseni kanıtlı (resumable, ucuz, bir-kez); ingest Catalog içinden → BC izolasyonu (tool DB'ye doğrudan yazmaz, uç üstünden). INSERT-ONLY uyumlu: yeni ürün için tool tekrar koşulur (cache atlar).
- **Model/boyut**: `text-embedding-3-small` = 1536 boyut; ucuz (~$0.02/1M token, 22.5k kitap birkaç cent). `3-large` (3072) gerekmez (maliyet/boyut; small yeter).

## 6. RRF füzyon

- **Decision**: BM25 ve kNN iki sorgusu ayrı çalışır (her biri top-N, ör. 100), app-katmanında ya da tek SQL CTE'de **RRF**: `skor = Σ 1/(k + sıra)`, k=60. Sıra-tabanlı → normalize yok. Füzyon ayrı birim (test edilebilir saf fonksiyon) + takılabilir (FR-011).
- **Rationale**: BM25 skoru (sınırsız) + kosinüs (0-1) ölçek uyumsuzluğunu sıra aşar. k=60 sektör-default. Saf RRF hesabı test-first.
- **Alternatives**: Ağırlıklı normalize toplam — kırılgan; öğrenilmiş rerank — ağır (ileride). Füzyon soyutlanır.

## Çözülen NEEDS CLARIFICATION

- Vektör nerede → ayrı `product_embedding` (Marten bump YOK, §1).
- BM25 yeni tablo mu → HAYIR, `mt_doc_product` üstünde index (§2).
- Türkçe → ICU+ascii_folding, İ/ı implement-doğrulama (§4).
- Embedding üretim/ingest → offline OpenAI araç + admin ingest ucu (§5).
