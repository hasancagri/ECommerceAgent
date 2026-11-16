# Data Model: Hibrit Arama

## Schema objesi: BM25 index (US1 — yeni tablo YOK)

`mt_doc_product` (Marten'in ürün tablosu) üstünde custom `ISchemaObject` → raw DDL:
- `CREATE INDEX IF NOT EXISTS product_bm25_idx ON <schema>.mt_doc_product USING bm25 (id, (data->>'Name'), (data->>'Description'), (data->'Tags'...)) WITH (...)` (ICU tokenizer + ascii_folding).
- Etiketler: `Product.Tags` = `BookTag[]` JSONB (`data->'Tags'->'$values'`) → metin olarak indekslenir (ad+açıklama+etiket tek arama alanı mantığı).
- MVCC otomatik bakım: Product INSERT/UPDATE/DELETE → index transaction içinde güncel.

## Tablo: product_embedding (US2 — YENİ)

Custom `ISchemaObject` (Marten'e kayıtlı değil; `ExtendedSchemaObjects`):

| Kolon | Tip | Kural |
|---|---|---|
| `product_id` | uuid (PK) | Catalog Product.Id'ye karşılık; upsert anahtarı |
| `embedding` | `vector(1536)` | `text-embedding-3-small` çıktısı |

- HNSW index: `CREATE INDEX ... ON product_embedding USING hnsw (embedding vector_cosine_ops)`.
- Upsert: ingest batch {isbn→vector}; Gtin→product_id çözülür (Product lookup); `ON CONFLICT (product_id) DO UPDATE`.
- Vektörü olmayan ürün satırı yok → semantik sonuçta yer almaz (FR-008 kısmi-kapsam güvenli).

## Aggregate: Product (opsiyonel — ingest yolu)

Vektör ayrı tabloda; Product aggregate'i vektör TUTMAZ (doc şişmesin). İngest `product_embedding`'e doğrudan yazar (Catalog içi admin uç/SQL). `Product.SetEmbedding` GEREKMEZ (karar: ayrı tablo). — Not: ileride vektör Product'a taşınırsa davranış eklenir.

## Arama sorguları (salt-okur, raw SQL)

| Sorgu | Girdi | SQL özü | Çıktı |
|---|---|---|---|
| **BM25** (US1) | metin + limit | `WHERE ... @@@ $q ORDER BY paradedb.score(id) DESC LIMIT n` | sıralı productId + skor |
| **kNN** (US2) | sorgu-vektör / ürün-vektör + limit | `ORDER BY embedding <=> $v LIMIT n` (kosinüs) | sıralı productId + mesafe |
| **Hibrit** (US3) | metin + sorgu-vektör | iki top-N + RRF birleştir | sıralı productId + rrf skor |

Sonuç productId listesi → Catalog ürün künyesine çözülür (aynı BC, Marten lookup; sıra KORUNUR).

## Saf birim: RRF füzyon

`RrfFusion.Combine(rankedListA, rankedListB, k=60)` → `skor(id)=Σ 1/(k+sıra)`; birleşik sıralı liste. Test-first (saf, host'suz).

## Validasyon

- Boş/whitespace sorgu → boş sonuç (hata değil, FR-003).
- limit guard (≤ makul üst sınır, ör. 100); negatif/0 → default.
- Sorgu-vektör boyutu 1536 değilse ingest/sorgu reddi.
