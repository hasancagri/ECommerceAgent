# Tasks: PG Pivot — EC Söküm (093)

**Input**: `specs/093-pg-pivot-teardown/spec.md` + onaylı söküm envanteri
(`~/.claude/plans/s-k-me-ba-layal-m-kapsam-birlikte-steady-goose.md`)

**Prerequisites**: Küçük kademe — plan.md YOK (bilinçli); envanter plan dosyasında.

**Tests**: İlke VI tetiklenmez — yeni saf domain mantığı yok (söküm + handler kısa devresi).
Mevcut testlerin yeşil kalması + silinen testlerin kaldırılması yeterli.

**Organization**: US2 (ödeme zinciri) US1'den ÖNCE koşar — `MerchantKeyGrpcService` (US2'de silinir)
`MerchantInformation`'ı (US1'de silinir) okur; ters sıra build'i kırar. İkisi de P1.

## Phase 1: Setup

- [X] T001 `master`'dan `093-pg-pivot-teardown` branch'i oluştur (çalışma ağacındaki ilgisiz
      değişiklikleri — `ApiKeyAuthenticationOptions.cs`, Discount `CatalogConsumers.cs` — dışarıda tut)

---

## Phase 2: User Story 2 — Ödeme başlatma geçici kapalı (P1)

**Goal**: PG'ye bakan ödeme zinciri komple gider; `start_payment` PAYMENT_DISABLED döner, Pending
sipariş yaratmaz. Ödeme sonraki feature'da (pay-by-link) geri gelecek — çekirdek (PaymentIntent,
fanout) yerinde kalır.

**Independent Test**: build+test yeşil; `start_payment` handler'ı Payment'a hiçbir çağrı yapmaz.

- [X] T002 [US2] `StartPayment.cs` kısa devre: `PAYMENT_DISABLED` resource sabiti
      (`src/services/order/Order.Api/Constants/OrderResourceConstants.cs`) + handler en başta hata döner
      (sepet okuma/intent sorgusu/Order.Create yolu kalkar); tool adı + description DEĞİŞMEZ
      (`src/services/order/Order.Api/Domains/Orders/Features/Agents/Commands/StartPayment.cs`)
- [X] T003 [US2] Order temizliği: `Grpc/PaymentIntentClient.cs` sil; `Program.cs` gRPC client kaydı +
      `Options/SagaAuth.cs` (başka tüketici yoksa — build doğrular) + appsettings ölü
      `PaymentGatewayOption`/SagaAuth section'ları temizle
- [X] T004 [US2] Payment PG-bağlı dosyalar sil: `Grpc/HostedPayment/PaymentIntentGrpcService.cs`,
      `Process/PaymentIntentExpiryCheck.cs`, `Infrastructure/PgHostedPaymentClient.cs`,
      `Grpc/MerchantKeyClient.cs`, `Infrastructure/PaymentTokenHandler.cs`,
      `Infrastructure/CallbackSignatureValidator.cs`, `Domains/Payments/Features/Commands/HandlePaymentCallback.cs`;
      `PaymentIntentEndpointExtension.cs`'ten callback ucu (dosyada başka uç kalmıyorsa dosya da gider)
- [X] T005 [US2] Payment kablo temizliği: `Options/PaymentOptions.cs` PG alanları (`PgBaseUrl`,
      `CallbackSecret`; `IntentTimeoutSeconds` tüketicisizse komple) + `Options/SagaAuth.cs` +
      `Program.cs` (gRPC map, HttpClient, m2m client) + appsettings section'ları.
      KALIR: `PaymentIntent.cs`, `GetAllPaymentsByUserId.cs`, `MessagingExtensions` fanout declare
- [X] T006 [US2] Shared proto söküm: `src/others/Shared/Protos/payment_intent.proto` + MerchantKey
      proto'su (adını `Protos/` listesinden doğrula) + `.csproj` `<Protobuf>` item'ları
