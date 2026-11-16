# Contract: Hibrit RRF Füzyon (US3)

## Girdi

- `query` (metin) + opsiyonel sorgu-vektör (metinden anlık üretilir).
- İki aday listesi: BM25 top-N (US1) + kNN top-N (US2), ör. N=100.

## RRF birleştirme (saf, test edilebilir)

```
RrfFusion.Combine(listA, listB, k=60):
  skor(id) = Σ  1 / (k + sıra_in_list)     (her listedeki konum)
  → skora göre azalan sırala, tekilleştir (id bir kez)
```

- Sıra-tabanlı → BM25 skoru (sınırsız) + kosinüs (0-1) **normalize edilmez**.
- Bir listede olmayan id o listeden katkı almaz ama **elenmez** (öteki listeden skorlanır).
- k=60 default; parametrik (FR-011 — füzyon takılabilir).

## Davranış tablosu

| Sorgu | Beklenen |
|---|---|
| `"Orwell karanlık gelecek"` (ad+tema) | Orwell kitapları (BM25) + distopik eserler (kNN) üstte birleşir |
| iki listede de üstte olan kitap | hibrit sonuçta en üstlerde (iki konum toplanır) |
| yalnız bir listede olan kitap | elenmez; tek-liste katkısıyla sıralanır |

## SQL vs app-katmanı

İki alternatif (implement seçer): (a) tek SQL CTE'de iki `ROW_NUMBER()` + RRF toplamı; (b) iki ayrı sorgu → app'te `RrfFusion.Combine`. (b) saf fonksiyon test-first'e uygun; (a) tek round-trip. Füzyon soyutlanır.

## Çıktı

Sıralı `[{productId, rrfScore}]` → Catalog künyesine çözülür (sıra korunur, sayfalanır).
