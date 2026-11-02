# Implementation Plan: Stock.Api — Envanter BC'sinin Dönüşü

**Input**: `specs/002-stock-service/spec.md` · **Tarih**: 2026-11-02

## Teknik yapı (mevcut desenlerin aynısı)

- **Yeni proje:** `src/services/stock/Stock.Api` (net10.0, Marten+Wolverine+ServiceDefaults — `Library.Api` iskeleti referans: en sade BC). `tests/Stock.Api.Tests`. İkisi `.slnx`'e.
- **DB:** `stockDb` — AppHost'ta Postgres database + servis kaydı (`WithReference`); `SchemaConstants`'a Stock şema adı.
- **Aggregate:** `Domains/StockItems/StockItem.cs` — `ProductId` (computed unique index, MartenExtensions deseni), `OnHand`. Davranışlar: `Create(productId, onHand≥0)`, `SetQuantity(≥0)`, `Decrease(>0, yeterlilik guard'ı)`. `Constants/StockResourceConstants.cs`.
- **Kontrat:** `Shared.IntegrationEvents.StockChanged(Guid ProductId, int OnHand)` + `RabbitMqConstants.StockChanged` (exchange `stock.changed`, queue `catalog.stock-changed`). Yayın noktası gelecekteki yazma handler'ları (bu dilimde yüzey yok — kontrat+topoloji kurulur, MessagingExtensions exchange'i deklare eder).
- **Catalog tarafı:** `Product.StockCount` (int, default 0) + `SetStockCount(≥0)` davranışı; `IsSalable`'a `StockCount > 0` bacağı; kökte `StockConsumers.cs` (kaynak=Stock; `StockChanged` → ürünü yükle→`SetStockCount`→kaydet; ürün yoksa logla-geç). **Wolverine tuzağı:** `*Consumers` taranmaz → `opts.Discovery.IncludeType(typeof(StockConsumers))` Catalog Program.cs'e. Binding'i tüketici kurar (Catalog MessagingExtensions).
- **FLOW.md:** `src/services/stock/FLOW.md` (yeni, lejantlı) + Catalog FLOW'una stok-kopyası dikişi (⇦ Stock) + csproj linked-file.
- **Test:** saf domain — `StockItemTests` (test-first) + Catalog `ProductTests` satışa-uygunluk stok bacağı güncellemesi. Consumer/Marten yolları canlı doğrulanır (Aspire).

## Riskler/notlar

- `IsSalable` stok bacağı aktifleşince mevcut IsSalable testlerinin beklentileri değişir (stoklu helper gerekir) — bilinçli kırılma, aynı dilimde düzelir.
- launchSettings TUZAĞI: yeni servis launchSettings'siz Production açılır — Library'ninki kopyalanıp port değiştirilir.
- Aspire AppHost kaydı olmadan servis ayağa kalkmaz — WithHttpHealthCheck dahil mevcut desen kopyalanır.