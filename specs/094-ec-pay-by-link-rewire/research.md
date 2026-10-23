# Research: EC pay-by-link rewire

Faz 0 — bilinmeyenleri çöz. PG kontratları birinci elden (PG repo kaynak + 091 E2E) doğrulandı.

## D1 — BC bölünmesi: orkestrasyon Order, ödeme-kenarı Payment

- **Karar**: `start_payment` (Order BC MCP tool) orkestratördür: sepet+indirim+tutar hesaplar, Order
  Pending yaratır, **Payment BC'ye S2S gRPC** ile intent+link ister, dönen hosted URL'yi döndürür.
  PaymentIntent + EC→PG link client + PG→EC webhook alıcı = **Payment BC** (PaymentSucceeded/Failed
  yayıncısı zaten orada).
- **Gerekçe**: PaymentIntent Payment BC aggregate'i (İlke I — modeli Order'a sızdırma). PG-link + webhook,
  fanout yayıncısıyla aynı BC'de kohezyon. Order ince kalır (sepet/indirim/koreografi).
- **Reddedilen**: her şeyi Order'da yapıp PG'yi Order'dan çağırmak → PaymentIntent'i Order'a taşır,
  İlke I ihlali + 092 fanout yayıncısını böler.

## D2 — EC→PG link client (REST)

- **Karar**: Payment.Api'de `PgPaymentLinkClient` → `POST {PgBaseUrl}/payment-links`, header
  `X-Api-Key: {StoreApiKey}`, gövde `{merchantId, amount, currency:"TRY", orderRef, returnUrl, storeName,
  orderSummary}` → `{linkUrl, pgPaymentRef}`. `merchantId` = config sabit MerchantId; `orderRef` = OrderId.
- **Gerekçe**: PG kontratı (089 CreatePaymentLink, E2E'de doğrulandı). Dış-sağlayıcı sınırı REST.
- **STORE_NOT_LIVE**: PG `isSuccess:false, code:STORE_ONBOARDING_STORE_NOT_LIVE` döner → client bunu
  Result hatasına çevirir → start_payment Ayşe'ye "mağaza satışa kapalı" taşır (FR-011). Pending order
  bu durumda kalıcı olmamalı (rollback/atılmamış).

## D3 — PG→EC webhook alıcı (kesin kontrat)

- **Karar**: Payment.Api `POST /payments/callback` (anonim + HMAC). Ham gövde (camelCase, PG
  `StoreResultDelivery` birebir): `{pgPaymentRef, orderRef, status, amount, currency, bankCode, reasonCode}`.
  Header `X-Signature` = **HMAC-SHA256(CallbackSecret, raw_body) lowercase-hex**. `status` = `"Paid"`
  (başarı) | `"Failed"` (başarısızlık) — ikisi de webhook atar.
- **Eşleme + guard**: `pgPaymentRef` ile PaymentIntent bul; `amount == PaymentIntent.Amount` (anti-tamper);
  imza/eşleşme başarısız → **fail-closed** (durum değişmez, bilgi sızdırmaz). `"Paid"`→MarkSucceeded→
  PaymentSucceeded; `"Failed"`→MarkFailed→PaymentFailed.
- **İdempotentlik**: MarkSucceeded/MarkFailed aggregate durum guard'ı (terminal'de no-op) → webhook retry
  güvenli (PG 2xx-dışında durable retry yapar).
- **Secret**: EC `CallbackSecret` = PG `StoreCallbackOptions.CallbackSecret` (aynı değer; dev
  `dev-sim-callback-secret-0909`). İmza bayt-bayt ham gövde üzerinden — ortadaki JSON yeniden
  serialize edilmeden doğrulanır (PG raw_body'yi imzalar).

## D4 — Order→Payment S2S gRPC (intent+link)

- **Karar**: Payment.Api gRPC ucu `CreateIntentAndLink(orderId, amount, currency, storeName, orderSummary,
  returnUrl)` → `{hostedUrl}`. Mantık **gRPC servis class'ında** (tek çağıran Order; Features hop yok —
  conventions S2S kuralı). PaymentIntent yaratır (Amount=amount), PgPaymentLinkClient çağırır, pgPaymentRef'i
  intent'e yazar, hostedUrl döner. Client auth = mevcut S2S makine-token DelegatingHandler.
- **Gerekçe**: 093'te sökülen "Order S2S intent gRPC" dönüşü; BC-arası S2S = gRPC (İlke I).

## D5 — tutar hesabı (liste − aktif indirim)

- **Karar**: Order `start_payment` içinde `BasketItemsClientProxy.GetItemsAsync` (sepet kalemleri + liste
  fiyatı) + `DiscountClient.GetActiveAsync` (kalem/ürün başına aktif yüzde) → satır toplamı Σ(liste×(1−%)).
  Adres chat-only'de atlanır (AddressClient UYKUDA kalır).
- **Gerekçe**: tasarım item 1; Discount yüzde otoritesi (fiyat tutmaz, tüketici hesaplar — CLAUDE).
- **Açık/küçük**: yuvarlama = kuruş (2 ondalık), satır-bazında yuvarla sonra topla (tasks netleştirir).

## D6 — ReturnUrl stub + config

- **Karar**: Order.Api `GET /checkout/done` → 200 düz metin/HTML (tarayıcı redirect düşer; gerçek durum
  webhook'tan). ReturnUrl = `{EcBaseUrl}/checkout/done`, PG link isteğinde geçilir.
- **Config (Options pattern)**: Payment.Api `PgPaymentLinkOptions{ BaseUrl, StoreApiKey, CallbackSecret,
  MerchantId }` (sabit localhost port, SD değil). `IConfiguration` doğrudan okuma YOK. Secret'lar
  user-secrets. EC Payment.Api portu PG `StoreCallbackOptions.CallbackUrl`=`http://localhost:5080/payments/callback`
  ile uyumlu olmalı (Payment.Api :5080 dinler ya da PG config EC portuna ayarlanır — dev koordinasyonu).

## D7 — Wolverine keşfi

- **Karar**: Yeni webhook handler / S2S handler `*Consumers`/`*Handler` ile bitmiyorsa Program.cs
  `opts.Discovery.IncludeType(typeof(X))`. 092 PaymentConsumers/StockConsumers zaten kayıtlı — dokunulmaz.

## Açık (clarify/tasks'e taşınan, küçük)

- Tutar yuvarlama kesin kuralı (kuruş; satır vs toplam) — tasks netleştirir, demo etkisi minimal.
- Boş sepet / PG-hatasında Order Pending temizliği: atılmamış mı yoksa Cancel mi — data-model'de karar.
