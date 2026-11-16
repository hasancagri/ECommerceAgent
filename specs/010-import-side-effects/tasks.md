# Tasks: Import Yan-Etkileri (Stok Tohumlama + İndirim İthali)

**Feature**: `specs/010-import-side-effects` | **Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

3 BC genişler (Catalog yayıncı, Stock+Discount tüketici). Yeni servis/DB yok. Yalnız integration event (senkron yok). US1 (stok) ve US2 (indirim) FARKLI BC → bağımsız. Domain-TDD (İlke VI): Campaign açık-uçlu test-first; consumer'lar (random/IO) test-sonra + canlı Aspire doğrulama.

**Kısaltma**: `Shared/`=`src/others/Shared/`, `Catalog/`=`src/services/catalog/Catalog.Api/`, `Stock/`=`src/services/stock/Stock.Api/`, `Discount/`=`src/services/discount/Discount.Api/`.

---

## Phase 1: Setup

- [X] T001 Başlangıç yeşili: `dotnet build && dotnet test` — mevcut durum temiz mi doğrula

---

## Phase 2: User Story 1 — Stok tohumlama → satılabilir (P1) 🎯 MVP

**Hedef**: Her yeni ürün için Stock 30-50 adet açar → Catalog kopyası dolar → fiyatlı+yazarlı kitap satılabilir.

**Bağımsız test**: import → her ürün için 1 StockItem (30-50); Catalog StockCount dolu; fiyatlı+yazarlı kitap IsSalable=true; 2. import yeni kayıt açmaz.

