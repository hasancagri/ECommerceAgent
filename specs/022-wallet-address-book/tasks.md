# Tasks: Wallet & AddressBook (Kayıtlı Kart + Adres Defteri)

**Input**: `/specs/022-wallet-address-book/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: Domain birim testleri **zorunlu** (constitution: yeni aggregate davranışı test edilir).

**Organization**: Görevler user story'ye göre gruplu; her story bağımsız uygulanır/test edilir.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: Paralel (farklı dosya, bağımlılık yok)
- **[Story]**: US1=AddressBook, US2=Wallet, US3=Snapshot kontratı

## Path Conventions

Yeni BC: `src/services/customer/Customer.Api/`. Testler: `tests/Customer.Api.Tests/`.
Paylaşılan: `src/others/`, `src/aspire/AppHost/`, `src/ui/WebApp/`, `src/services/gateway/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Yeni Customer BC iskeleti + paylaşılan kayıtlar.

- [X] T001 `src/services/customer/Customer.Api/Customer.Api.csproj` oluştur (Basket.Api.csproj ikizi; sürümsüz PackageReference'lar)
- [X] T002 [P] `src/services/customer/Customer.Api/GlobalUsings.cs` (Basket usings ikizi: Common.Domains, Marten, Wolverine, Result, scope, MCP)
- [X] T003 [P] `src/others/Shared/Utils/Constants/SchemaConstants.cs`: `CustomerSchemaName = "customerManagement"` ekle
- [X] T004 [P] `src/others/Common/Utils/Constants/AuthorizationScopes.cs`: `CustomerRead="customer.read"` + `CustomerWrite="customer.write"` ekle
- [X] T005 [P] `src/others/Identity.Server/Config.cs`: 2 ApiScope + ApiResource `customer.api` + `ecommerce.bff` AllowedScopes'a customer scope'ları ekle
- [X] T006 `src/aspire/AppHost/AppHost.cs`: `customerDb` + `customer-api` resource (WithReference db/rabbit/identity); gateway + web referanslarına ekle
- [X] T007 [P] `tests/Customer.Api.Tests/Customer.Api.Tests.csproj` oluştur (xUnit + Shouldly; Customer.Api referansı)
- [X] T008 [P] `ECommerceAgent.slnx`: Customer.Api + Customer.Api.Tests projelerini çözüme ekle

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Servis çalışır iskelet + ağ geçidi + BFF istemcisi. **US'lerden önce bitmeli.**

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz.

- [X] T009 `src/services/customer/Customer.Api/Program.cs` iskeleti: Marten(customerDb, CustomerSchemaName, Newtonsoft nonpublic) + IntegrateWithWolverine + UseWolverine(dev Solo, scope middleware, assembly discovery) + ApiVersioning(v1) + AddAuthenticationAndAuthorizationExtension(CustomerRead, CustomerWrite) + GlobalExceptionHandler + AddAllDependencies + AddHttpContextAccessor + AddMcpServer + auth pipeline + MapMcp (Schema.For/endpoint map story fazlarında eklenir)
- [X] T010 [P] `src/services/customer/Customer.Api/appsettings.json` + `appsettings.Development.json` (IdentityOption Audience=`customer.api`, connection string adları; Basket ikizi)
- [X] T011 [P] `src/services/customer/Customer.Api/Properties/launchSettings.json` + `Customer.http` (Basket ikizi)
- [X] T012 [P] `src/services/gateway` YARP config: `customer-api` route/cluster ekle (basket-api deseni, service discovery)
- [X] T013 [P] `src/ui/WebApp/Program.cs`: Customer API HttpClient/typed client + kullanıcı token forwarding (mevcut Basket istemci deseni)

**Checkpoint**: Servis ayağa kalkar, migrate olur, /mcp mount; US'ler başlayabilir.

---

## Phase 3: User Story 1 - Adres defterini yönet (Priority: P1) 🎯 MVP

**Goal**: Kullanıcı adres ekler/listeler/düzenler/siler + birini varsayılan yapar (≤1 varsayılan).

**Independent Test**: Wallet/gateway olmadan; adres ekle → listede gör → varsayılan yap → düzenle → sil; boş alan reddi.

### Tests for User Story 1 ⚠️ (önce yaz, FAIL olsun)

- [X] T014 [P] [US1] `tests/Customer.Api.Tests/AddressBookTests.cs`: ≤1 varsayılan (INV-2), Add/Update/Remove/SetDefault, Address.Create boş-alan reddi (FR-002)

### Implementation for User Story 1

- [X] T015 [P] [US1] `.../Domains/AddressBooks/ValueObjects/Address.cs`: record + private ctor + `Create` (Province/District/Street/ZipCode/Line; zorunlu-alan → Error)
- [X] T016 [P] [US1] `.../Domains/AddressBooks/SavedAddress.cs`: sade entity (Id, Value:Address, IsDefault; `Create`/`SetDefault`/`Update`)
- [X] T017 [US1] `.../Domains/AddressBooks/AddressBook.cs`: aggregate root (UserId, private `_addresses`, `AddAddress`/`UpdateAddress`/`RemoveAddress`/`SetDefaultAddress` ≤1 invariant) — T014 geçsin (dep T015,T016)
- [X] T018 [P] [US1] `.../AddressBooks/Features/Commands/AddAddress.cs` ([Transactional]; get-or-create defter; endpoint POST `/addresses`, customer.write)
- [X] T019 [P] [US1] `.../AddressBooks/Features/Commands/UpdateAddress.cs` (PUT `/addresses/{id}`; yoksa NotFound)
- [X] T020 [P] [US1] `.../AddressBooks/Features/Commands/DeleteAddress.cs` (DELETE `/addresses/{id}`)
- [X] T021 [P] [US1] `.../AddressBooks/Features/Commands/SetDefaultAddress.cs` (POST `/addresses/{id}/default`)
- [X] T022 [P] [US1] `.../AddressBooks/Features/Queries/GetAddresses.cs` (GET `/addresses`, customer.read, `AddressView`, yalnız kendi UserId)
- [X] T023 [P] [US1] `.../AddressBooks/Features/Agent/GetAddresses.cs` (MCP için okuma query slice'ı)
- [X] T024 [US1] `.../AddressBooks/AddressBookEndpointExtension.cs`: adres endpoint'lerini grup + map (dep T018-T022)
- [X] T025 [US1] `.../AddressBooks/AddressBookMcpTools.cs`: `list_addresses` tool (Agent.GetAddresses'i IMessageBus ile sarar; okuma-yalnız)
- [X] T026 [US1] `Program.cs`: `Schema.For<AddressBook>().Index(x => x.UserId)` + adres endpoint grubunu map et (dep T024)
- [X] T027 [US1] `src/ui/WebApp/`: adres yönetim sayfası/sayfaları (listele/ekle/düzenle/sil/varsayılan; Customer API istemcisi üzerinden)

**Checkpoint**: US1 tek başına tam çalışır ve test edilebilir (MVP).

---

## Phase 4: User Story 2 - Cüzdanı (kayıtlı kart) yönet (Priority: P1)

**Goal**: Kullanıcı kart ekler (tokenize)/listeler (marka+son4+expiry+etiket)/siler (revoke)/varsayılan yapar.

**Independent Test**: AddressBook'tan bağımsız; kart ekle → listede token/PAN yok → varsayılan yap → sil; geçmiş-expiry reddi; tokenize hata → kayıt yok.

### Tests for User Story 2 ⚠️ (önce yaz, FAIL olsun)

- [X] T028 [P] [US2] `tests/Customer.Api.Tests/WalletTests.cs`: ≤1 varsayılan (INV-1), Add/Remove/SetDefault, geçmiş-expiry reddi (FR-009), SavedCard'da PAN/CVV alanı yokluğu (INV-3)

### Implementation for User Story 2

- [X] T029 [P] [US2] `.../Domains/Wallets/Tokenization/ICardTokenizer.cs`: `TokenizeAsync` + `RevokeAsync` + `TokenizeResult` record (contracts/tokenizer.md)
- [X] T030 [P] [US2] `.../Domains/Wallets/Tokenization/SimulatedCardTokenizer.cs`: stub (sahte token, BIN'den brand, last4; RevokeAsync no-op; ISingletonDependency; geçmiş-expiry/boş-PAN → Success=false)
- [X] T031 [P] [US2] `.../Domains/Wallets/SavedCard.cs`: sade entity (Token/Brand/Last4/ExpiryMonth/ExpiryYear/Label/IsDefault; **PAN/CVV YOK**; `Create`/`SetDefault`)
- [X] T032 [US2] `.../Domains/Wallets/Wallet.cs`: aggregate root (UserId, private `_cards`, `AddCard`/`RemoveCard`/`SetDefaultCard` ≤1 invariant) — T028 geçsin (dep T031)
- [X] T033 [US2] `.../Wallets/Features/Commands/AddCard.cs` ([Transactional]; expiry doğrula → tokenize → başarısızsa Error+Store yok (FR-013) → yalnız token+görünen alanları sakla; PAN/CVV persist ETME; POST `/cards`, customer.write)
- [X] T034 [P] [US2] `.../Wallets/Features/Commands/DeleteCard.cs` (kartı çıkar+Store, SONRA `RevokeAsync(token)` best-effort fail-open; DELETE `/cards/{id}`)
- [X] T035 [P] [US2] `.../Wallets/Features/Commands/SetDefaultCard.cs` (POST `/cards/{id}/default`)
- [X] T036 [P] [US2] `.../Wallets/Features/Queries/GetCards.cs` (GET `/cards`, customer.read, `CardView` — **token/PAN/CVV dönmez**, SC-002)
- [X] T037 [P] [US2] `.../Wallets/Features/Agent/GetCards.cs` (MCP için okuma query slice'ı)
- [X] T038 [US2] `.../Wallets/WalletEndpointExtension.cs`: kart endpoint'lerini grup + map (dep T033-T036)
- [X] T039 [US2] `.../Wallets/WalletMcpTools.cs`: `list_cards` tool (okuma-yalnız; **kart-ekleme tool'u YOK**, FR-019)
- [X] T040 [US2] `Program.cs`: `Schema.For<Wallet>().Index(x => x.UserId)` + kart endpoint grubunu map et (ICardTokenizer Scrutor marker ile otomatik kayıtlı) (dep T038)
- [X] T041 [US2] `src/ui/WebApp/`: kart yönetim sayfası/sayfaları (listele/ekle/sil/varsayılan; PAN/CVV form alanı asla yeniden gösterilmez)

**Checkpoint**: US1 + US2 bağımsız çalışır.

---

## Phase 5: User Story 3 - Checkout referansı + snapshot kontratı (Priority: P2)

**Goal**: Kayıtlar checkout tarafından referanslanabilir; snapshot kontratı sabitlenir (kod ayrı feature'da).

**Independent Test**: Read uçları snapshot için gerekli alanları döndürür; kayıt değişince Customer verisi bağımsız (Order kendi VO'sunu tutar → izole).

### Implementation for User Story 3

- [X] T042 [US3] Read çıktı doğrula: `CardView` marka+son4 taşır, `AddressView` tüm adres alanlarını taşır (checkout kopyası için); eksikse GetCards/GetAddresses'i düzelt
- [X] T043 [US3] Snapshot kontratını referansla: kod yorumu + `contracts/mcp-and-snapshot.md`'ye işaret; **bu feature'da order-tarafı kod YOK** (US3 kontrat-only)

**Checkpoint**: Tüm story'ler bağımsız işlevsel.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T044 [P] `dotnet test tests/Customer.Api.Tests/Customer.Api.Tests.csproj` — tüm domain testleri yeşil
- [X] T045 [P] `dotnet build` — tüm çözüm derlenir
- [X] T046 Quickstart doğrulaması (Aspire): A(adres)+B(kart) canlı PASS; C MCP read-only surface ✓; D kontrat-only; B5/B6 forced-error ertelendi (stub hep-true)
- [X] T047 Güvenlik kontrolü: DB `customerManagement` + loglarda PAN/CVV/token sızıntısı yok (FR-008, SC-002) — grep + tablo denetimi
- [X] T048 [P] Obsidian `todo-payment-gateway-card-vault` durum notu güncellendi (022 stub bitti + canlı doğrulama; gerçek gateway kalan iş)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: Bağımsız başlar. T001 → T002; T003-T005,T007,T008 [P]; T006 T001'e bağlı.
- **Foundational (P2)**: Setup sonrası; **tüm story'leri bloklar**. T009 T001-T002'ye bağlı.
- **US1 (P3) / US2 (P4)**: Foundational sonrası; **birbirinden bağımsız** (ayrı Domains alt-klasörü). Paralel yürütülebilir.
- **US3 (P5)**: US1 + US2 read uçları hazır olunca (T022, T036).
- **Polish (P6)**: İstenen story'ler bitince.

### Within Each Story

- Test önce (FAIL) → VO/entity → aggregate → command/query slice'ları → endpoint extension → Program.cs wiring → WebApp UI.
- US1: T014 → (T015,T016) → T017 → (T018-T023) → T024 → T025 → T026 → T027
- US2: T028 → (T029,T030,T031) → T032 → T033 → (T034-T037) → T038 → T039 → T040 → T041

### Parallel Opportunities

- Setup: T002,T003,T004,T005,T007,T008 [P] birlikte.
- Foundational: T010,T011,T012,T013 [P] birlikte.
- US1 slice'ları T018-T023 [P] (farklı dosya). US2 slice'ları T034-T037 [P].
- US1 ve US2 tüm faz olarak paralel (ayrı geliştiriciler).

---

## Parallel Example: User Story 1

```bash
# Test önce:
Task: "AddressBookTests.cs — ≤1 varsayılan + Add/Update/Remove/SetDefault + boş-alan reddi"
# Sonra VO + entity paralel:
Task: "Address.cs value object"
Task: "SavedAddress.cs entity"
# Sonra command/query slice'ları paralel:
Task: "AddAddress / UpdateAddress / DeleteAddress / SetDefaultAddress / GetAddresses"
```

---

## Implementation Strategy

### MVP First (US1 — Adres defteri)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **DUR + DOĞRULA** (adres uçtan uca) → demo.

### Incremental Delivery

1. Setup + Foundational → iskelet hazır.
2. US1 (adres) → bağımsız test → demo (MVP, gateway'siz).
3. US2 (cüzdan/kart, tokenize stub) → bağımsız test → demo.
4. US3 (snapshot kontratı) → read alanları + kontrat sabiti.
5. Polish → testler/güvenlik/quickstart.

### Notlar

- [P] = farklı dosya, bağımlılık yok. [Story] = izlenebilirlik.
- PCI: PAN/CVV asla persist/log/event/MCP; AddCard'da yalnız tokenize'a geçer.
- Kart update yok (sil+ekle). Silme/değişim eski token'ı revoke eder (fail-open).
- Her task veya mantıksal grup sonrası commit; checkpoint'lerde story bağımsız doğrula.