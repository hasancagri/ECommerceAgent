---
description: "Task list for Checkout Orchestrator (049)"
---

# Tasks: Checkout Orchestrator (standalone orchestration-based saga)

**Input**: Design documents from `/specs/049-checkout-orchestrator/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/checkout-messages.md, quickstart.md

**Tests**: İlke VI (Domain-TDD) — saf domain birimleri (Payment iki-faz geçiş metotları, saga `On*`
karar metotları) test-first; test task'ı implementasyondan ÖNCE. Handler/endpoint/UI/wiring test-sonra.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşabilir (farklı dosya, bağımsız)
- **[Story]**: US1..US5 (spec kullanıcı hikâyeleri)

---

## Phase 1: Setup (Paylaşılan İskelet)

- [X] T001 `Checkout.Orchestrator` projesini oluştur `src/services/checkout/Checkout.Orchestrator/Checkout.Orchestrator.csproj` + `ECommerceAgent.slnx`'e ekle
- [X] T002 [P] AppHost'a ekle: `checkoutDb` + `checkout-orchestrator` (rabbit + orderApi/stockApi/basketApi/paymentApi WaitFor) `src/aspire/AppHost/AppHost.cs`
- [X] T003 [P] `CheckoutSchemaName` sabiti ekle `src/others/Common/Utils/Constants/SchemaConstants.cs`
- [X] T004 [P] `Properties/launchSettings.json` (Development env + http/https profil) — 048 dersi `src/services/checkout/Checkout.Orchestrator/Properties/launchSettings.json`
- [X] T005 [P] `GlobalUsings.cs` + `Constants/CheckoutResourceConstants.cs` iskeleti `src/services/checkout/Checkout.Orchestrator/`

---

## Phase 2: Foundational (Tüm Hikâyeleri Bloke Eder)

**Amaç**: Broker altyapısı + 028 sökümü + Payment iki-faz domain — hiçbir US bunlarsız koşamaz.

### Sözleşme + altyapı

- [X] T006 [P] Broker komut/yanıt kayıtları (zarf: `CheckoutId`, `IdempotencyKey`, `Success`, `ErrorClass`) `src/others/Shared/CheckoutMessages.cs` (bkz. contracts/checkout-messages.md)
- [X] T007 [P] Checkout exchange/queue adları `src/others/Shared/RabbitMqConstants.cs`
- [X] T008 [P] Kullanıcı scope `checkout.write` sabiti (KnownScopes + `customer` rol demeti) — giriş endpoint için `src/others/Common/Utils/Constants/AuthorizationScopes.cs` + `src/others/Identity.Server/Rbac/KnownScopes.cs`. Not: broker komutları scope-guard DEĞİL (HttpContext yok), makine scope'u gereksiz.
- [ ] T009 ~~m2m client seed~~ — GEREKSİZ (orchestrator HTTP/gRPC çağırmaz; broker connection auth Aspire'dan). Atlandı.
- [X] T010 Wolverine+Marten durable saga config (`IntegrateWithWolverine`, `UseDurableLocalQueues`, outbox) + RabbitMQ transport + tüketici-tarafı binding `src/services/checkout/Checkout.Orchestrator/Program.cs`
- [X] T011 [P] `Checkout` options (watchdog/retry değerleri) — m2m token YOK `src/services/checkout/Checkout.Orchestrator/Options/`

### 028 söküm (full replace — FR-002)

- [X] T012 Sil `src/services/order/Order.Api/Sagas/CheckoutSaga.cs` (+ saga mesaj kayıtları)
- [X] T013 Sil `src/services/order/Order.Api/Grpc/StockCommitClientProxy.cs`, `BasketClearClientProxy.cs`, `SagaTokenHandler.cs` + Order `Program.cs` gRPC istemci wiring'i
- [X] T014 Order'da 028 saga tetikleyicilerini kaldır: `CreateOrder.cs` `StartCheckout` yayını + `Checkout` options + **`PaymentOrderCreator.cs` `StartCheckout` yayını** (039 chat/reconcile). Not: `SagaTokenHandler` + 039 gRPC istemcileri (BasketItems/CustomerContext/PaymentGateway) SİLİNMEZ (039 chat başka amaçla kullanır). `src/services/order/Order.Api/Domains/`
- [X] T014b 039 chat sipariş-tamamlama orchestrator'a devredildi — **KARAR A** (kullanıcı): chat A2A çekimi KALIR (dış PG), `PaymentOrderCreator` siparişi oluşturur + orchestrator'ı `PaymentMode.AlreadyCaptured` + `OrderId` dolu ile `checkout.start` kuyruğuna tetikler → orchestrator authorize/capture ATLAR, yalnız stok commit + onay + sepet temizler (çift-tahsilat yok). `src/services/order/Order.Api/Domains/PaymentAttempts/PaymentOrderCreator.cs`

### Payment iki-faz domain (test-first — İlke VI)

- [X] T015 [P] Test: `Payment.Authorize/Capture/Void` geçiş guard'ları (Authorized→Captured|Voided; terminal; idempotent no-op) `tests/Payment.Api.Tests/PaymentTwoPhaseTests.cs`
- [X] T016 Payment aggregate genişlet: `PaymentState` enum + `Authorize/Capture/Void` (ResultDomain, PSP stub) — T015'i geçir `src/services/payment/Payment.Api/Domains/Payments/Payment.cs`

**Checkpoint**: Altyapı + sözleşme + Payment domain hazır; 028 sökülü; build PASS.

---

## Phase 3: User Story 1 — Başarılı checkout uçtan uca (P1) 🎯 MVP

**Amaç**: Mutlu yol — CreateOrder→Authorize→Commit→Capture→Confirm→ClearBasket.
**Bağımsız test**: Geçerli rezervasyonlu sepetle POST → Order Confirmed, Stock düştü, Payment Captured, sepet boş.

- [~] T017 KALDIRILDI — kullanıcı kararı: saga full-inline (On*/NextStep dolaylaması yok), 7 test silindi. İlke VI (saga On* test-first) SAPMASI: gerekçe = okunabilirlik tercihi; saga build + canlı smoke ile doğrulanır. (Anayasa/plan güncellemesi gerekebilir.)
- [X] T018 [US1] `CheckoutProcess.cs` — full-inline saga: her Handle net ilerler (yanıt→sonraki komut); retry Wolverine policy'de (saga'da değil); telafi açık aşama zinciri; `CheckoutPhases` string sabit `src/services/checkout/Checkout.Orchestrator/Sagas/CheckoutProcess.cs`
- [X] T019 [P] [US1] Giriş endpoint `POST /api/v1/checkout` (`checkout.write` kullanıcı scope) + `StartCheckout` komutu (idempotent anahtar `UserId+BasketId`) `src/services/checkout/Checkout.Orchestrator/Domains/Checkout/`
- [X] T020 [P] [US1] Order broker handler: `CreateOrderCommand`→`OrderCreated` (Pending) `src/services/order/Order.Api/Domains/Orders/Features/Commands/`
- [X] T021 [P] [US1] Payment broker handler: `AuthorizePaymentCommand`→`PaymentAuthorized` + `CapturePaymentCommand`→`PaymentCaptured` `src/services/payment/Payment.Api/Domains/Payments/Features/Commands/`
- [X] T022 [P] [US1] Stock broker handler: `CommitStockCommand`→`StockCommitted` (mevcut `CommitStock` sarar) `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/`
- [X] T023 [P] [US1] Order broker handler: `ConfirmOrderCommand`→`OrderConfirmed` (**pivot**) `src/services/order/Order.Api/Domains/Orders/Features/Commands/`
- [X] T024 [P] [US1] Basket broker handler: `ClearBasketCommand`→`BasketCleared` (mevcut clear sarar) `src/services/basket/Basket.Api/Domains/`
- [X] T025 [US1] WebApp `Order/Create` OnPost → orchestrator `POST /api/v1/checkout`; Payment ön-yaratımı + Order POST kaldır; `IsReservationExpired` guard KALIR `src/ui/WebApp/Pages/Order/Create.cshtml.cs` + `Services/OrderService.cs`
- [ ] T026 [US1] Inbox/dedup temel doğrulama (Wolverine mesaj store) + idempotent başlatma no-op + **terminal saga (Completed/Cancelled) geç-mesaj no-op handler** (FR-026, `NotFound()` deseni) `src/services/checkout/Checkout.Orchestrator/Sagas/CheckoutSaga.cs`

**Checkpoint**: US1 bağımsız teslim edilebilir — mutlu checkout uçtan uca çalışır (SC-001, SC-007).

### Phase 3b: Chat entegrasyonu — KARAR A ile çözüldü (agent-slice YOL DEĞİL)

Erken tasarım (T047-49) "tıkla/yaz aynı, agent slice = ince tetikleyici, chat de mock iki-faz" idi.
Implement'te çıktı: chat ödemesi dış PaymentGateway A2A ile ÖNCEDEN çekiliyor → mock iki-faz'a sokmak
çift-tahsilat. Kullanıcı **KARAR A**: chat A2A'da kalır, orchestrator `AlreadyCaptured` modda tamamlar.
Sonuç: ayrı agent slice / ChatAgent MCP rerouting / A2A söküm GEREKMEZ. Chat girişi mevcut
`PlaceOrderForAgent` (Order MCP) kalır; yalnız `PaymentOrderCreator` orchestrator'ı tetikler (T014b).

- [~] T047 GEREKSİZ — `Installments` StartCheckout'a eklendi (T006/T018); mock Payment iki-faz taksiti kaydetmez (öğrenme maketi). Ayrı iş yok.
- [~] T048 GEREKSİZ (Karar A) — orchestrator'da agent slice açılmaz; chat Order MCP `PlaceOrderForAgent` üzerinden gider.
- [~] T049 GEREKSİZ (Karar A) — ChatAgent değişmez; A2A çekim korunur; reroute `PaymentOrderCreator`→orchestrator (AlreadyCaptured) ile yapıldı (T014b).

---

## Phase 4: User Story 2 — Pivot öncesi temiz geri sarma (P1)

**Amaç**: Commit başarısızlığında LIFO revert + Void + Cancel (asla refund).
**Bağımsız test**: Kalem rezervasyonunu commit öncesi düşür → Order Cancelled, kalemler geri, Payment Voided, sepet korunur.

- [ ] T027 [P] [US2] Test: telafi `On*` (`OnCommitResult` fail→Compensating, `OnStepFailed` permanent pre-pivot→Compensating, LIFO sıra, `OnRevertResult`, terminal Cancelled) `tests/Checkout.Orchestrator.Tests/CheckoutSagaCompensationTests.cs`
- [ ] T028 [US2] `CheckoutSaga.cs` — `Compensating` fazı + `CompensateCheckout` handler (RevertCommit LIFO → Void → Cancel) — T027'yi geçir
- [ ] T029 [P] [US2] Stock broker handler: `RevertCommitStockCommand`→`StockCommitReverted` (mevcut `RevertCommit` sarar, idempotent) `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/`
- [ ] T030 [P] [US2] Payment broker handler: `VoidPaymentCommand`→`PaymentVoided` `src/services/payment/Payment.Api/Domains/Payments/Features/Commands/`
- [ ] T031 [P] [US2] Order broker handler: `CancelOrderCommand`→`OrderCancelled(reason)` `src/services/order/Order.Api/Domains/Orders/Features/Commands/`
- [ ] T032 [US2] Idempotent telafi doğrulama: redelivery → tek revert (SC-006, SC-002) + WebApp "Siparişlerim"de iptal sebebi görünür doğrula (FR-028; mevcut sipariş listesi reason gösteriyor mu, değilse küçük UI ekle) `src/ui/WebApp/Pages/Order/`

**Checkpoint**: US1+US2 — mutlu yol + pivot öncesi tutarlı telafi.

---

## Phase 5: User Story 3 — Pivot sonrası onaylı sipariş korunur (P1)

**Amaç**: Capture+Confirm sonrası ClearBasket düşse bile Order Confirmed KALIR.
**Bağımsız test**: ClearBasket'i başarısız tetikle → Order Confirmed kalır, retry, sonunda Completed (iptal yok).

- [ ] T033 [P] [US3] Test: pivot kuralı `On*` (post-Confirm ClearBasket fail→retry then Completed, ASLA Cancelled; post-pivot timeout→no-op) `tests/Checkout.Orchestrator.Tests/CheckoutSagaPivotTests.cs`
- [ ] T034 [US3] `CheckoutSaga.cs` — pivot guard (post-pivot Cancelled yasak) + ClearBasket sınırlı retry + log-and-complete — T033'ü geçir

**Checkpoint**: US1-US3 — en yıkıcı tutarsızlıklar (para alındı/sipariş kayboldu, onaylı iptal) imkânsız (SC-003).

---

## Phase 6: User Story 4 — Broker dayanıklılığı (P2)

**Amaç**: Geçici kesintide komut kuyrukta bekler; geçici↔kalıcı hata ayrımı; backoff + DLQ.
**Bağımsız test**: Hedef servisi süreç ortasında durdur → orchestrator askıda kalmaz; servis dönünce tamamlanır.

- [ ] T035 [P] [US4] Test: hata sınıflandırma `On*` (Transient→retry+backoff, Permanent→faz'a göre telafi/log) `tests/Checkout.Orchestrator.Tests/CheckoutSagaErrorClassTests.cs`
- [ ] T036 [US4] Retry+backoff policy + DLQ config `src/services/checkout/Checkout.Orchestrator/Program.cs`
- [ ] T037 [US4] `CheckoutTimedOut` handler + watchdog faz-guard (pre-pivot→compensate, post-pivot→no-op) + per-step timeout `ScheduleAsync` `src/services/checkout/Checkout.Orchestrator/Sagas/CheckoutSaga.cs`

**Checkpoint**: US1-US4 — temporal decoupling + zehirli mesaj yönetimi (SC-004).

---

## Phase 7: User Story 5 — Süreç sahibi restart'a dayanır (P2)

**Amaç**: Orchestrator restart'ında saga kaldığı adımdan devam; kayıp/çift-işleme yok.
**Bağımsız test**: Süreç ortasında orchestrator öldür+restart → nihai durum tek ve doğru.

- [ ] T038 [P] [US5] Test: saga rehydration + redelivery idempotency (faz-guard + idempotency çift-işlemeyi önler) `tests/Checkout.Orchestrator.Tests/CheckoutSagaRestartTests.cs`
- [ ] T039 [US5] Durable saga persistence (Marten) doğrula — bellek-içi-only state YOK; bekleyen komutlar restart sonrası yeniden teslimde bozulmaz (SC-005)

**Checkpoint**: Tüm P1+P2 hikâyeler tamam.

---

## Phase 8: Polish & Cross-Cutting

- [ ] T040 [P] Yeni `checkout/FLOW.md` (BC domain süreci, EventStorming altitude) `src/services/checkout/FLOW.md`
- [ ] T041 [P] Güncelle `src/services/order/FLOW.md` + `src/services/payment/FLOW.md` (süreç değişti — aynı PR, İlke VII)
- [ ] T042 [P] CLAUDE.md BC haritasına `checkout-orchestrator` satırı + origin spec 049 ekle `CLAUDE.md`
- [ ] T043 `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` PASS
- [ ] T044 `dotnet build` + `dotnet test` tüm çözüm PASS
- [ ] T045 [P] Quickstart senaryo 1-5 canlı doğrulama (Aspire AppHost) `specs/049-checkout-orchestrator/quickstart.md`
- [ ] T046 [P] (Opsiyonel) E2E Playwright checkout mutlu yol `tests/E2E/`

---

## Dependencies & Execution

- **Setup (P1)** → **Foundational (P2)** → hikâyeler. Foundational tüm US'leri bloke eder.
- **028 söküm (T012-T014)** foundational'da; US implementasyonundan önce (iki süreç koşmasın, FR-002).
- **Payment domain (T015-T016)** foundational; US1 (authorize/capture) + US2 (void) tüketir.
- **US1 (P1)** = MVP. US2/US3 US1 saga'sını genişletir (aynı `CheckoutSaga.cs` → sıralı, paralel değil).
- **US4/US5 (P2)** US1-US3 sonrası; dayanıklılık katmanı.
- Test-first (İlke VI): T015→T016, T017→T018, T027→T028, T033→T034, T035→(37), T038→T039.

### Paralel fırsatlar

- Setup: T002-T005 paralel.
- Foundational sözleşme: T006-T009 paralel (farklı dosya); T010-T011 sonra.
- US1 broker handler'ları T020-T024 paralel (farklı BC dosyaları); T018 saga çekirdeği önce.
- US2 handler'ları T029-T031 paralel.
- Polish T040-T042, T045-T046 paralel.

### MVP kapsamı

**US1 (Phase 1-3)** — mutlu yol checkout uçtan uca. Bağımsız gösterilebilir, teslim edilebilir.
US2-US3 tutarlılık garantilerini, US4-US5 dayanıklılığı ekler.