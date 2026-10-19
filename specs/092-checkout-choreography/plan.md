# Implementation Plan: Checkout Orchestration → Choreography

**Branch**: `092-checkout-choreography` | **Date**: 2026-10-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/092-checkout-choreography/spec.md`

## Summary

`Checkout.Orchestrator` saga BC'si + `checkoutDb` sökülür; checkout üç yeni fanout integration event'iyle
koreografiye döner. Saga karmaşıklığının kaynağı olan **kalem-kalem** stok commit, Stock tarafında **tek
transaction atomik batch commit**'e çevrilir → döngü + LIFO telafi + fazlar + watchdog tümden gereksizleşir.
Her BC kaynak-adlı `*Consumers` ile event'e tepki verir; saga/orkestratör yok.

## Technical Context

**Language/Version**: .NET 10 (C#, Nullable + ImplicitUsings açık)
**Primary Dependencies**: Marten (Postgres document/event store), Wolverine (in-proc bus + RabbitMQ fanout)
**Storage**: Postgres (orderDb, stockDb, basketDb) — **checkoutDb KALDIRILIR**
**Testing**: xUnit + Shouldly, saf domain unit (host/entegrasyon harness yok — İlke VI)
**Target Platform**: Aspire AppHost (tüm sistem birlikte)
**Project Type**: Mikroservis / BC-per-service; bu feature bir BC'yi SİLER (net servis −1)
**Performance Goals**: Checkout mutlu yol sonucu bugünküyle birebir (regresyon yok); batch commit tek tx
**Constraints**: At-least-once teslimat → tüm tüketiciler idempotent; fanout binding'i tüketici kurar
**Scale/Scope**: 3 BC değişir (order/stock/basket), 1 BC + 1 DB silinir, 3 yeni event, ~10 eski mesaj silinir

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakıldı.*

| İlke | Değerlendirme | Sonuç |
|---|---|---|
| I. BC izolasyonu | İletişim yalnız fanout integration event; cross-DB erişim yok. checkoutDb gider, kalan DB'ler izole. | ✅ |
| I. (v1.11.0 saga-kanalı) | "Orkestre saga broker komut/yanıtı" kanalının **canlı örneği (049)** uykuya düşer — repoda başka orkestre-saga yok. İlke KALDIRILMAZ; anayasa PATCH amendment (örnek uykuda) + eski ADR status güncellenir. Governance notu, ihlal değil. | ⚠️ amendment |
| II. Zengin aggregate | `ProductStock.Commit`/`Order.Confirm`/`Cancel` aggregate metotları; invariant içeride. `RevertCommit` silinir. Anemik kaçış yok. | ✅ |
| III. VSA/CQRS, repo yok | Süreç-güdümlü tüketiciler (kullanıcı tetiklemez) `Domains/` dışı, kaynak-adlı `*Consumers`, BC kökünde (conventions). Handler doğrudan `IDocumentSession`. `Saga/` klasörleri silinir. | ✅ |
| IV. Result pattern | `Commit` → `ResultDomain`; consumer'lar Result/void süreç-glue. Hata kodu resource sabiti (`STOCK_INSUFFICIENT`). | ✅ |
| V. Scope yetki | Checkout akışı broker-only (HTTP scope yüzeyi yok); Payment/Order/Stock/Basket MCP auth matrisi değişmez. | ✅ |
| VI. Domain-TDD | `ProductStock.Commit` davranışı test-first (yeterli/yetersiz/idempotent). Batch atomiklik = Marten tx (host) → canlı doğrulama, İlke VI kapsam dışı. | ✅ |
| VII. FLOW.md | order/stock/basket FLOW.md aynı PR'da güncellenir; `checkout/FLOW.md` silinir; guard'lar koşulur. | ✅ (task) |
| VIII. Sır görünmezliği | İlgisiz (checkout event'leri sır taşımaz). | ✅ |

**Gate sonucu:** Geçti. Tek işaret = İlke I saga-kanalı örneğinin uykuya düşmesi → anayasa amendment görevi
(ihlal değil; v1.12.0 telemetri + v1.12.1 capability-link "örneksiz uyku" emsali birebir uygulanır).

## Project Structure

### Documentation (this feature)

```text
specs/092-checkout-choreography/
├── spec.md
├── plan.md                      # bu dosya
├── research.md                  # Phase 0
├── data-model.md                # Phase 1
├── contracts/
│   └── integration-events.md    # Phase 1 — yeni/silinen event sözleşmeleri
├── quickstart.md                # Phase 1 — uçtan uca doğrulama
└── tasks.md                     # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/order/Order.Api/
├── PaymentConsumers.cs          # DEĞİŞİR: PaymentSucceeded → StockCommitRequested yayar
├── StockConsumers.cs            # YENİ: StockCommitted→Confirm+OrderCompleted; StockCommitFailed→Cancel+log
├── Saga/CheckoutConsumers.cs    # SİLİNİR
└── Extensions/MessagingExtensions.cs  # checkout queue publish/listen çıkar; yeni fanout + IncludeType

src/services/stock/Stock.Api/
├── OrderConsumers.cs            # YENİ: StockCommitRequested → batch atomik commit → StockCommitted/Failed
├── Saga/CheckoutConsumers.cs    # SİLİNİR
├── Domains/Stocks/ProductStock.cs   # RevertCommit + STOCK_REVERT_* SİLİNİR
└── Extensions/MessagingExtensions.cs

src/services/basket/Basket.Api/
├── OrderConsumers.cs            # YENİ: OrderCompleted → sepet sil (idempotent)
├── Saga/CheckoutConsumers.cs    # SİLİNİR
└── Extensions/MessagingExtensions.cs  # OrderCompleted binding + IncludeType

src/others/Shared/
├── IntegrationEvents.cs         # YENİ: StockCommitRequested/StockCommitted/StockCommitFailed (additive)
├── CheckoutMessages.cs          # komut/reply record'ları SİLİNİR (CheckoutItem/OrderAddress/ErrorClass kalır/taşınır)
└── RabbitMqConstants.cs         # Checkout.* queue sabitleri SİLİNİR; yeni fanout exchange adları EKLENİR

src/services/checkout/           # TÜM DİZİN SİLİNİR (Checkout.Orchestrator + FLOW.md)
src/aspire/AppHost/              # Checkout.Orchestrator servis + checkoutDb kaydı SİLİNİR
tests/Checkout.*.Tests/          # varsa SİLİNİR
ECommerceAgent.slnx              # checkout proje + test kayıtları ÇIKAR
docs/adr/adr-checkout-choreography-migration.md   # YENİ ADR; eski ADR status "yerini aldı"
CLAUDE.md                        # BC haritası + Yapma listesi güncellenir
```

**Structure Decision**: Yeni proje yok; mevcut order/stock/basket BC'lerine kaynak-adlı event tüketicisi
eklenir, bir BC (checkout) tamamen silinir. Fiziksel klasör = solution klasörü birebir korunur.

## Complexity Tracking

Anayasa ihlali yok — bu feature karmaşıklığı AZALTIR (BC −1, DB −1, saga + LIFO + watchdog −1). Tablo boş.
