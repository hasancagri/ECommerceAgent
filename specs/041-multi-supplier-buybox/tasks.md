# Tasks: Multi-Supplier Dropship — Procurement BC (Havuz + Buy-Box)

**Input**: `specs/041-multi-supplier-buybox/` — spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md

**Not**: Domain-TDD (İlke VI) — PoolProduct/Supplier davranış testleri implementasyondan ÖNCE yazılır.
Söküm ÖNCE gelir (eski ingest yolu ile yeni yol birlikte yaşayamaz); söküm sonrası build yeşil kalmalı.

## Phase 1: Setup

- [X] T001 Branch doğrula: `041-multi-supplier-buybox` aktif; working tree'deki " 2" kopya temizliği commit'e dahil

## Phase 2A: Söküm (foundational — eski ingest yolu gider)

- [X] T002 IngestionAgent projesini sil: `src/agents/IngestionAgent/` + `ECommerceAgent.slnx` kaydı
- [X] T003 Supplier.Gateway projesini sil: `src/services/supplier/Supplier.Gateway/` + slnx kaydı
- [X] T004 AppHost temizliği: `ingestion-agent` + `supplier-gateway` resource'ları + `supplierGatewayDb`:
      `src/aspire/AppHost/AppHost.cs`
- [X] T005 [P] Shared söküm: `SupplierProductSnapshotReceived` (`src/others/Shared/IntegrationEvents.cs`) +
      `SupplierProductSnapshot` bloğu (`src/others/Shared/RabbitMqConstants.cs`)
