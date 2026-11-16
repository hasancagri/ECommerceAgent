# Contract: BM25 Keyword Arama (US1)

## Index (ExtendedSchemaObjects custom ISchemaObject)

```sql
CREATE INDEX IF NOT EXISTS product_bm25_idx
ON <catalog-schema>.mt_doc_product
USING bm25 (id, (data->>'Name'), (data->>'Description'), (data->'Tags'))
WITH (...);   -- ICU tokenizer + ascii_folding; kesin WITH söz dizimi implement'te ParadeDB sürümüne göre
```
- Idempotent; `ApplyAllDatabaseChangesOnStartup` kurar; Marten düşürmez.
- Extension ön koşul: `pg_search` (ExtendedSchemaObjects `Extension("pg_search")`).

## Sorgu

- Girdi: `query` (metin), `limit` (default 20, ≤100), `offset`.
- `... WHERE <alanlar> @@@ :query ORDER BY paradedb.score(id) DESC LIMIT :limit OFFSET :offset`
- Çıktı: sıralı `[{productId, score}]` → Catalog künyesine çözülür (sıra korunur).

## Davranış tablosu

| Sorgu | Beklenen |
|---|---|
| `"Orwell"` | Orwell kitapları üst sıralarda (yazar adı yüksek alaka) |
| `"distopya"` (nadir) | temayı taşıyan kitaplar, bir-kez-geçen uzun açıklamalardan önce |
| `""` / `"   "` | boş sonuç (hata YOK) |
| `"İstanbul"` vs `"istanbul"` | aynı sonuç (ICU + ascii_folding; İ/ı implement-doğrulama) |

## Değişmezler

- Yeni tablo YOK (mt_doc_product index'i); MVCC otomatik bakım (Product değişince index güncel).
- Anonim erişim (keşif login istemez).