- [X] T007 [US2] Customer `Grpc/HostedPayment/MerchantKeyGrpcService.cs` sil + `Program.cs` map kaydı
      (US1'den ÖNCE şart — MerchantInformation bağımlılığı)
- [X] T008 [US2] Checkpoint: `dotnet build && dotnet test` (Order/Payment/Customer test projeleri dahil)

---

## Phase 3: User Story 1 — Merchant/onboarding dünyası kalkar (P1)

**Goal**: Customer BC'de merchant kaydı, PG S2S onboarding, credential callback ve 4 admin MCP tool
komple gider; ölü sır makineleri (bootstrap key, callback secret) kalmaz.

**Independent Test**: Customer.Api `/mcp` tool listesinde merchant tool'u yok; grep'te çalışan kod
referansı 0; build+test yeşil.

- [X] T009 [P] [US1] `src/services/customer/Customer.Api/Domains/MerchantInformations/` klasörünü
      komple sil (aggregate + 4 agent slice + `ReceiveMerchantCredentials`)
- [X] T010 [P] [US1] `src/services/customer/Customer.Api/Onboarding/` klasörünü komple sil
      (PgOnboardingClient, OnboardingGatewayTokenHandler, DropShopOnboardingOption, MerchantBusinessProfileOption)
- [X] T011 [P] [US1] `MerchantRegistrationCallbackEndpointExtension.cs` +
      `Infrastructure/CallbackSignatureValidator.cs` sil (customer tarafı)
- [X] T012 [US1] Customer `Program.cs` + appsettings + `GlobalUsings.cs` temizliği: scope kaydı
      (`MerchantCredentialsWrite`), Options binding'leri, PgOnboardingClient HttpClient zinciri,
      callback endpoint map; `DropShopOnboarding`/`MerchantBusinessProfile` section'ları
- [X] T013 [P] [US1] `tests/Customer.Api.Tests/MerchantInformationTests.cs` sil
- [X] T014 [P] [US1] Shared sabitler: `McpToolNames.cs` CustomerAdminTools merchant bloğu (4 const) +
      `McpToolDescriptions.cs` karşılıkları sil
- [X] T015 [US1] `AuthorizationScopes.cs`: `MerchantCredentialsWrite` + `merchant.*`/`commission.*`
      bloğu sil; repo genelinde kalan referans grep'i (`merchant\.|commission\.`) temiz
- [X] T016 [US1] Checkpoint: build + test + grep `DropShop|MerchantInformation|onboarding` çalışan
      kodda 0 (tarihsel specs/ADR hariç)

---

## Phase 4: User Story 3 — Koreografi dokunulmadı doğrulaması (P2)

**Goal**: 092 zinciri değişmeden durur; söküm diff'i koreografiye sızmadı.

**Independent Test**: `git diff master --stat` — koreografi dosyaları listede YOK.

- [X] T017 [US3] Diff doğrulaması: Order `PaymentConsumers.cs`+`StockConsumers.cs`, Stock
      `OrderConsumers.cs`, `IntegrationEvents.cs` PaymentSucceeded/PaymentFailed, `RabbitMqConstants.cs`
      değişmedi; Payment `MessagingExtensions` exchange declare duruyor

---

## Phase 5: User Story 4 — AgentPlatform fasat temizliği (P2, AYRI REPO)

**Goal**: Fasat ölü pg-* downstream'lere bağlanmaz, ölü scope ilan etmez.

**Independent Test**: Mcp.Gateway testleri yeşil; config'de pg-* yok.

- [X] T018 [US4] `~/dev/AgentPlatform/src/Mcp.Gateway/appsettings.Development.json`: `pg-merchant` +
      `pg-commission` downstream kayıtları sil; `FacadeOption.Scopes`'tan `merchant.read/write/admin`,
      `commission.read/write` çıkar (diğer config dosyalarını da tara)
- [X] T019 [US4] `~/dev/AgentPlatform/tests/Mcp.Gateway.Tests/ToolRoutingRegistryTests.cs` pg-*
      fixture'larını güncelle; `dotnet test` (AgentPlatform çözümü)
- [ ] T020 [US4] AgentPlatform'da ayrı commit (EC PR'ına girmez)

---

## Phase 6: Polish — dokümanlar + guard'lar (aynı PR, İlke VII)

- [X] T021 FLOW.md güncellemeleri: `src/services/customer/FLOW.md` merchant süreci sil;
      `src/services/payment/FLOW.md` + `src/services/order/FLOW.md` söküm-sonrası gerçek + open hotspot
      `🟥 Hotspot (open): yeni PG pay-by-link kontratı bekleniyor — ödeme burada geri açılacak`
- [X] T022 `CLAUDE.md` hizalama: payment + customer BC satırları, Yüzey modeli "Kalan REST/senkron
      kontrat" maddeleri (077 intents/callback + Order→Payment S2S çıkar); `README.md` hosted ödeme anlatımı
- [X] T023 Guard'lar: `scripts/check-claude-spec-links.sh` + `scripts/check-flow-links.sh` geçer
- [X] T024 Final doğrulama: `dotnet build && dotnet test` tüm çözüm; (opsiyonel canlı) Aspire ayağa
      kaldır, `start_payment` PAYMENT_DISABLED döner + orderDb'de yeni sipariş yok (SC-003)
- [ ] T025 Oturum sonu: memory `pg-bank-adapter-pivot-ec-impact.md` söküm-DONE notu + PR aç

---

## Dependencies & Execution Order

- **US2 → US1 zorunlu sıra** (MerchantKeyGrpcService↔MerchantInformation derleme bağı); US3 her an;
  US4 bağımsız (ayrı repo); Polish en son.
- [P] işaretli US1 silmeleri paralel (farklı dosyalar); T012/T015 onlardan sonra (kablo + sabit temizliği).
- MVP = Phase 2 + Phase 3 (iki P1 story; build yeşil + merchant yüzeyi yok + ödeme bilinçli kapalı).

## Notes

- Silme araçları: `git rm -r` tercih; her checkpoint'te build+test (bilinen tuzak: bağımlı TEST
  projeleri de derlenir).
- `specs/077`, `078`, `087` SİLİNMEZ (tarihsel); CLAUDE.md Origin yolları guard'la doğrulanır.
- Çalışma ağacındaki önceden-var iki ilgisiz değişiklik bu branch'in commit'lerine ALINMAZ.