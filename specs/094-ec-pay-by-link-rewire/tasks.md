---
description: "Task list — EC pay-by-link rewire"
---

# Tasks: EC pay-by-link rewire (Ayşe kartla ödeme)

**Input**: `/specs/094-ec-pay-by-link-rewire/` (plan, spec, research, data-model, contracts, quickstart)

**Tests**: İlke VI Domain-TDD — saf domain (PaymentIntent davranışı, tutar hesabı) test-first ZORUNLU,
impl'den önce. Handler/endpoint/gRPC/webhook = altyapı, test-sonra/canlı (quickstart E2E).

**Organization**: User story bazlı; US1 tek başına MVP (uçtan uca satın alma).

## Phase 1: Setup (paylaşılan altyapı)

- [X] T001 [P] `PgPaymentLinkOptions` POCO (BaseUrl/StoreApiKey/CallbackSecret/MerchantId) + bind
  `AddOptions().BindConfiguration().ValidateDataAnnotations().ValidateOnStart()` `src/services/payment/Payment.Api/Options/PgPaymentLinkOptions.cs`
- [X] T002 [P] appsettings.json (non-secret: BaseUrl, MerchantId) + user-secrets (StoreApiKey, CallbackSecret) `src/services/payment/Payment.Api/appsettings.json`
- [X] T003 [P] Hata kodları: `STORE_NOT_LIVE`, `EMPTY_BASKET`, `INVALID_AMOUNT` `src/services/order/Order.Api/Constants/OrderResourceConstants.cs` + `CALLBACK_REJECTED`, `AMOUNT_MISMATCH` `src/services/payment/Payment.Api/Constants/PaymentResourceConstants.cs`
- [X] T004 Order→Payment gRPC proto `CreateIntentAndLink` (contracts/order-payment-grpc.md) + iki csproj'a `<Protobuf>` (server Payment.Api, client Order.Api)

---

## Phase 2: Foundational (bloke eden önkoşul — tüm story'lerden önce)

- [X] T005 [P] [domain test] `PaymentIntentTests`: `Create` amount≤0→INVALID_AMOUNT; `MarkSucceeded` Pending→Succeeded, tekrar Succeeded→no-op Ok (idempotent), Failed/Expired'den→error; `MarkFailed` benzeri idempotent; `AttachPgRef` Pending'de bir kez `tests/Payment.Api.Tests/PaymentIntentTests.cs`
- [X] T006 `PaymentIntent` aggregate: `Amount`/`OrderRef`/`PgPaymentRef` alanları + `Create`/`AttachPgRef`/`MarkSucceeded`/`MarkFailed` guard'ları (T005 yeşil); private parametresiz ctor + private setter (Marten record-VO deserialize tuzağı) `src/services/payment/Payment.Api/Domains/Payments/PaymentIntent.cs`

**Checkpoint**: PaymentIntent durum makinesi + idempotent geçişler yeşil → US1/US2 kurulabilir.

---

## Phase 3: User Story 1 - Ayşe sepetini kartla öder (P1) 🎯 MVP

**Goal**: start_payment → hosted link → kart → webhook başarı → Order Confirmed + stok. Uçtan uca satış.

**Independent Test**: Live merchant + dolu sepet → start_payment HostedUrl döner → PG Paid webhook →
Order Confirmed + stok düşer.

