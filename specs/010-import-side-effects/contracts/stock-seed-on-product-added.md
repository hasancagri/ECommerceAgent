# Contract: ProductAdded → Stock Seed

## Event: ProductAdded(Gtin, ProductId, CoverSourceUrl) — MEVCUT

- **Yayıncı**: Catalog (ürün doğumunda; değişmez).
- **Yeni tüketici**: Stock `CatalogConsumers` (File'a ek — aynı fanout exchange).
- Exchange `catalog.product-added` → yeni Queue `stock.product-added` (binding tüketici kurar).

## Stock tüketim davranışı

```
Handle(ProductAdded evt):
  if StockItem(ProductId=evt.ProductId) zaten var:  return   // idempotent (re-delivery)
  var qty = Random.Shared.Next(30, 51)                        // 30-50 dahil, altyapı
  var s = StockItem.Create(evt.ProductId, qty)
  if s.IsSuccess:
     store(s.Data)
     publish StockChanged(evt.ProductId, qty)                 // Catalog stok kopyası → IsSalable
```

- `StockChanged` → Catalog `StockConsumers` → `Product.SetStockCount` (ZATEN VAR).
- Random **consumer'da** (altyapı); `StockItem.Create(productId, onHand)` deterministik (İlke VI test-first dışı).

## Davranış tablosu

| Senaryo | Sonuç |
|---|---|
| Yeni ürün doğar | StockItem(30-50) açılır + StockChanged yayılır |
| Aynı ProductId tekrar (re-delivery) | atlanır (mükerrer kayıt yok) |
| fiyatsız ürün | stok yine açılır; IsSalable yine false (fiyat ayağı eksik) |

## Wolverine kayıt (tuzak)

`Stock MessagingExtensions`:
- `DeclareExchange(ProductAdded.Exchange)` + `BindQueue(ProductAdded.Queues.Stock)` + `ListenToRabbitQueue(...Stock)`
- `opts.Discovery.IncludeType(typeof(CatalogConsumers))` — çoğul suffix convention'a uymaz, ZORUNLU.
