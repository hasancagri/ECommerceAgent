# Data Model: Import Yan-Etkileri

Yeni DB tablosu YOK. Mevcut aggregate'ler + 1 yeni integration event.

## Integration Event: ProductDiscountImported (YENİ)

`Shared/IntegrationEvents.cs`. Catalog → Discount.

| Alan | Tip | Kural |
|---|---|---|
| `ProductId` | Guid | Yeni yazılan ürünün kimliği (ISBN değil — create'ten gelir) |
| `Rate` | decimal | İndirim oranı (kesir, 0<rate≤0.90). Catalog yalnız bu aralıkta yayınlar; Discount da guard'lar |

RabbitMqConstants: `Exchange="catalog.product-discount-imported"`, `Queues.Discount="discount.product-discount-imported"`.
`ProductAdded.Queues.Stock="stock.product-added"` eklenir (Stock bu kuyruğu Catalog'un ProductAdded exchange'ine bağlar).

## Aggregate: Campaign (EVRİLİR — açık-uçlu)

| Üye | Değişim | Kural |
|---|---|---|
| `EndsAt` | `DateTimeOffset` → `DateTimeOffset?` | null = açık-uçlu (süresiz aktif) |
| `Create(productId, rate, startsAt, endsAt?)` | endsAt opsiyonel | endsAt doluysa `>startsAt`; null ise pencere-guard atlanır. Rate `0<rate≤0.90` KORUNUR; ProductId boş reddedilir |
| `IsActiveAt(now)` | null-end eklenir | `now>=StartsAt ∧ (EndsAt is null ∨ now<EndsAt) ∧ !Cancelled` |

Geriye-uyum: dolu endsAt davranışı AYNEN; yalnız null yeni yol. Cancel/CampaignSelection değişmez.

## Aggregate: StockItem (MEVCUT, kullanılır)

`Create(productId, onHand)` — değişmez. Consumer `onHand=Random(30..50)` ile çağırır; guard OnHand≥0 + ProductId zorunlu zaten var. Ürün başına tek kayıt (DB unique + consumer idempotent guard).

## Consumer: Stock.CatalogConsumers (YENİ)

`ProductAdded` → ProductId için StockItem var mı? (varsa atla) → `StockItem.Create(productId, Random(30..50))` → store → `StockChanged(productId, onHand)` publish.

## Consumer: Discount.CatalogConsumers (YENİ)

`ProductDiscountImported` → `Campaign.Create(productId, rate, now, null)` (açık-uçlu); başarılıysa store + `ProductDiscountChanged(productId, rate)` publish. Rate guard'ı Create verir (geçersiz → kampanya yok, event yutulmaz-mı? geçersiz oran Catalog'da filtrelendiği için normalde gelmez; gelirse Create reddi loglanır/atlanır).

## Catalog tarafı (yayıncı + mapper)

| Öğe | Değişim |
|---|---|
| `ImportRow.DiscountRate` | `decimal?` — "İndirim %" (kesir, dönüşüm yok) |
| `ImportJobWorker` | yeni ürün + rate∈(0,0.90] → batch'e `ProductDiscountImported` → post-commit publish |
| `Catalog MessagingExtensions` | `ProductDiscountImported` exchange declare + PublishMessage |