- [X] T002 [US1] `Shared/RabbitMqConstants.cs`: `ProductAdded.Queues.Stock = "stock.product-added"` ekle (Stock bu kuyruğu Catalog ProductAdded exchange'ine bağlayacak)
- [X] T003 [US1] Stock yeni consumer `Stock/CatalogConsumers.cs`: `Handle(ProductAdded)` → ProductId için StockItem VARSA atla (idempotent, `IQuerySession` sorgu) → `qty=Random.Shared.Next(30,51)` → `StockItem.Create(productId, qty)` → store → `PublishAsync(StockChanged(productId, qty))`
- [X] T004 [US1] Stock wiring `Stock/Extensions/MessagingExtensions.cs`: `DeclareExchange(ProductAdded.Exchange)` + `BindQueue(ProductAdded.Queues.Stock)` + `ListenToRabbitQueue(...Stock)` + `opts.Discovery.IncludeType(typeof(CatalogConsumers))` (çoğul suffix bulunmaz — ZORUNLU)
- [X] T005 [US1] Doğrula: `dotnet build && dotnet test`; Aspire küçük import → StockItem 30-50 açıldı + Catalog StockCount dolu + fiyatlı/yazarlı IsSalable=true; 2. import mükerrer AÇMADI (quickstart US1)

**Checkpoint**: US1 tek başına = MVP (mağaza satılabilir).

---

## Phase 3: User Story 2 — İndirim ithali (P2)

**Hedef**: Excel indirim oranı → Discount açık-uçlu kampanya → Catalog ürün indirim kopyası dolar.

**Bağımsız test**: indirimli ISBN import → Discount'ta açık-uçlu Campaign + Catalog DiscountRate = Excel kesri; indirimsiz ISBN'de kampanya yok, oran boş.

- [X] T006 [US2] `Shared/IntegrationEvents.cs`: `record ProductDiscountImported(Guid ProductId, decimal Rate)` + `Shared/RabbitMqConstants.cs`: `ProductDiscountImported` (Exchange `catalog.product-discount-imported` + `Queues.Discount`)
- [X] T007 [P] [US2] Test-first `tests/Discount.Api.Tests/CampaignTests.cs`: `Create_OpenEndedNullEnd_Succeeds`, `IsActiveAt_NullEnd_TrueAfterStart`, `IsActiveAt_NullEnd_FalseBeforeStart`, `Create_RateAboveMax_ReturnsError`, `Create_WindowedEndBeforeStart_ReturnsError` (geriye-uyum) — contracts/product-discount-imported.md tablosu
- [X] T008 [US2] Campaign evrimi `Discount/Domains/Campaigns/Campaign.cs`: `EndsAt` → `DateTimeOffset?`; `Create(productId, rate, startsAt, DateTimeOffset? endsAt)` (endsAt doluysa `>startsAt`, null ise pencere-guard atla; rate 0.01-0.90 KORU); `IsActiveAt`: `now>=StartsAt ∧ (EndsAt is null ∨ now<EndsAt) ∧ !Cancelled`; T007 geçsin
- [X] T009 [US2] Catalog mapper `Catalog/Helpers/ExcelRowMapper.cs`: `ImportRow.DiscountRate` (`decimal?`) + `Map`'te "İndirim %" oku (ZATEN kesir — dönüşüm YOK; 0/boş → null)
- [X] T010 [US2] Catalog worker `Catalog/Process/ImportJobWorker.cs`: yeni ürün + `DiscountRate∈(0,0.90]` → batch'e `ProductDiscountImported(product.Id, rate)` → post-commit publish (ProductAdded deseni, yalnız !dryRun)
- [X] T011 [US2] Catalog yayın `Catalog/Extensions/MessagingExtensions.cs`: `DeclareExchange(ProductDiscountImported.Exchange)` + `PublishMessage<ProductDiscountImported>().ToRabbitExchange(...)`
- [X] T012 [US2] Discount yeni consumer `Discount/CatalogConsumers.cs`: `Handle(ProductDiscountImported)` → `Campaign.Create(productId, rate, DateTimeOffset.UtcNow, endsAt: null)` → başarılıysa store + `PublishAsync(ProductDiscountChanged(productId, rate))`; başarısız (geçersiz oran) atla (iş durmaz)
- [X] T013 [US2] Discount wiring `Discount/Extensions/MessagingExtensions.cs`: `DeclareExchange(ProductDiscountImported.Exchange)` + `BindQueue(...Queues.Discount)` + `ListenToRabbitQueue(...Discount)` + `opts.Discovery.IncludeType(typeof(CatalogConsumers))`
- [X] T014 [US2] Doğrula: `dotnet build && dotnet test`; Aspire import indirimli ISBN → Discount açık-uçlu Campaign (EndsAt null) + Catalog DiscountRate = Excel kesri; indirimsizde boş (quickstart US2)

**Checkpoint**: US1 + US2 = tek re-import ürün+etiket+kapak+stok+indirim doldurur.

---

## Phase 4: Polish & Cross-Cutting

- [X] T015 [P] FLOW.md as-built: `catalog/FLOW.md` (4c/6), `discount/FLOW.md` (1b + hotspot `closed`), `stock/FLOW.md` (1b) — `_(planned, 010)_` ekini düşür, kenar-anchor ekle (CatalogConsumers, Campaign.Create, StockItem.Create)
- [X] T016 Tam re-import: catalogDb+stockDb+discountDb sil → `_v3_enriched.xlsx` tek import → SC-001 (fiyatlı+yazarlı kitapların ≥%99'u IsSalable) + DB spot-check (StockItem 30-50 aralığı/tek kayıt, Campaign EndsAt null, DiscountRate = Excel) — quickstart SQL

---

## Dependencies & Execution Order

- **Setup (T001)** → önce
- **US1 (T002-T005)** → MVP; bağımsız (yalnız Stock + Shared sabiti)
- **US2 (T006-T014)** → bağımsız (Discount + Catalog yayıncı + Shared event); US1'den AYRI test edilebilir. Test-first: T007→T008
- **Polish (T015-T016)** → US1+US2 sonrası; T016 ikisini birlikte canlı doğrular

## Parallel Opportunities

- **US1 ∥ US2** — farklı BC'ler, farklı dosyalar (ortak yalnız Shared — T002/T006 ayrı bloklar)
- T007 (CampaignTests) US2 impl'inden önce bağımsız yazılır
- T015 (FLOW.md) T016'dan bağımsız

## MVP Scope

**US1 (T001-T005)** = teslim edilebilir MVP: kitaplar satılabilir olur (stok açılır). US2 indirim deneyimini katar. T016 tam re-import ikisini + 008/009 (etiket) + File (kapak) birlikte canlı doğrular — teslim kapısı.
