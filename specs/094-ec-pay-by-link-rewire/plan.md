# Implementation Plan: EC pay-by-link rewire (Ayşe kartla ödeme)

**Branch**: `094-ec-pay-by-link-rewire` | **Date**: 2026-10-23 | **Spec**: [spec.md](spec.md)

## Summary

EC'yi PG ödeme sağlayıcısına yeniden kablolayıp 093'te sökülen ödeme girişini pay-by-link kontratıyla
canlandır. İş iki EC BC'ye yayılır: **Order** (start_payment orkestrasyonu + 092 koreografi tetiği) ve
**Payment** (PaymentIntent + EC→PG link client + PG→EC HMAC webhook alıcı + Order↔Payment S2S gRPC).
PG dokunulmaz; PaymentIntent aggregate + PaymentSucceeded/Failed fanout + 092 zaten mevcut (uykuda),
yeniden kullanılır. Kart PII hiçbir EC yüzeyinde görünmez (İlke VIII merkezi kısıt).

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings)
**Primary Dependencies**: Marten (Postgres doc/event store, Newtonsoft), Wolverine (in-proc bus +
RabbitMQ fanout), gRPC (BC-arası S2S), Minimal API, MCP (start_payment tool)
**Storage**: Postgres — orderDb (Order BC) + paymentDb (Payment BC); DB paylaşımı YOK
**Testing**: xUnit + Shouldly, saf unit (aggregate davranışı test-first); Marten/HTTP/gRPC yolları
canlı Aspire + quickstart E2E ile doğrulanır (İlke VI; integration harness yok)
**Target Platform**: Aspire AppHost orkestralı servisler; dev çok-host (EC + PG ayrı host)
**Project Type**: Mikroservis / BC-per-service (mevcut yapı)
**Performance Goals**: etkileşimli ödeme başlatma; ağır yük hedefi yok (demo ölçeği)
**Constraints**: NonSecure (3D yok), yalnız TRY, chat-only dönüş, SimBank; kart PII %0 EC yüzeyi;
webhook idempotent + imza + anti-tamper; EC→PG sabit localhost port (cross-host SD değil)
**Scale/Scope**: iki BC, bir MCP tool canlandırma, bir webhook ucu, bir S2S gRPC ucu, 1 config POCO/BC

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

| İlke | Değerlendirme | Sonuç |
|---|---|---|
| **I. BC İzolasyonu** | Order↔Payment = sanksiyonlu S2S gRPC (DB değil API). EC↔PG = REST + HMAC webhook (dış-sağlayıcı/3. taraf callback istisnası — gRPC desteklemez). PaymentSucceeded/Failed = fanout integration event (İlke I birincil kanalı). DB paylaşımı yok. | PASS |
| **II. Zengin Aggregate** | PaymentIntent durum makinesi (MarkSucceeded/MarkFailed, amount + idempotency guard aggregate içinde). Order.Confirm/Cancel mevcut. Yeni invariant → aggregate metoduna. | PASS |
| **III. VSA+CQRS, Repo yok** | start_payment = `Order Agents/Commands/StartPayment` slice (ince MCP sarmalayıcı). Webhook alıcı = Payment Minimal API endpoint→handler. Order→Payment tek-çağıran S2S → mantık gRPC servis class'ına gömülür (Features hop yok). `IDocumentSession` doğrudan. | PASS |
| **IV. Result Pattern** | Handler `FeatureObjectResultModel<T>`; aggregate `ResultDomain`. Beklenen hata (STORE_NOT_LIVE, boş sepet, imza-red) = Result, exception değil. | PASS |
| **V. Scope Yetki** | start_payment müşteri yüzeyi (authenticated customer MCP). Webhook ucu anonim + HMAC (File kapak callback deseni). Yeni admin scope YOK. | PASS |
| **VI. Domain-TDD** | PaymentIntent guard'ları (idempotent geçiş, amount eşitlik) test-first. Amount hesabı (liste−indirim) saf birim test-first. Webhook imza/HTTP/gRPC = altyapı (test-sonra/canlı). | PASS |
| **VII. FLOW.md Legibility** | order + payment FLOW.md'ye canlanan sticky'ler (🟦 start_payment, 🟪 PG-link policy, 🟧 PaymentSucceeded/Failed, 🩷 PG). Storm + as-built anchor. Aynı PR. | PASS (task) |
| **VIII. Sır/Kimlik İnsan-Yüzeyde Görünmez** | Kart PII yalnız PG hosted sayfada; EC yalnız opak pgPaymentRef görür. StoreApiKey/CallbackSecret config'te (log/trace/dönüşe girmez). Merkezi anti-hedef. | PASS |

**Violations**: yok → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/094-ec-pay-by-link-rewire/
├── plan.md              # bu dosya
├── research.md          # Faz 0 — kontrat/karar çözümleri
├── data-model.md        # Faz 1 — PaymentIntent + Order durumları
├── quickstart.md        # Faz 1 — uçtan uca doğrulama (PG + EC ayakta)
├── contracts/           # Faz 1 — PG link REST, PG→EC webhook, Order→Payment gRPC
└── tasks.md             # Faz 2 (/speckit-tasks — bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/order/Order.Api/
├── Domains/Orders/Features/Agents/Commands/StartPayment.cs   # kısa devre KALDIR → orkestrasyon
├── Grpc/BasketItemsClientProxy.cs                            # UYKU → canlandır (sepet kalemleri)
├── Grpc/DiscountClient.cs                                    # UYKU → canlandır (aktif indirim)
├── Grpc/PaymentIntentClient.cs                               # YENİ (Order→Payment S2S client)
├── Domains/Checkout/CheckoutDoneEndpointExtension.cs         # YENİ GET /checkout/done 200 stub
├── Saga|Process? (hayır — 092 PaymentConsumers/StockConsumers DOKUNULMAZ)
└── Program.cs                                                # gRPC client kaydı + Wolverine IncludeType

src/services/payment/Payment.Api/
├── Domains/Payments/PaymentIntent.cs                         # MEVCUT — guard'lar test-first pekiştir
├── Domains/Payments/Features/.../CreateIntentAndLink (gRPC)  # YENİ — S2S servis class'ında mantık
├── Domains/Payments/PaymentCallbackEndpointExtension.cs      # YENİ POST webhook alıcı + HMAC
├── Domains/Payments/CallbackSignatureValidator.cs            # YENİ (093 dönüşü)
├── Grpc/PgPaymentLinkClient.cs                               # YENİ EC→PG POST /payment-links (REST)
├── Options/PgPaymentLinkOptions.cs                           # YENİ (BaseUrl/StoreApiKey/CallbackSecret/MerchantId)
└── Program.cs                                                # endpoint + options + Wolverine IncludeType

tests/Order.Api.Tests/ , tests/Payment.Api.Tests/             # aggregate/guard testleri (test-first)
```

**Structure Decision**: Mevcut BC-per-service VSA korunur. Order = orkestrasyon + 092 tüketici (değişmez);
Payment = ödeme niyeti + PG kenarı (link client + webhook + S2S gRPC). Yeni senkron çağrı yalnız
sanksiyonlu (Order→Payment S2S gRPC; EC→PG dış-sağlayıcı REST). `start_payment` adı dış sözleşme — korunur.

## Complexity Tracking

> İhlal yok — boş.
