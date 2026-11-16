# Implementation Plan: Import Yan-Etkileri (Stok Tohumlama + İndirim İthali)

**Branch**: `010-import-side-effects` | **Date**: 2026-11-16 | **Spec**: [spec.md](./spec.md)

## Summary

Catalog Excel import'u ürün yazdıktan sonra iki broker yan-etkisi tetikler: (1) her yeni ürün için Stock `StockItem.Create(productId, rastgele 30-50)` → `StockChanged` → Catalog stok kopyası → kitap satılabilir; (2) indirimi olan ürün için Catalog `ProductDiscountImported(productId, rate)` yayınlar → Discount `Campaign.Create` açık-uçlu → `ProductDiscountChanged` → Catalog indirim kopyası. Üç BC entegrasyon event'leriyle bağlanır; senkron çağrı yok. Catalog'un stok/indirim kopya-tüketicileri ZATEN var; bu dilim yayıncı tarafını + Stock tüketicisini + Campaign açık-uçlu evrimini kurar.

## Technical Context

**Language/Version**: .NET 10 (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten (Postgres doc store, her BC kendi DB'si), Wolverine (in-proc + RabbitMQ fanout), xUnit + Shouldly

**Storage**: stockDb (`StockItem`), discountDb (`Campaign`), catalogDb (mevcut kopyalar). Yeni tablo: yok (mevcut aggregate'ler).

**Testing**: Saf unit test-first (İlke VI): `Campaign.Create` açık-uçlu + `IsActiveAt` null-end, `StockItem.Create`. Consumer'lar (random/infra) test-sonra; canlı Aspire re-import ile doğrulanır.

**Target Platform**: Aspire AppHost altında Catalog/Stock/Discount servisleri

**Project Type**: Mevcut 3 BC genişletme (yeni servis/DB yok)

**Performance Goals**: Import zaten arka plan; ürün başına 1-2 event ek — ihmal edilebilir. ~22.5k ürün × (1 stok + ~%37 indirim) event.

**Constraints**: Yalnız integration event (senkron yok); yalnız YENİ ürün (INSERT-ONLY); random stok consumer'da (aggregate deterministik); Wolverine `*Consumers` keşif tuzağı → IncludeType.

**Scale/Scope**: 3 BC, 1 yeni event, 1 aggregate evrimi, 2 yeni consumer, 1 mapper alanı.

## Constitution Check

*GATE: Phase 0 öncesi + Phase 1 sonrası.*

| İlke | Durum | Not |
|---|---|---|
| I BC İzolasyonu | GEÇER | Catalog↔Stock↔Discount yalnız event; her BC kendi DB'si; senkron çağrı YOK |
| II Zengin Aggregate | GEÇER | `Campaign`/`StockItem` davranışları; `Campaign` açık-uçlu evrim (EndsAt nullable + IsActiveAt), invariant korunur |
| III VSA/CQRS | GEÇER | Yan-etki handler'ları `Saga/` değil kaynak-adlı `CatalogConsumers` (kök, conventions kaynak-adı kuralı) |
| IV Result Pattern | GEÇER | `Campaign.Create`/`StockItem.Create` → `ResultDomain<T>` |
| V Scope | N/A | Dış yüzey yok; iç broker akışı |
| VI Domain-TDD | GEÇER | Campaign/StockItem saf davranış test-first; consumer (random/IO) test-sonra |
| VII FLOW.md | GEÇER | 3 FLOW.md'ye storm sticky eklendi (Catalog 4c/6, Discount 1b+hotspot, Stock 1b); implement'te as-built |
| VIII Sır | N/A | Hassas değer yok |
| Teknoloji (Wolverine/Marten) | GEÇER (tuzak) | `*Consumers` keşfi IncludeType ister; binding TÜKETİCİ kurar (soğuk-açılış) |

**Gate: PASS** — Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/010-import-side-effects/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/
│   ├── product-discount-imported.md   # yeni event sözleşmesi + Discount tüketim davranışı
│   └── stock-seed-on-product-added.md  # ProductAdded→StockItem seed sözleşmesi
└── tasks.md   # /speckit-tasks
```

### Source Code (repository root)

```text
src/others/Shared/
├── IntegrationEvents.cs          # + ProductDiscountImported(ProductId, Rate)
└── RabbitMqConstants.cs          # + ProductDiscountImported (Exchange+Queues.Discount); ProductAdded.Queues.Stock

src/services/catalog/Catalog.Api/
├── Helpers/ExcelRowMapper.cs     # + ImportRow.DiscountRate ("İndirim %" — ZATEN kesir, dönüşüm yok)
├── Process/ImportJobWorker.cs    # + indirim>0 için ProductDiscountImported batch event (post-commit)
└── Extensions/MessagingExtensions.cs  # + ProductDiscountImported publish (exchange+PublishMessage)

src/services/stock/Stock.Api/
├── CatalogConsumers.cs                 # YENİ: ProductAdded → StockItem.Create(random 30-50) → StockChanged publish
├── Extensions/MessagingExtensions.cs   # + ProductAdded exchange+queue+listen; IncludeType(CatalogConsumers)
└── (StockItem.Create mevcut)

src/services/discount/Discount.Api/
├── CatalogConsumers.cs                 # YENİ: ProductDiscountImported → Campaign.Create açık-uçlu → ProductDiscountChanged publish
├── Domains/Campaigns/Campaign.cs       # EndsAt nullable + IsActiveAt null-end + Create guard gevşetme
└── Extensions/MessagingExtensions.cs   # + ProductDiscountImported exchange+queue+listen; IncludeType(CatalogConsumers)

tests/
├── Discount.Api.Tests/CampaignTests.cs   # + açık-uçlu Create + IsActiveAt null-end (test-first)
└── Stock.Api.Tests/StockItemTests.cs     # Create (mevcut desen; random consumer'da, test dışı)
```

**Structure Decision**: Mevcut 3 BC genişletilir. Yeni dosya yalnız iki `CatalogConsumers` (Stock + Discount) + testler; gerisi mevcut dosyalara eklenir. Yeni slice/endpoint/DB yok.

## Complexity Tracking

Anayasa ihlali yok — tablo boş.
