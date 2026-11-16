# Quickstart: Hibrit Arama Doğrulama

## Ön koşullar

- AppHost postgres image = `paradedb/paradedb:<ver>-pg17` (pg_search + pgvector).
- catalogDb dolu (010 sonrası: 22.5k ürün + açıklama + etiket).
- ParadeDB eklentileri + BM25 index + product_embedding tablosu Catalog açılışında kurulmuş (ExtendedSchemaObjects).
- US2 için: offline araç embedding üretip ingest edilmiş.

## Birim test (test-first — İlke VI)

```bash
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj
```
Kapsam: `RrfFusionTests` (sıra-tabanlı birleştirme, k, elenmeme, iki-liste toplamı), arama param validasyonu (boş sorgu, limit guard).

## Canlı doğrulama (acceptance ↔ test)

| Senaryo | Adım | Beklenen |
|---|---|---|
| **US1** BM25 | `GET /search?q=Orwell` | Orwell kitapları ilk sıralarda (SC-001) |
| **US1** nadir terim | `q=distopya` | tema kitapları üstte; bir-kez-geçen uzun açıklamalar altta |
| **US1** Türkçe | `q=istanbul` vs `q=İstanbul` | aynı sonuç (İ/ı — implement doğrulaması) |
| **US1** boş | `q=` | boş sonuç, hata yok |
| **US2** benzer | ünlü kitabın "benzer"i | komşular tema/tür uyumlu (SC-003) |
| **US2** anlam | `q=yalnızlığı anlatan sıcak roman` | tematik uygun kitaplar (kelime örtüşmese de) |
| **US3** hibrit | `q=Orwell karanlık gelecek` | ad (BM25) + tema (kNN) birleşir; tek-ayaktan iyi (SC-004) |

## DB spot-check

```sql
-- BM25 index var mı + çalışıyor mu
SELECT id, paradedb.score(id) FROM <schema>.mt_doc_product
  WHERE (data->>'Name') @@@ 'orwell' ORDER BY 2 DESC LIMIT 5;
-- embedding kapsamı
SELECT count(*) FROM <schema>.product_embedding;
-- kNN benzer (bir product_id'nin komşuları)
SELECT product_id, embedding <=> (SELECT embedding FROM product_embedding WHERE product_id=:id) d
  FROM product_embedding ORDER BY d LIMIT 5;
```

## Doğrulama notları (implement FLAG'leri)

- ParadeDB kesin image tag + `USING bm25` vs `USING paradedb` yazımı sürüme göre teyit.
- Türkçe İ/ı: ICU tokenizer yeterli mi, yoksa sorgu-öncesi normalize gerek mi — küçük örnekle doğrula.
- "Anlamca ara" sorgu-vektörü arama anında OpenAI'den (ürün vektörü offline) — latency/cache gözlemle.

## Kapsam dışı

Reco/kişiselleştirme, zevk-vektörü, gösterim logu/CTR — ayrı dilim(ler).
