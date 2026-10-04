# Implementation Plan: Storefront Elasticsearch Search

**Branch**: `086-storefront-elasticsearch-search` | **Date**: 2026-10-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/086-storefront-elasticsearch-search/spec.md`

## Summary

Storefront'un Postgres read-model'i (`StorefrontView`) **CQRS + Event Sourcing** modeliyle değiştirilir:
Postgres KALIR ama işi değişir — read-model deposu değil, **ince event-log / gerçek-kaynak** (Marten
event store, stream id = ProductId). Gelen integration event'ler (Catalog/Stock/Reviews/Discount) ürün
stream'ine append edilir; **Marten async projection** stream'i katlayıp **Elasticsearch** dokümanı üretir.
`query_storefront` MCP tool'u artık LLM'in ürettiği **Elasticsearch Query DSL (JSON)** çalıştırır (text +
fuzzy + kNN semantik tek sorguda). Embedding doc alanı (`dense_vector`) olur; ayrı tablo kalkar. Index
projection rebuild ile yeniden kurulabilir. `UserPurchase` → Library BC'ye taşınır.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten 9.5 (Postgres event store + async projection daemon), Wolverine +
RabbitMQ (integration event tüketimi), **Elasticsearch 8.x** (`Elastic.Clients.Elasticsearch` + Aspire
hosting/client integration — yeni), `Microsoft.Extensions.AI` OpenAI `IEmbeddingGenerator` (mevcut),
ModelContextProtocol.AspNetCore (MCP tool).

**Storage**: Postgres (storefrontDb) = Marten event-log (mt_events) + async daemon progress + AgentQueryLog;
**Elasticsearch** = sorgu projeksiyonu (tek index, _id = ProductId). ES verisi her zaman event-log'dan
yeniden kurulabilir (gerçek-kaynak = Postgres).

**Testing**: xUnit + Shouldly. Domain-TDD kapsamı = projeksiyon **fold** mantığı (event → doc durumu) +
embedding tazeleme kararı (saf, test-first İLKE VI).

**Target Platform**: Linux container, Aspire AppHost orkestrasyonu (ES = yeni container kaynağı).

**Project Type**: Backend mikroservis (storefront BC) — agent-only yüzey (tek MCP tool).

**Performance Goals**: SC-001 tipik arama < 1 sn; SC-003 kaynak değişimi aramada < 5 sn (eventual
consistency penceresi — async daemon gecikmesi dahil).

**Constraints**: Integration event sözleşmeleri DEĞİŞMEZ (assumption). ES'e dual-write YOK (tek sorgu
store ES, event-log ayrı sorumluluk). JSON guard KAPSAM DIŞI (ayrı iş) — bu sürüm güvenli istemci ortamı
varsayar + minimal sunucu kalkanı (sabit index + size tavan + _source whitelist + timeout).

**Scale/Scope**: ~20k kitap künyesi (Open Library seed); tek index, exact/HNSW kNN ölçekte rahat.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **İLKE I (BC izolasyonu):** ✅ ES = storefront BC'nin KENDİ sorgu deposu; başka BC DB'sine erişim yok.
  Kaynak veri yalnız integration event'lerden (sözleşme değişmez). `UserPurchase` kendi DB'si olan Library
  BC'ye taşınır (storefront'tan çıkar). MCP yalnız agent tüketir (query_storefront). ✅
- **İLKE II (zengin aggregate):** ✅ Yeni aggregate YOK. Projeksiyon doc'u (`StorefrontDocument`) read-model
  fold durumu (aggregate değil — read-model/projeksiyon istisnası, mevcut `StorefrontView` emsali). Event-log
  yakalanan **yabancı** event'leri tutar, kendi domain olayımız değil (durable inbox).
- **İLKE III (VSA+CQRS, repository yok):** ✅ Query slice `Features/Agents/Queries/QueryStorefront.cs`'te
  kalır; ES client doğrudan kullanılır (repository yok — Marten `IDocumentSession` yerine ES client, aynı
  "doğrudan store" ruhu). Event-log appender'lar + projection süreç-güdümlü → `Consumers/` + `Projections/`.
  MCP tool ince sarmalayıcı kalır.
- **İLKE IV (Result pattern):** ✅ Query handler `FeatureObjectResultModel<T>` döner (değişmez).
- **İLKE V (scope yetki):** ✅ Storefront MCP anonim kalır (CLAUDE.md: storefront/catalog/stock anonim);
  auth kablosu değişmez.
- **İLKE VI (Domain-TDD):** ✅ Projeksiyon fold + embedding kararı saf → test-first; tasks.md'de test
  task'ı impl'den önce. ES/daemon/consumer wiring kapsam dışı (test-sonra / canlı doğrulama).
- **İLKE VII (FLOW.md):** ✅ Domain süreci değişiyor → `storefront/FLOW.md` AYNI PR'da güncellenir
  (read-model upsert → event-log append + projection); `library/FLOW.md` UserPurchase adımını kazanır.

**Sonuç: GEÇTİ.** ES'in yeni depo olması İlke I ihlali değil (BC kendi store'unu seçer); CQRS+ES
kullanıcı-seçimli sanksiyonlu desen. Complexity Tracking gerekmez.

## Project Structure

### Documentation (this feature)

```text
specs/086-storefront-elasticsearch-search/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — kararlar (ES projection, ordering, index, _id, backfill)
├── data-model.md        # Phase 1 — event-log + ES doc + taşınan/silinen varlıklar
├── contracts/
│   ├── search-index-mapping.json   # ES index şeması (analyzer + dense_vector + alanlar)
│   └── query-storefront-tool.md    # MCP tool sözleşmesi (ES DSL giriş/çıkış)
├── quickstart.md        # Phase 1 — US bazlı doğrulama senaryoları
└── tasks.md             # /speckit-tasks üretir (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/services/storefront/Storefront.Api/
├── Search/                              # YENİ — ES altyapısı
│   ├── ElasticsearchExtensions.cs       #   client kaydı (Aspire) + Program.cs kablosu
│   ├── StorefrontSearchIndex.cs         #   index adı + mapping bootstrap (sabit, sunucu-sahipli)
│   └── StorefrontDocument.cs            #   ES doc şekli + fold (saf, TDD) — StorefrontView yerini alır
├── Projections/                         # YENİ — event-log → ES
│   └── StorefrontProjection.cs          #   Marten async IProjection: stream fold → ES upsert/delete
├── Consumers/                           # DEĞİŞİR — upsert yerine event-log'a append
│   ├── CatalogConsumers.cs  (ProductChangedEvent  → Events.Append)
│   ├── StockConsumers.cs    (StockChangedEvent    → Events.Append)
│   ├── ReviewsConsumers.cs  (ReviewSummaryChanged → Events.Append)
│   ├── DiscountConsumers.cs (ProductDiscountChanged→ Events.Append)
│   └── OrderConsumers.cs    → SİLİNİR (UserPurchase Library'ye taşınır)
├── Domains/Storefront/                  # RENAME (eski Domains/StorefrontView/ — view kavramı öldü)
│   └── Features/Agents/Queries/QueryStorefront.cs  # DEĞİŞİR — ES DSL çalıştırır (SQL değil)
│   # (eski StorefrontView.cs + ProductDescriptionEmbedding.cs → SİLİNİR; doc şekli Search/StorefrontDocument.cs'e göçtü)
├── Domains/UserPurchase/                # SİLİNİR (→ Library)
├── AgentSql/
│   ├── AgentQueryLog.cs                 # KALIR (ES sorgu izi; Marten doc Postgres'te)
│   └── (AgentSqlGuard, StorefrontSellableSchema, EmbedPlaceholder, AgentQuerySurfaceBootstrap,
│         kısıtlı rol conn-source) → SİLİNİR (SQL yüzeyi gider; ES guard ayrı iş)
├── Extensions/MartenExtensions.cs       # DEĞİŞİR — doc şemaları yerine event-log + async projection kaydı
└── Program.cs                           # DEĞİŞİR — ES client + index bootstrap; SQL-rol bootstrap sökülür

src/services/library/Library.Api/
├── OrderConsumers.cs                    # YENİ — OrderCompleted → UserPurchase (storefront'tan taşınan)
└── Domains/UserPurchase/UserPurchase.cs # YENİ — taşınan read-model (Library DB)

src/aspire/AppHost/AppHost.cs            # DEĞİŞİR — Elasticsearch kaynağı + storefront referans/WaitFor;
                                         #           Library'ye OrderCompleted için rabbit zaten var
Directory.Packages.props                 # DEĞİŞİR — Elastic client + Aspire ES hosting/client sürümleri
```

**Structure Decision**: Mevcut VSA düzeni korunur. Yeni `Search/` (ES altyapısı) ve `Projections/`
(event-log → ES, süreç-güdümlü, `Domains/` dışı — conventions "iç süreç" kuralı) klasörleri eklenir.
Consumers appender'a dönüşür (upsert yerine `session.Events.Append`). SQL serbest-sorgu yüzeyi (guard +
kısıtlı rol + sellable view) tümüyle sökülür; yerini sabit ES index + minimal sunucu kalkanı alır.
`Domains/StorefrontView/` → `Domains/Storefront/` yeniden adlandırılır (read-model/aggregate "view" kavramı
kalktı; yalnız query slice'ı barındırır — doc şekli artık `Search/StorefrontDocument.cs`).

## Complexity Tracking

Constitution Check ihlali yok → boş.