- [X] T007 [P] [US1] [domain test] Order tutar hesabı `StartPaymentAmountTests`: liste−aktif-indirim satır toplamı (kuruş); boş sepet→EMPTY_BASKET `tests/Order.Api.Tests/StartPaymentAmountTests.cs`
- [X] T008 [US1] `PgPaymentLinkClient` — REST `POST {BaseUrl}/payment-links` (X-Api-Key), istek/yanıt contracts/pg-payment-link.md `src/services/payment/Payment.Api/Grpc/PgPaymentLinkClient.cs`
- [X] T009 [US1] Payment S2S gRPC servisi `CreateIntentAndLink`: PaymentIntent.Create → PgPaymentLinkClient → AttachPgRef → hostedUrl dön (mantık gRPC class'ında, Features hop yok — conventions S2S) `src/services/payment/Payment.Api/Grpc/PaymentIntentGrpcService.cs`
- [X] T010 [US1] Order `PaymentIntentClient` gRPC client + DI + S2S makine-token `DelegatingHandler` (`AddGrpcClient...AddHttpMessageHandler`) `src/services/order/Order.Api/Grpc/PaymentIntentClient.cs` + `Program.cs`
- [X] T011 [US1] `start_payment` handler: PAYMENT_DISABLED kısa devreyi KALDIR; `BasketItemsClientProxy.GetItemsAsync` + `DiscountClient.GetActiveAsync` canlandır, tutar hesapla, Order Pending + Payment gRPC çağrısı, başarıda HostedUrl dön (commit-after-success: başarısızlıkta Order commit YOK) `src/services/order/Order.Api/Domains/Orders/Features/Agents/Commands/StartPayment.cs`
- [X] T012 [US1] Webhook alıcı `POST /payments/callback` + `CallbackSignatureValidator` (X-Signature HMAC-SHA256 ham gövde doğrula) + handler: pgPaymentRef eşle, status=Paid→MarkSucceeded→PaymentSucceeded yayınla, `[Transactional]` outbox `src/services/payment/Payment.Api/Domains/Payments/PaymentCallbackEndpointExtension.cs` + `CallbackSignatureValidator.cs`
- [X] T013 [US1] Order `GET /checkout/done` 200 stub (tarayıcı redirect düşer) `src/services/order/Order.Api/Domains/Checkout/CheckoutDoneEndpointExtension.cs`
- [X] T014 [US1] Wolverine keşfi: webhook handler + gRPC handler `*Consumers`/`*Handler` değilse `Program.cs` `IncludeType`; 092 PaymentConsumers/StockConsumers DOKUNULMADIĞINI doğrula (iki `Program.cs`)
- [X] T015 [US1] Dev config hizası: EC Payment.Api `:5080` + PG `StoreCallbackOptions.CallbackUrl`=`http://localhost:5080/payments/callback` (launchSettings/appsettings)

**Checkpoint**: US1 tek başına Ayşe'nin uçtan uca satın alımını canlı doğrular (quickstart S1-S3).

---

## Phase 4: User Story 2 - Hatalı/çift webhook bütünlüğü bozmaz (P2)

**Goal**: imza + idempotentlik + anti-tamper. Para/sipariş güvenliği.

**Independent Test**: aynı Paid webhook ×2 → tek Confirm; imzasız/yanlış imza → 404; tutar oynatılmış → red.

- [X] T016 [US2] Webhook guard'ları: `amount == PaymentIntent.Amount` değilse AMOUNT_MISMATCH red (anti-tamper); bilinmeyen pgPaymentRef → 404 fail-closed; imza geçersiz → 404 fail-closed `PaymentCallbackEndpointExtension.cs`
- [X] T017 [US2] İdempotent 200: aggregate no-op geçişte webhook 200 döner (PG durable retry durur); status=Failed→MarkFailed→PaymentFailed yolu `PaymentCallbackEndpointExtension.cs`
- [X] T018 [P] [US2] [domain test] (T005 kapsamıyorsa) MarkSucceeded/MarkFailed çift-çağrı no-op + terminal-koruma kenar testleri `tests/Payment.Api.Tests/PaymentIntentTests.cs`

**Checkpoint**: webhook retry/sahte/tamper'a dayanıklı.

---

## Phase 5: User Story 3 - Satışa kapalı mağazada ödeme başlamaz (P3)

**Goal**: STORE_NOT_LIVE'ı Ayşe'ye anlaşılır taşı, kirli order yok.

**Independent Test**: Live olmayan MerchantId → start_payment "mağaza satışa kapalı", HostedUrl yok, Pending Order yok.

- [X] T019 [US3] `PgPaymentLinkClient` PG `STORE_ONBOARDING_STORE_NOT_LIVE` yanıtını Result hatasına çevir; gRPC `is_success=false, error_code=STORE_NOT_LIVE`; start_payment anlaşılır mesaj taşır, Order/Intent commit etmez `PgPaymentLinkClient.cs` + `StartPayment.cs`

---

## Phase 6: Polish & cross-cutting

- [ ] T020 [P] `order` + `payment` FLOW.md: canlanan sticky'ler (🟦 start_payment, 🟪 PG-link policy, 🩷 PG, 🟧 PaymentSucceeded/Failed) + as-built kenar-anchor (İlke VII) `src/services/order/FLOW.md` + `src/services/payment/FLOW.md`
- [ ] T021 [P] CLAUDE.md BC haritası: Order `start_payment` canlandı + Payment webhook/link/intent-gRPC geri (093 "uykuda/söküldü" notlarını güncelle) `CLAUDE.md`
- [ ] T022 `dotnet build && dotnet test` + `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` (hepsi yeşil)
- [ ] T023 Canlı iki-host E2E (quickstart US1-US3 + S6 sır taraması İlke VIII) — tüm geliştirme bitince topluca; PG + EC AppHost ayakta, merchant Live

---

## Dependencies & sıra

- **Setup (T001-T004)** → **Foundational (T005-T006)** → story'ler.
- **US1 (T007-T015)** = MVP; T006'ya bağlı. T008/T009/T010 zincir (client→gRPC servis→Order client); T011 bunlara bağlı; T012 bağımsız (webhook) ama T006'ya bağlı.
- **US2 (T016-T018)** US1 webhook'u (T012) üstüne kurulur.
- **US3 (T019)** US1 link client'ı (T008) üstüne.
- **Polish (T020-T023)** en son; T023 her şeyden sonra.

## Paralel fırsatlar

- Setup: T001/T002/T003 [P] birlikte.
- Foundational test: T005 [P].
- US1 içi: T007 [P] (test) + T013 [US1 stub] diğerlerinden bağımsız; T008→T009→T010 sıralı.
- Polish: T020/T021 [P] birlikte.

## MVP kapsamı

**US1 (T001-T015)** = teslim edilebilir MVP: Ayşe uçtan uca kartla öder, sipariş onaylanır. US2/US3
sağlamlaştırma + kenar; sonra eklenir.
