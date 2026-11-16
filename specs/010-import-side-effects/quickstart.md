# Quickstart: Import Yan-Etkileri Doğrulama

## Ön koşullar

- Aspire AppHost ayakta; catalogDb + stockDb + discountDb temiz (taze re-import).
- `_v3_enriched.xlsx` (`İndirim %` kesir dolu, ~%37).
- Gateway admin import ucu (X-Admin-Api-Key).

## Birim test (test-first — İlke VI)

```bash
dotnet test tests/Discount.Api.Tests/Discount.Api.Tests.csproj   # Campaign açık-uçlu + IsActiveAt null-end
dotnet test tests/Stock.Api.Tests/Stock.Api.Tests.csproj         # StockItem.Create (mevcut)
```

Kapsam: `CampaignTests` — `Create_OpenEnded_IsActiveWithNullEnd`, `IsActiveAt_NullEnd_TrueAfterStart`, `Create_RateAboveMax_ReturnsError`, `Create_WindowedEndBeforeStart_ReturnsError` (geriye-uyum).

## Canlı doğrulama (acceptance ↔ test)

| Senaryo | Adım | Beklenen |
|---|---|---|
| **US1** stok→satılabilir | `_v3_enriched.xlsx` import (dry-run→confirm) | Her ürün için 1 StockItem (30-50); Catalog ürün StockCount dolu; fiyatlı+yazarlı kitap IsSalable=true |
| **US1** tek kayıt | import 2. kez | Yeni StockItem AÇILMAZ (idempotent) |
| **US2** indirim ithal | import sonrası indirimli ISBN | Discount'ta açık-uçlu Campaign; Catalog ürün DiscountRate = Excel kesri |
| **US2** indirimsiz | indirim%=0 ISBN | Campaign yok; DiscountRate boş |
| **SC-001** | re-import sonrası sayım | fiyatlı+yazarlı kitapların ≥%99'u IsSalable |

## DB spot-check

```sql
-- Stock: ürün başına tek kayıt + 30-50 aralığı
select count(*), min((data->>'OnHand')::int), max((data->>'OnHand')::int) from <stock-schema>.mt_doc_stockitem;
-- Discount: açık-uçlu kampanya (EndsAt null)
select count(*) from <discount-schema>.mt_doc_campaign where data->>'EndsAt' is null;
-- Catalog: satılabilir sayısı
select count(*) from catalogmanagement.mt_doc_product
  where (data->>'StockCount')::int > 0 and data->'Price' is not null and jsonb_array_length(data->'AuthorIds'->'$values') > 0;
```

## Kapsam dışı (çalıştırılmaz)

CampaignWindowProcess (zaman-pencereli yürürlük), checkout stok düşümü, admin stok/indirim ekranları, kapak (File zaten kurulu).
