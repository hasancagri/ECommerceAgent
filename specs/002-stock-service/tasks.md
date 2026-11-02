# Tasks: Stock.Api — Envanter BC'sinin Dönüşü

**Input**: spec.md + plan.md · Domain-TDD: test task'ları implementasyondan ÖNCE.

## Phase 1: Setup (iskelet)

- [X] T001 `src/services/stock/Stock.Api` projesi (Library.Api iskeletinden: csproj sürümsüz paketler, GlobalUsings, launchSettings portlu, Program.cs + Extensions/MartenExtensions + MessagingExtensions, appsettings) + `tests/Stock.Api.Tests` + ikisi `ECommerceAgent.slnx`'e.
- [X] T002 AppHost kaydı: `stockDb` Postgres + stock servisi (`WithReference` + health check); `SchemaConstants`'a Stock şeması.

## Phase 2: US1 — Stok kaydı domain'i (P1) 🎯 MVP

- [X] T003 [US1] Test-first `tests/Stock.Api.Tests/StockItemTests.cs`: `Create_ValidQuantity_SetsOnHand`, `Create_NegativeQuantity_ReturnsError`, `Create_EmptyProductId_ReturnsError`, `SetQuantity_Valid_ReplacesOnHand`, `SetQuantity_Negative_ReturnsErrorAndKeepsCurrent`, `Decrease_SufficientStock_ReducesOnHand`, `Decrease_Insufficient_ReturnsErrorAndKeepsCurrent`, `Decrease_NonPositiveAmount_ReturnsError`.
- [X] T004 [US1] Implement `src/services/stock/Stock.Api/Domains/StockItems/StockItem.cs` (AggregateRoot; ProductId + OnHand; Create/SetQuantity/Decrease, ResultDomain) + `Constants/StockResourceConstants.cs`; MartenExtensions'ta ProductId computed unique index. T003 YEŞİL.

## Phase 3: US2 — StockChanged kontratı + Catalog kopyası (P2)

- [X] T005 [US2] Kontrat: `Shared/IntegrationEvents.cs`'e `StockChanged(Guid ProductId, int OnHand)` + `Shared/RabbitMqConstants.cs`'e `StockChanged` (exchange `stock.changed`, queue `catalog.stock-changed`); Stock MessagingExtensions exchange deklare eder.
- [X] T006 [US2] Test-first `tests/Catalog.Api.Tests/ProductTests.cs`: IsSalable stok bacağı — `IsSalable_WithoutStock_ReturnsFalse`, `IsSalable_PricedAuthoredAndStocked_ReturnsTrue`, `SetStockCount_Negative_ReturnsError`, `SetStockCount_Zero_ClosesSalability`; mevcut IsSalable testleri stoklu beklentiye uyarlanır (KIRMIZI).
- [X] T007 [US2] Implement Catalog: `Product.StockCount` + `SetStockCount` + `IsSalable`'a `StockCount > 0`; kökte `StockConsumers.cs` (StockChanged → LoadAsync → SetStockCount → Save; ürün yoksa log-geç) + binding (Catalog MessagingExtensions) + `opts.Discovery.IncludeType(typeof(StockConsumers))`. T006 YEŞİL.

## Phase 4: Polish

- [X] T008 `src/services/stock/FLOW.md` (lejant + süreç: stok atanır/düşer → 🟧 StockChanged ⇨ Catalog; kurallar: adet ≥0, tek yazar Stock, rezervasyon yok) + csproj linked-file; Catalog FLOW.md'ye ⇦ Stock dikişi + IsSalable stok bacağı as-built; CLAUDE.md BC haritasına stock satırı + catalog satırında stok-kopyası.
- [X] T009 Tam doğrulama: `dotnet build && dotnet test` tüm çözüm yeşil.

## Dependencies

Setup → US1 → US2 → Polish (US2 kontrat Stock iskeletine, Catalog testleri US1 semantiğine bağlı değil ama sıralı gider — tek dal).