- [X] T006 [P] Catalog agent-yazım yüzeyi sil: `Domains/Brands/Features/Agents/UpsertBrandForAgent.cs`,
      `Domains/Categories/Features/Agents/UpsertCategory.cs`, `Domains/Products/Features/Agents/UpsertProduct.cs` +
      ilgili `*McpTools.cs` tool metotları (okuma tool'ları KALIR)
- [X] T007 [P] Stock agent-yazım yüzeyi sil: `Domains/Stocks/Features/Agents/SetStock.cs` + McpTools kaydı
- [X] T008 [P] `IngestionWriteException` sil: `src/others/Common/Exceptions/IngestionWriteException.cs`
- [X] T009 [P] Supplier.Api eski uç + veri sil: `Domains/Feeds/FeedEndpointExtension.cs` eski GET + `Datasets/products.json`
- [X] T010 Söküm sonrası derleme + testler: `dotnet build && dotnet test` (ingest'e bağlı test kalmadıysa yeşil)

**Checkpoint**: Sistem ingest'siz ama sağlıklı; vitrin/sepet/checkout eski veriyle çalışır durumda derlenir.

## Phase 2B: Foundational (yeni iskelet — tüm story'leri bloklar)

- [X] T011 Shared ekler: `CanonicalProductUpserted` + `BuyBoxChanged` + `ProductLinked` event'leri
      (`src/others/Shared/IntegrationEvents.cs`) + exchange/queue sabitleri (`RabbitMqConstants.cs`)
      — contracts/integration-events.md birebir
- [X] T012 [P] `SchemaConstants.ProcurementSchemaName` = "procurementManagement": `src/others/Shared/Utils/Constants/SchemaConstants.cs`
- [X] T013 Procurement.Api proje iskeleti: `src/services/procurement/Procurement.Api/` (csproj net10.0, GlobalUsings,
      Program.cs: Marten+Newtonsoft+Wolverine+RabbitMQ autoprovision+versioning+Scalar+AddAllDependencies) + slnx kaydı
- [X] T014 [P] Hata kodları: `Procurement.Api/Constants/ProcurementResourceConstants.cs` (barkodsuz satır, eşlenemeyen
      kategori, negatif fiyat/stok, enrich hataları)
- [X] T015 [P] Options: `Procurement.Api/Options/FeedPullOptions.cs` (PullCron, FirstPullDelaySeconds) +
      `EnrichmentOptions.cs` (OpenAI ApiKey/Model — fail-fast) — Options pattern (POCO unwrap)
- [X] T016 AppHost: `procurementDb` + `procurement-api` resource (refs: procurementDb, rabbit, supplier-api;
      WaitFor catalog-api + stock-api — tüketici kuyrukları publisher'dan önce bağlansın): `AppHost.cs`
- [X] T017 [P] Test projesi: `tests/Procurement.Api.Tests/Procurement.Api.Tests.csproj` (xUnit+Shouldly) + slnx kaydı

**Checkpoint**: Boş Procurement servisi Aspire'da kalkar; event kontratları derlenir.

## Phase 3: User Story 1 — Vitrin benzersiz ürünü en iyi fiyatla gösterir (P1)

**Goal**: İki mock feed → havuz → eksiksiz kanonik yayın → Catalog/Stock → vitrinde buy-box fiyatlı ürünler.

**Independent Test**: pull sonrası vitrinde eksiksiz satırlardan gelen ürünler; çakışan barkodda stoklu en ucuz fiyat.
(3000'in TAMAMI US3'te — eksik ~%10 satır enrich ister; bu fazın sonunda ~2700 beklenir.)

- [X] T018 [P] [US1] Dataset kontrat testleri (test-first): `tests/Supplier.Api.Tests/FeedDatasetTests.cs`
      YENİ proje (A=1800, B=1700, çakışan=500, benzersiz=3000; ~%10 eksik alan; barkod hep dolu;
      rev2 yalnız fiyat/stok değiştirir) + slnx kaydı
- [X] T019 [US1] JSON dataset'ler: `Supplier.Api/Datasets/supplier-{a,b}.rev{1,2}.json`
      (script-üretimli, commit'li; contracts/mock-feed-api.md dağılımları) — testleri yeşile çek
- [X] T020 [US1] Mock uçlar: `GET /v1/feeds/{supplierCode}` + `POST /v1/feeds/{supplierCode}/advance`
      (bellek-içi rev → rev dosyası seçimi): `Supplier.Api/Domains/Feeds/FeedEndpointExtension.cs`
- [X] T021 [P] [US1] Supplier aggregate testleri (test-first, kırmızı): `tests/Procurement.Api.Tests/SupplierTests.cs`
      (Create guard'ları, ResolveCategory: eşleşme/eşleşmeme)
- [X] T022 [P] [US1] PoolProduct testleri (test-first, kırmızı): `tests/Procurement.Api.Tests/PoolProductTests.cs`
      (UpsertListing hash Unchanged/Added/Updated + guard'lar; RebuildCanonical Priority-merge + sıra-bağımsızlık;
      EvaluateBuyBox: en ucuz/eşitlikte düşük Priority/tek offer; TryTakePublish: complete+değişim koşulu, NoChange)
- [X] T023 [US1] Supplier aggregate + VO: `Procurement.Api/Domains/Suppliers/Supplier.cs` +
      `ValueObjects/SupplierValueObjects.cs` (CategoryMapping) — data-model.md; testler yeşil
- [X] T024 [US1] PoolProduct aggregate + entity + VO'lar: `Domains/PoolProducts/PoolProduct.cs`,
      `Entities/SupplierListing.cs`, `ValueObjects/PoolProductValueObjects.cs` (durum enum'u PoolProduct.cs'te);
      aggregate kuralları (helper yok, summary+remarks) — testler yeşil
- [X] T025 [US1] Marten kayıtları: PoolProduct (string Id=barkod) + Supplier + şema: `Procurement.Api/Program.cs`
- [X] T026 [P] [US1] Procurement seed: `Procurement.Api/Seeding/ProcurementSeedHostedService.cs` (supplier-a/b
      Priority 1/2 + kanonik taksonomi kopyası + tedarikçi başına kategori eşleme tabloları; idempotent)
- [X] T027 [P] [US1] Catalog taksonomi seed: `Catalog.Api/Seeding/CatalogTaxonomySeedHostedService.cs`
      (kanonik Category>SubCategory ağacı, ParentCategoryId ile; idempotent get-or-create)
- [X] T028 [US1] Feed pull: `Procurement.Api/Infrastructure/Feeds/SupplierFeedClient.cs` (service discovery) +
      `FeedPullJob.cs` (Hangfire cron + SemaphoreSlim) + `Domains/PoolProducts/Features/Commands/PullSupplierFeed.cs`
      (satır başına PoolProduct upsert: barkodsuz reddet+logla; kategori eşle; hash-diff) + Hangfire wiring Program.cs
- [X] T029 [US1] Manuel tetik ucu: `POST /v1/feeds/pull`: `Domains/PoolProducts/PoolProductEndpointExtension.cs` (anonim)
- [X] T030 [US1] Okuma pencereleri (CLAUDE.md "her aggregate REST penceresi" kuralı):
      `GET /v1/suppliers` + `GET /v1/pool-products/{barcode}` (+durum filtreli liste) —
      `Domains/Suppliers/Features/Queries/GetSuppliers.cs` + `SupplierEndpointExtension.cs`,
      `Domains/PoolProducts/Features/Queries/GetPoolProduct.cs` (endpoint'ler PoolProductEndpointExtension'a eklenir)
- [X] T031 [US1] Yayın: `Features/Commands/PublishPoolProduct.cs` — TryTakePublish → `CanonicalProductUpserted`
      (buy-box Price+Stock dahil) publish; Wolverine outbox + exchange wiring Program.cs
- [X] T032 [US1] Catalog tüketici: `Catalog.Api/ProcurementEventHandlers.cs` — CanonicalProductUpserted:
      Gtin upsert (yeni/mevcut), Brand get-or-create, kategori NormalizedName çözümü (çözülemezse error),
      ölçü/Sku/SEO yazımı, `ProductChangedEvent` + yeni üründe `ProductLinked{InitialStock}`;
      kuyruk `catalog.procurement-events` Sequential + Gtin index: `Catalog.Api/Program.cs`
- [X] T033 [US1] Stock tüketici: `Stock.Api/ProcurementEventHandlers.cs` — ProductLinked: `BarcodeLink` doc
      (`Domains/Stocks/BarcodeLink.cs`) upsert + ProductStock `SetQuantity(InitialStock)` + `StockChangedEvent`;
      kuyruk `stock.procurement-events` + Marten kaydı: `Stock.Api/Program.cs`
- [X] T034 [US1] Derleme + tüm testler + canlı smoke: pull → vitrin eksiksiz ürünler (~2700), çakışan barkodda
      stoklu en ucuz fiyat, eşit fiyatta supplier-a (quickstart 1-3 kısmi)

**Checkpoint**: Yapısal (AI'sız) boru ucu uca canlı; eksik satırlar Pending bekler.

## Phase 4: User Story 2 — Buy-box değişimi vitrine yansır (P2)

**Goal**: rev artışı sonrası kazanan devri; delist; kazanansız ürün stok 0.

**Independent Test**: advance + pull → fiyat/stok/kazanan güncellenir; değişmeyen feed sessiz (quickstart 4-5).

- [X] T035 [US2] PoolProduct değişim testleri (test-first): `tests/Procurement.Api.Tests/PoolProductTests.cs` ekle
      (kazanan devri; kazanan stoksuz → sonraki; hepsi stoksuz → kazanansız Stock 0; MarkDelisted yarıştan çıkarır;
      aynı listing tekrar → NoChange/yayınsız)
- [X] T036 [US2] `BuyBoxChanged` yayını: PullSupplierFeed/PublishPoolProduct akışında karar değişiminde publish
      (yalnız BuyBoxDecision değişince); testleri yeşile çek
- [X] T037 [US2] MarkDelisted: pull'da o tedarikçinin feed'inde görünmeyen barkodlar işaretlenir:
      `Features/Commands/PullSupplierFeed.cs`
- [X] T038 [P] [US2] Catalog BuyBoxChanged handler: Gtin lookup → SetPrice → ProductChangedEvent; bilinmeyen Gtin
      YOK SAY: `Catalog.Api/ProcurementEventHandlers.cs`
- [X] T039 [P] [US2] Stock BuyBoxChanged handler: BarcodeLink lookup → SetQuantity → StockChangedEvent; link yoksa
      YOK SAY: `Stock.Api/ProcurementEventHandlers.cs`
- [X] T040 [US2] Canlı: `advance supplier-a` + pull → kazanan devri vitrine yansır; kazanansız örnek vitrinde
      stok 0 + sepete eklenemez; tekrar pull sessiz (quickstart 4 + 5 ilk madde)

**Checkpoint**: Buy-box canlı rekabeti işletiyor; feed değişimi tek POST'la simüle edilebiliyor.

## Phase 5: User Story 3 — Havuz eksikleri agent tamamlar, eksiksizi yayınlar (P2)

**Goal**: Eksik ~%10 satır enrich'ten geçip yayınlanır → vitrin 3000 tamamlanır; hata yolu DLQ.

**Independent Test**: quickstart 2 (3000 + AI yalnız eksikte) + 6 (DLQ senaryosu).

- [X] T041 [US3] ApplyEnrichment testleri (test-first): `tests/Procurement.Api.Tests/PoolProductTests.cs` ekle
      (yalnız içerik alanları dolar; barkod/ölçü/fiyat/stok denemesi Error; SourceHash cache: aynı girdi → atla;
      kategori kanonik listeden — liste dışı Error)
- [X] T042 [US3] EnrichmentAgent: `Procurement.Api/Infrastructure/Enrichment/EnrichmentAgent.cs` (Singleton
      ChatClientAgent, Temperature=0, structured JSON; girdi: mevcut içerik+eksik alanlar+kanonik kategori listesi;
      EnrichmentOptions fail-fast Program.cs)
- [X] T043 [US3] EnrichPoolProduct command: `Domains/PoolProducts/Features/Commands/EnrichPoolProduct.cs` —
      lokal durable kuyruk `procurement.enrich`, retry 10s/30s/60s → error queue; ApplyEnrichment + publish zinciri
- [X] T044 [US3] Pull akışına enrich tetiği: eksik kanonik → kuyruğa `EnrichPoolProduct{Barcode}` (hash değişmediyse
      tetiklenmez): `Features/Commands/PullSupplierFeed.cs`
- [X] T045 [US3] Canlı: temiz DB + pull → vitrin TAM 3000; loglarda AI yalnız eksik satırlarda; bozuk key ile DLQ
      senaryosu + replay (quickstart 2 + 6) — DLQ/replay alt-senaryosu BİLİNÇLİ ATLANDI (desen 007/015'te kanıtlı)

**Checkpoint**: SC-002/005 kapandı; tüm satırlar için tek boru.

## Phase 6: Polish & Cross-Cutting

- [X] T046 Idempotency + sıra bağımsızlığı canlı: aynı pull tekrar → 0 yayın; (opsiyonel temiz DB) B önce A sonra →
      aynı kanonik + aynı buy-box (quickstart 5, SC-007/008)
- [X] T047 Regresyon: `dotnet build && dotnet test` tüm çözüm + chat ürün sorgusu + sepet/checkout smoke (quickstart 7)
      — build + 274 test yeşil (Order amount-mismatch 041-öncesi kırmızı); vitrin+MCP smoke PASS; chat/sepet
      etkileşimli akışı tarayıcıda kullanıcıya bırakıldı
- [X] T048 CLAUDE.md güncelle: 007/014/015 ingestion bölümleri yeniden yazılır (Procurement BC, söküm, yeni event'ler,
      enrich agent, mock rev); bayat Gateway/IngestionAgent satırları temizlenir
- [X] T049 Memory/notlar: 041 durumunu güncelle; Obsidian gerekçe notu (buy-box/havuz kararları) kullanıcı isterse

## Dependencies

- Phase 2A → 2B → US1 → US2 → US3 → Polish (US2 ve US3, US1'in borusuna bağımlı — sıralı önerilir).
- US2 ile US3 kendi aralarında bağımsızdır (US1 sonrası paralel alınabilir).
- Söküm (2A) her şeyden önce: eski yol yeni kontratlarla çakışmaz, build yeşil kalır.

## Parallel Örnekleri

- 2A: T005-T009 paralel (farklı projeler).
- 2B: T012/T014/T015/T017 paralel; T013 sonrası T016.
- US1: T018+T021+T022 paralel (test-first blok); T026+T027 paralel; T032 ile T033 farklı servisler — paralel.
- US2: T038 ile T039 paralel.

## Implementation Strategy

- **MVP = Phase 2A+2B+US1**: söküm + yapısal boru + buy-box ilk yayın (~2700 ürün vitrinde, AI'sız).
- Sonra US2 (canlı rekabet) → US3 (enrich ile 3000 tamam) → Polish.
- Her checkpoint'te `dotnet build && dotnet test` yeşil tutulur; canlı doğrulama quickstart adımlarıyla yapılır.