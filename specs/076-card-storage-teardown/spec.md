# Feature Specification: Kart Saklama Söküm (Store + PG)

**Feature Branch**: `076-card-storage-teardown` · **Created**: 2026-09-12 · **Status**: Onaylandı

**Input**: Kart saklamadan vazgeçildi (canlı test: iyzico ödemesiz-hosted-save yok; karmaşıklık > getiri
demo ölçeğinde). Kart-saklama + saved-card-charge + taksit tool'ları **iki repodan da** sökülür. Yeni ödeme
yönü hosted-CF (ayrı sonraki spec). Bkz [[hosted-cf-checkout-pivot]].

**Test**: YOK (kullanıcı kararı — removal; doğrulama = `dotnet build` yeşil + ilgili testler silinir).

## Amaç

Kart-saklama özelliğini (kayıtlı kart + saved-card ile çekim + taksit) store ve PaymentGateway'den tamamen
kaldır. Kart-saklama mevcut **ödeme yöntemi** olduğundan, ona kuple **agent charge yolu** da sökülür →
**checkout geçici boşlukta** (hosted-CF sonraki spec'te geri getirir; kullanıcı kabul etti).

## Kapsam — REMOVE

### Store (ECommerceAgent)
- **Customer.Api:** `Domains/Wallets/*` (Wallet aggregate, SavedCard, WalletMcpTools/list_cards,
  GetCardsForAgent, GetPaymentContextForAgent, WalletEndpointExtension payment-context ucu) ·
  `Infrastructure/Tokenization/*` (ICardTokenizer, GatewayCardTokenizer, MerchantTokenProvider) ·
  `Options/DropShopVaultOption` · Program.cs + GlobalUsings kayıtları · appsettings `DropShopVault`.
- **Order.Api:** `Domains/PaymentAttempts/*` · `Http/*` (PaymentGatewayClient, CustomerPaymentContextClient,
  MerchantKeyClient) · `Process/PaymentReconcileHandler` · `Domains/Orders/Features/Agents/PlaceOrderForAgent`
  + `place_order` MCP tool · `Options` (PaymentGatewayOption, CustomerContextOption, CheckoutReconcile,
  CorrelationKeyOption) · `Grpc/*` (SagaTokenHandler, BasketItemsClientProxy — yalnız charge yolu kullanıyordu)
  · Program.cs + GlobalUsings temizliği.
- **Testler:** WalletTests, SavedCardBinTests (Customer.Api.Tests) · PaymentAttemptTests, CorrelationKeyTests
  (Order.Api.Tests).
- **Shared:** kart/charge'a özel eklemeler yoksa dokunma (CheckoutMessages KALIR — saga kullanır).

### PaymentGateway (PG)
- **Payment.Api:** `Domains/StoredCards/*` (StoredCard, CardSession, CardVaultEndpointExtension,
  CardAssociationMapper, ValueObjects/CardInformation, Features: StartCardSession/CompleteCardSession/
  ListCards/DeleteCard) · `Domains/Payments` saved-card yolu: `ChargePayment`, `RetrievePayment`,
  `InstallmentOptions` + `Features/Agents/*` (ChargeSavedCardForAgent, InstallmentOptionsForAgent) ·
  `PaymentMcpTools` (charge_saved_card + **installment_tool**) · `Payment` aggregate (charge kaydı) ·
  Program.cs + GlobalUsings + Marten kayıtları temizliği · card hata sabitleri (CardVaultResourceConstants).
- **Testler:** CardSessionTests, PaymentChargeTests, StoredCard*Tests, CardInformationValueObjectTests.

## Kapsam — KEEP

- **Store:** `AddressBook` + `MerchantInformation` (+ merchant admin /mcp-admin tool'ları) · Order aggregate
  + `GetOrders` + `Saga/OrderEventHandlers` (checkout saga katılımı) · Checkout.Orchestrator saga.
- **PG:** iyzico V2 wire (`Utils/*V2` — hosted-CF ödemede repurpose) · Merchant BC (onboarding/approve/status)
  · Commission BC · Identity/Admin BFF/gateway.

## Beklenen sonuç (Success Criteria)

- **SC-001:** `dotnet build` iki repoda da 0 hata; silinen tiplere referans kalmaz.
- **SC-002:** Kart/vault/saved-card-charge/taksit yüzeyi kalmaz (grep: `SavedCard`/`StoredCard`/`list_cards`/
  `installment`/`vaultToken`/`cardUserKey` = 0, yorumlar hariç).
- **SC-003:** Checkout saga + Order/Payment-olmayan aggregate + iyzico wire + Merchant/Commission derlenir kalır.
- **SC-004:** Agent yüzeyinde kart/taksit/charge_saved_card/place_order tool'u görünmez.

## Assumptions

- Checkout **geçici boşlukta** — `place_order` + charge sökülür; hosted-CF sonraki spec geri getirir (onaylı).
- PG merchant/commission/iyzico-wire durur (hosted-CF onları kullanacak).
- Silinen sandbox merchant/kart verisi terk (demo).

## Dependencies

- Sonraki: hosted-CF checkout spec (ödeme linki→başarılı→saga). Bu teardown onun ön-koşulu (temiz zemin).
