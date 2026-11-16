# Contract: Embedding Üretimi + Ingest (US2)

## Offline araç (repo-dışı, 008 deseni)

`~/dev/book-crawl/embed_books.py`:
- Girdi: kitap metni = `{ad} {yazar} {açıklama} {etiketler}` (enriched Excel / Catalog export).
- OpenAI `text-embedding-3-small` → 1536-boyut vektör.
- ISBN-anahtarlı resumable JSON cache (atomik tmp+replace); yalnız yeni/eksik ISBN işlenir.
- Çıktı: `{isbn: [float×1536]}` (JSON/jsonl). Anahtar env'den (log'a yazma — İlke VIII).

## Tablo + HNSW (ExtendedSchemaObjects)

```sql
CREATE EXTENSION IF NOT EXISTS vector;
CREATE TABLE IF NOT EXISTS <schema>.product_embedding (
  product_id uuid PRIMARY KEY, embedding vector(1536) );
CREATE INDEX IF NOT EXISTS product_embedding_hnsw
  ON <schema>.product_embedding USING hnsw (embedding vector_cosine_ops);
```

## Ingest (Catalog admin ucu)

- Girdi: batch `[{isbn, vector[1536]}]`.
- Her kayıt: Gtin=isbn → Product bul → `product_id`; `INSERT ... ON CONFLICT (product_id) DO UPDATE SET embedding=...`.
- Vektör boyutu ≠1536 → o kayıt reddedilir (iş durmaz); bilinmeyen ISBN atlanır.
- Yüzey: admin-anahtar korumalı (import deseni) ya da offline araç çıktısını okuyan iş.

## kNN sorgu (benzer/anlam)

| Mod | SQL özü |
|---|---|
| Benzer kitaplar (ürün→komşu) | ürünün vektörü `v` → `ORDER BY embedding <=> v LIMIT n` (kendini ele) |
| Anlamca ara (metin→kitap) | sorgu metni OpenAI ile anlık vektöre → `ORDER BY embedding <=> $qvec LIMIT n` |

- "Anlamca ara" sorgu-vektörü **arama anında** OpenAI'den (ürün embedding'i offline; sorgu embedding'i canlı-tek). Maliyet/latency kabul (tek sorgu); cache'lenebilir (backlog).
- Vektörü olmayan ürün sonuçta yok; hata yok.
