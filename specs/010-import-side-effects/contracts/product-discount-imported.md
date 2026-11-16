# Contract: ProductDiscountImported + Discount Tüketimi

## Event: ProductDiscountImported(ProductId: Guid, Rate: decimal)

- **Yayıncı**: Catalog `ImportJobWorker` (post-commit, yalnız YENİ ürün + rate∈(0, 0.90]).
- **Tüketici**: Discount `CatalogConsumers`.
- Exchange `catalog.product-discount-imported` → Queue `discount.product-discount-imported` (binding tüketici kurar).

## Catalog yayın kuralı

| İndirim% (Excel, kesir) | Yayın |
|---|---|
| `0.285` | `ProductDiscountImported(productId, 0.285)` |
| `0` / boş | yayın YOK |
| `> 0.90` | yayın YOK (aralık dışı) |
| mevcut ISBN (INSERT-ONLY atlandı) | yayın YOK |

## Discount tüketim davranışı

```
Handle(ProductDiscountImported evt):
  var c = Campaign.Create(evt.ProductId, evt.Rate, now, endsAt: null)   // açık-uçlu
  if c.IsSuccess:
     store(c.Data)
     publish ProductDiscountChanged(evt.ProductId, evt.Rate)   // hemen aktif → Catalog kopyası dolar
  else:
     // geçersiz oran (normalde Catalog filtreler) — kampanya yok, iş durmaz
```

- `now` = consumer'da `DateTimeOffset.UtcNow` (domain saat okumaz; dışarıdan verilir).
- `ProductDiscountChanged` → Catalog `DiscountConsumers` → `Product.SetDiscountRate` (ZATEN VAR).

## Campaign.Create açık-uçlu sözleşmesi

| Girdi | Sonuç |
|---|---|
| `(pid, 0.285, now, null)` | Ok — açık-uçlu, `IsActiveAt(now)=true` |
| `(pid, 0.285, now, now+1g)` | Ok — pencereli (geriye-uyum) |
| `(pid, 0.95, now, null)` | Error (rate >0.90) |
| `(pid, 0, now, null)` | Error (rate ≤0) |
| `(Guid.Empty, 0.285, now, null)` | Error (ProductId boş) |
| `(pid, 0.285, now, now)` | Error (endsAt dolu ∧ ≤startsAt) |

`IsActiveAt`: `now>=StartsAt ∧ (EndsAt null ∨ now<EndsAt) ∧ !Cancelled`.
