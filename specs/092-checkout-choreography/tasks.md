# Tasks: Checkout Orchestration → Choreography

**Input**: Design documents from `specs/092-checkout-choreography/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/integration-events.md

**Tests**: İlke VI — saf domain (ProductStock.Commit davranışı) test-first; handler/wiring/silme test-sonra/
canlı doğrulama. Batch atomikliği (Marten tx) host gerektirir → unit kapsam dışı, quickstart S2 ile doğrulanır.

**Not (refactor):** Bu bir davranış-korumalı geçiş; US1 (mutlu) ve US2 (yetersiz) aynı iki consumer'ın iki
dalı → tam bağımsız değiller ama ayrı test edilebilir. Cutover & Cleanup fazı tek atomik PR'da bitmeli
(eski + yeni yol aynı anda yaşayamaz: Checkout.Orchestrator StartCheckout'suz ölür).

## Phase 1: Setup

- [x] T001 Baseline doğrula: `dotnet build && dotnet test` yeşil (geçiş öncesi referans noktası).

---

## Phase 2: Foundational (tüm story'leri bloke eder)

**⚠️ Yeni sözleşmeler eklenmeden hiçbir consumer derlenemez.**

- [x] T002 [P] Yeni event'leri ekle `src/others/Shared/IntegrationEvents.cs`: `StockCommitRequested(Guid OrderId, Guid UserId, IReadOnlyList<CheckoutItem> Items)`, `StockCommitted(Guid OrderId)`, `StockCommitFailed(Guid OrderId, string ReasonCode)` (additive; `CheckoutItem` Shared'dan).
- [x] T003 [P] Yeni fanout exchange sabitlerini ekle `src/others/Shared/RabbitMqConstants.cs`: `stock.commit-requested` (queue `stock.commit-requested`), `stock.committed` (queue `order.stock-committed`), `stock.commit-failed` (queue `order.stock-commit-failed`); `OrderCompleted` exchange'ine Basket queue adı `basket.order-completed`.

**Checkpoint**: Sözleşmeler hazır → consumer'lar yazılabilir.

---

## Phase 3: User Story 1 — Ödenmiş sipariş uçtan uca tamamlanır (P1) 🎯 MVP

**Goal**: Mutlu yol koreografiyle bugünküyle birebir aynı sonucu üretir (stok düş → confirm → sepet sil → Reviews/Library).

**Independent Test**: quickstart S1 — sepete ekle → start_payment → callback success → stok düştü, Confirmed, sepet boş, sinyaller geldi.

- [x] T004 [US1] Stock batch-commit consumer `src/services/stock/Stock.Api/OrderConsumers.cs` (YENİ): `Handle(StockCommitRequested)` — tüm kalemlerin `ProductStock`'unu tek `IDocumentSession` ile yükle, her birine `Commit(qty, OrderId)`, hepsi Ok ise tek `SaveChangesAsync` + `StockCommitted(OrderId)` yayar. (Hata dalı T011'de.)
- [x] T005 [US1] Order mutlu-yol consumer `src/services/order/Order.Api/StockConsumers.cs` (YENİ): `Handle(StockCommitted)` — order yükle, `Status==Confirmed` ise no-op; değilse `order.Confirm()` + `OrderCompleted` fanout yayar (mevcut `Saga/CheckoutConsumers` Confirm mantığını buraya taşı). (Hata dalı T012'de.)
- [x] T006 [US1] Order giriş değişimi `src/services/order/Order.Api/PaymentConsumers.cs`: `Handle(PaymentSucceeded)` artık `StartCheckout` yerine `StockCommitRequested(order.Id, order.BuyerId, items)` yayar. `PaymentFailed`→Cancel dalı değişmez.
- [x] T007 [US1] Basket consumer `src/services/basket/Basket.Api/OrderConsumers.cs` (YENİ): `Handle(OrderCompleted)` — kullanıcının sepetini `session.Delete` ile idempotent sil.
- [x] T008 [US1] Stock messaging `src/services/stock/Stock.Api/Extensions/MessagingExtensions.cs`: `stock.commit-requested` fanout declare + queue bind + listen; `StockCommitted`/`StockCommitFailed` publish→exchange; `opts.Discovery.IncludeType(typeof(Stock.Api.OrderConsumers))`.
- [x] T009 [US1] Order messaging `src/services/order/Order.Api/Extensions/MessagingExtensions.cs`: `StockCommitRequested` publish→exchange; `stock.committed`+`stock.commit-failed` fanout declare+bind+listen; `opts.Discovery.IncludeType(typeof(Order.Api.StockConsumers))`.
- [x] T010 [US1] Basket messaging `src/services/basket/Basket.Api/Extensions/MessagingExtensions.cs`: `OrderCompleted` fanout `basket.order-completed` bind+listen; `opts.Discovery.IncludeType(typeof(Basket.Api.OrderConsumers))`.

**Checkpoint**: Mutlu yol uçtan uca çalışır (eski Saga/ handler'lar henüz duruyor olabilir; cutover'da silinir).

---

## Phase 4: User Story 2 — Stok yetersizse atomik iptal (P1)

**Goal**: Yetersiz stokta hiçbir kalem düşmez (tek tx rollback), sipariş iptal, sepet korunur.

**Independent Test**: quickstart S2 — stok<sepet → callback success → Cancelled, stok değişmedi, sepet korundu.

- [x] T011 [US2] Stock hata dalı `src/services/stock/Stock.Api/OrderConsumers.cs`: `Commit` ×N sırasında biri hata dönerse `SaveChanges`/`Store` YAPMADAN çık → `StockCommitFailed(OrderId, STOCK_INSUFFICIENT)` yayar (iş hatası). Teknik hata (exception) yakalanmaz → Wolverine retry/DLQ.
- [x] T012 [US2] Order hata dalı `src/services/order/Order.Api/StockConsumers.cs`: `Handle(StockCommitFailed)` — order `Pending` değilse no-op; `order.Cancel(reasonCode)` + "ödendi-stok yok, manuel iade" uyarısı logla (iade kapsam dışı).

**Checkpoint**: Mutlu + yetersiz-stok yolları ikisi de doğru; atomiklik quickstart S2 ile kanıtlı.

---

## Phase 5: User Story 3 — Takılı adım görünür, watchdog yok (P2)

**Goal**: Watchdog olmadan takılma durable retry→DLQ (088) + `Pending` sorgusuyla görünür.

**Independent Test**: quickstart S4 — Stock geçici hata → DLQ'da görünür → replay → tamamlanır.

- [x] T013 [US3] Doğrula + gerekirse düzelt: order/stock/basket `MessagingExtensions.cs`'te DLQ (088) common handler + retry merdiveni yeni consumer'ları (StockConsumers/OrderConsumers) KAPSIYOR mu. Kapsıyorsa task = canlı doğrulama (quickstart S4); kapsamıyorsa eksik kayıt/retry policy'yi EKLE.

**Checkpoint**: Takılma teşhisi DLQ + sipariş durumuyla sağlanıyor.

---

## Phase 6: Cutover & Cleanup (cross-cutting — tek atomik PR)

**⚠️ Eski ve yeni yol aynı anda yaşayamaz. Bu faz US1-US2 bitince TEK commit grubu olarak koşar.**

- [x] T014 Sil `src/services/order/Order.Api/Saga/CheckoutConsumers.cs` + Order `MessagingExtensions`'tan eski checkout queue listen/publish + IncludeType(Saga.CheckoutConsumers) kaydı.
- [x] T015 Sil `src/services/stock/Stock.Api/Saga/CheckoutConsumers.cs` + Stock eski checkout queue wiring + IncludeType kaydı.
- [x] T016 Sil `src/services/basket/Basket.Api/Saga/CheckoutConsumers.cs` + Basket eski checkout queue wiring + IncludeType kaydı.
- [x] T017 [P] Domain test-first `tests/Stock.Api.Tests/ProductStockTests.cs`: `RevertCommit` testlerini sil; `Commit` yeterli/yetersiz(mutasyon yok)/idempotent testleri VAR mı doğrula, yoksa ekle (İlke VI).
- [x] T018 Sil `ProductStock.RevertCommit` + `RevertKey` + `"{orderId}:revert"` mantığı `src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs`; `STOCK_REVERT_INVALID`/`STOCK_REVERT_WITHOUT_COMMIT` kodlarını `Constants/StockResourceConstants.cs`'ten sil (`STOCK_INSUFFICIENT` KALIR).
- [x] T019 [G1] `order-saga` makine kimliği temizliği: grep'le `order-saga` client-credentials client (AgentPlatform AppRegistry config / IdP seed) + `SagaTokenHandler` (ve `stock.reserve`/`basket.write` statik scope demeti) checkout dışında KULLANILIYOR mu. Koreografi broker-event kullandığı, gRPC saga adımı kalmadığı için ölüyse sil; başka kullanım varsa dokunma + tasks notuna yaz. (ADR §5b kalıntısı.)
- [x] T020 Sil `src/others/Shared/CheckoutMessages.cs` komut/reply record'ları (StartCheckout, CommitStockCommand, eski StockCommitted, RevertCommitStockCommand, StockCommitReverted, ConfirmOrderCommand, OrderConfirmed, CancelOrderCommand, OrderCancelled, ClearBasketCommand, BasketCleared). `CheckoutItem`/`OrderAddress` koru; `ErrorClass` kullanımını grep'le — kullanılmıyorsa sil.
- [x] T021 Sil `RabbitMqConstants.Checkout.*` queue sabitleri `src/others/Shared/RabbitMqConstants.cs`.
- [x] T022 Sil `src/services/checkout/` dizini (Checkout.Orchestrator projesi + `FLOW.md`) ve varsa `tests/Checkout.*.Tests/`.
- [x] T023 `ECommerceAgent.slnx`'ten Checkout.Orchestrator + (varsa) test proje kayıtlarını çıkar.
- [x] T024 AppHost `src/aspire/AppHost/AppHost.cs` (veya Program): `Checkout.Orchestrator` servis kaydı + `checkoutDb` Postgres kaydını çıkar.
- [x] T025 [P] FLOW.md güncelle: `src/services/order/FLOW.md`, `src/services/stock/FLOW.md`, `src/services/basket/FLOW.md` yeni event zincirine göre (storm notasyonu, dikiş yönleri ⇨/⇦). İlke VII.
- [x] T026 [P] Yeni ADR `docs/adr/adr-checkout-choreography-migration.md`: 028/049 orchestration kararını tersine çevirir (gerekçe: ödeme-önden + atomik batch + az adım), watchdog feragatı, "ödendi-stok yok" iade borcu. `docs/adr/adr-checkout-saga-orchestration.md` status → "yerini aldı (092)". `docs/adr/README.md` güncelle.
- [x] T027 [P] `CLAUDE.md`: BC haritasından `checkout` satırı çıkar; order/stock/basket satırları + "Yapma listesi"ndeki "049 Checkout.Orchestrator istisna" + "kalan senkron kontrat = checkout gRPC" ifadeleri güncelle (basket `GetBasketItems` gRPC KALIR — start_payment kullanıyor).
- [x] T028 Anayasa PATCH amendment `.specify/memory/constitution.md`: İlke I v1.11.0 "orkestre saga broker komut/yanıtı" örneğini (049) "uykuda" işaretle (telemetri v1.12.0 + capability-link v1.12.1 emsali); versiyon + Sync Impact notu. İlke KALDIRILMAZ.

**Checkpoint**: Eski yol tamamen gitti; sistem yalnız koreografi.

---

## Phase 7: Polish & Doğrulama

- [x] T029 `dotnet build && dotnet test` — tüm çözüm yeşil (silinen referanslar temiz, yeni/düzenlenen domain testleri geçer).
- [x] T030 [P] `scripts/check-claude-spec-links.sh` — checkout origin spec'i haritadan çıktı, yeşil.
- [x] T031 [P] `scripts/check-flow-links.sh` — order/stock/basket FLOW.md anchor'ları kodla eşleşir, yeşil.
- [ ] T032 Aspire kaldır + quickstart S0-S4'ü Claude Desktop MCP ile canlı yürüt (sadeleşme, mutlu yol, atomik iptal, DLQ görünürlük).

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002-T003, BLOKE)** → **US1 (T004-T010)** → **US2 (T011-T012)** → **US3 (T013)** → **Cutover (T014-T028)** → **Polish (T029-T032)**.
- US1 ve US2 aynı iki dosyayı (Stock/OrderConsumers, Order/StockConsumers) paylaşır → US2 task'ları US1 task'larının ÜSTÜNE yazar (aynı dosya, sıralı; [P] yok).
- Cutover US1+US2 bitmeden BAŞLAMAZ (eski yol çalışır durumda kalmalı ki build + ara-doğrulama yapılabilsin); sonra tek grup halinde silinir.

### Parallel Opportunities
- T002 ‖ T003 (farklı Shared dosyaları).
- Cutover içinde T025 ‖ T026 ‖ T027 (farklı belge dosyaları); T017 ‖ (domain test, ayrı dosya).
- Polish T030 ‖ T031 (ayrı guard script'leri).

## Implementation Strategy

- **MVP = US1 (T001-T010):** mutlu yol koreografi çalışır; eski Saga/ handler'lar geçici olarak durur
  (çift yayın yok çünkü PaymentConsumers artık StartCheckout yaymıyor — orchestrator boşta kalır, zararsız).
- **US2 (T011-T012):** hata dalı; atomiklik kanıtı.
- **Cutover (T014-T027):** eski yolu + orchestrator'ı + checkoutDb'yi + eski sözleşmeleri sil, belge/governance hizala. Tek PR.
- **Her task sonrası commit; Cutover grubu birlikte commit.** Checkpoint'lerde dur, doğrula.

## Notes
- Wolverine TUZAĞI: her yeni `*Consumers` için `IncludeType` (çoğul, keşfedilmez) — unutulursa event sessiz yutulur.
- Soğuk-açılış: fanout binding'i tüketici kurar (T008/T009/T010).
- Wire message-type: elle publish'te AMQP `type` prefix'li tam ad (memory: wolverine-rabbitmq-wire-message-type).
