# Tasks: Discount'ın Sistemden Tamamen Kaldırılması

**Input**: Design documents from `/specs/018-remove-discount/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Yeni test yazılmaz; mevcut testlerden indirim izleri temizlenir (FR-013). Her faz build+test yeşiliyle kapanır.

**Organization**: Fazlar UYGULAMA sırasındadır (research K1: dıştan içe). US1 (P1) çekirdek silmedir ve en sona kalır;
US2/US3 tüketici temizlikleri onu güvenli kılar. Her faz sonunda çözüm derlenir ve testler geçer.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşabilir (farklı dosyalar, bağımlılık yok)
- **[Story]**: Görevin ait olduğu user story (US1, US2, US3)

---

## Phase 1: Setup

**Purpose**: Kaldırma öncesi taban çizgisi.

- [X] T001 Taban çizgisi doğrula: repo kökünde `dotnet build` + `dotnet test` yeşil (kaldırma öncesi durum kayıt altına alınır)

---

## Phase 2: Foundational

**Purpose**: Yok — bu feature yalnız silme/sadeleştirmedir; engelleyici ön koşul görevi bulunmaz.

**Checkpoint**: T001 sonrası user story fazları başlayabilir.

---

## Phase 3: User Story 2 - Alışveriş deneyiminde indirim izi kalmaz (Priority: P2)

**Goal**: Basket/Order/Storefront domain'leri ve WebApp'ten kupon + indirim kavramları tamamen silinir (K2, K7).

**Independent Test**: Vitrin ve sepet sayfaları tek fiyat gösterir; kupon alanı yok; sipariş kaydında indirim yok; build+test yeşil.

### Basket BC

- [X] T002 [P] [US2] Sil: src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/ApplyDiscountCoupon.cs ve RemoveDiscountCoupon.cs
- [X] T003 [P] [US2] Sil: src/services/basket/Basket.Api/Domains/Baskets/Features/Agent/ApplyDiscountCoupon.cs ve RemoveDiscountCoupon.cs
- [X] T004 [P] [US2] Sil: src/services/basket/Basket.Api/Domains/Baskets/ValueObjects/Discount.cs
- [X] T005 [US2] src/services/basket/Basket.Api/Domains/Baskets/Basket.cs: AppliedDiscount, IsApplyDiscount, GetTotalPriceWithAppliedDiscount,
      ApplyNewDiscount, ApplyAvailableDiscount, ClearDiscount kaldır; GetTotalPrice tek fiyattan kalır
- [X] T006 [US2] src/services/basket/Basket.Api/Domains/Baskets/Entities/BasketEntities.cs: BasketItem.PriceByApplyDiscountRate ve
      ApplyDiscount/ClearDiscount metotları kaldır
- [X] T007 [US2] BasketEndpointExtension.cs kupon endpoint'leri + BasketMcpTools.cs apply/remove_discount_coupon tool'ları kaldır
      (src/services/basket/Basket.Api/Domains/Baskets/)
- [X] T008 [US2] Features/Queries/GetBasket.cs ve Features/Agent/GetBasket.cs yanıtlarından DiscountRate, Coupon,
      TotalPriceWithAppliedDiscount ve satır PriceByApplyDiscountRate alanlarını kaldır

### Order BC

- [X] T009 [P] [US2] src/services/order/Order.Api/Domains/Orders/Order.cs: DiscountRate alanı ve Create imzasındaki discountRate kaldır
- [X] T010 [US2] src/services/order/Order.Api/Domains/Orders/Features/Commands/CreateOrder.cs: command/response'tan DiscountRate kaldır

### Storefront BC

- [X] T011 [P] [US2] src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontView.cs: DiscountRate + ApplyDiscount kaldır
- [X] T012 [US2] src/services/storefront/Storefront.Api/StorefrontEventHandlers.cs: DiscountChangedEvent handler'ını kaldır
- [X] T013 [US2] Features/Queries/GetProductStorefrontView.cs ve GetStorefrontProductList.cs yanıtlarından DiscountRate kaldır
      (src/services/storefront/Storefront.Api/Domains/StorefrontView/)

### WebApp

- [X] T014 [P] [US2] Sil: src/ui/WebApp/Services/Refit/IDiscountRefitService.cs, Pages/Basket/Dto/GetDiscountByCouponResponse.cs ve
      ApplyDiscountRateRequest.cs; src/ui/WebApp/Program.cs'teki Refit istemci kaydını kaldır
- [X] T015 [US2] src/ui/WebApp/Services/BasketService.cs + Services/Refit/IBasketRefitService.cs kupon metotları;
      Pages/Basket/Dto/BasketResponse.cs ve BasketItemDto.cs indirim alanları kaldır
- [X] T016 [US2] src/ui/WebApp/Pages/Basket/Index.cshtml(.cs) + ViewModel/{BasketViewModel,BasketItemViewModel,BasketPageViewModel}.cs:
      kupon alanı, indirimli toplam ve satır indirim gösterimi kaldır
- [X] T017 [US2] src/ui/WebApp/Services/OrderService.cs + Pages/Order/Create.cshtml.cs + Dto/CreateOrderRequest.cs +
      ViewModel/CreateOrderViewModel.cs: DiscountRate taşıma kaldır
- [X] T018 [US2] src/ui/WebApp/Services/StorefrontService.cs + Dto/StorefrontProductDto.cs + ViewModel/StorefrontProductViewModel.cs +
      Pages/Shared/_ProductCard.cshtml: indirim rozeti/üstü çizili fiyat kaldır

### Testler (US2)

- [X] T019 [P] [US2] tests/Basket.Api.Tests/BasketTests.cs ve BasketItemTests.cs: indirim/kupon testlerini sil, kalanları hizala
- [X] T020 [P] [US2] tests/Order.Api.Tests/OrderTests.cs: DiscountRate izlerini temizle
- [X] T021 [P] [US2] tests/Storefront.Api.Tests/{StorefrontViewTests,ProductStorefrontViewResponseTests,StorefrontProductResponseTests}.cs:
      indirim izlerini temizle
- [X] T022 [US2] Checkpoint: `dotnet build` + `dotnet test` yeşil; sepet toplamı yalnız birim fiyat × adet

**Checkpoint**: US2 bağımsız doğrulanabilir — alışveriş yüzeyinde indirim izi yok, sistem derlenir ve testler geçer.

---

## Phase 4: User Story 3 - Ingestion ve agent'lar indirim bilmez (Priority: P3)

**Goal**: Ingestion zinciri 4 adıma iner (K4); feed kontratından indirim alanları düşer (K3); ChatAgent Discount MCP'sini bırakır.

**Independent Test**: Feed tetiklenir, zincir Brand→Category→Catalog→Stock→Finish biter; ChatAgent araç listesinde indirim yok.

- [X] T023 [P] [US3] Sil: src/agents/IngestionAgent/Workflows/05_DiscountWrite/ (klasörün tamamı)
- [X] T024 [US3] src/agents/IngestionAgent/SupplierSnapshotHandler.cs: DiscountWrite düğümü/edge'leri kaldır;
      StockWrite success-edge'ini doğrudan finish collector'a bağla (T023'e bağlı)
- [X] T025 [US3] src/agents/IngestionAgent/{Program.cs,ConstValues.cs,Workflows/WriterResult.cs,GlobalUsings.cs}:
      DiscountWriterAgent kaydı, talimat/araç sabitleri, DiscountWriterResult ve using temizliği (T023'e bağlı)
- [X] T026 [P] [US3] src/agents/ChatAgent/{Program.cs,ConstValues.cs}: Discount MCP kaydı, indirim araç adları,
      agent talimatlarındaki indirim izleri ve varsa scope talepleri kaldır
- [X] T027 [US3] src/others/Shared/IntegrationEvents.cs: SupplierProductSnapshotReceived.DiscountPercent alanını kaldır
- [X] T028 [US3] src/services/supplier/Supplier.Gateway/Domains/Feeds/SupplierFeedAdapter.cs: wire + kanonik modelden
      DiscountPercent/DiscountCode eşlemelerini kaldır (T027 ile birlikte)
- [X] T029 [US3] src/services/supplier/Supplier.Api/Domains/Feeds/FeedEndpointExtension.cs wire modeli +
      Datasets/products.json: indirim alanlarını kaldır
- [X] T030 [P] [US3] tests/Supplier.Gateway.Tests/{SupplierFeedAdapterTests,FeedSnapshotTests,FeedPullTests}.cs: indirim alan izleri temizle
- [X] T031 [P] [US3] tests/IngestionAgent.Tests/WorkflowSemanticsSpikeTests.cs: DiscountWrite adım izlerini temizle
- [X] T032 [US3] Checkpoint: `dotnet build` + `dotnet test` yeşil; zincir topolojisi 4 yazıcı + finish

**Checkpoint**: US3 bağımsız doğrulanabilir — ingestion indirim adımı olmadan biter, agent'larda indirim aracı yok.

---

## Phase 5: User Story 1 - Sistem Discount servisi olmadan tam çalışır (Priority: P1) 🎯 Çekirdek

**Goal**: Discount.Api + testleri + DB + AppHost/gateway/slnx kayıtları ve Shared/Identity kalıntıları silinir (K5, K6).

**Independent Test**: AppHost ayağa kalkar; resource listesinde discount-api/discountDb yok; uçtan uca alışveriş tamamlanır.

- [X] T033 [US1] ECommerceAgent.slnx: Discount.Api ve Discount.Api.Tests proje kayıtlarını sil
- [X] T034 [US1] src/aspire/AppHost/AppHost.cs + AppHost.csproj: discountDb, discount-api resource'u, WithReference(discountApi)
      (webapp/chat-agent/ingestion), WaitFor ve proje referansını sil
- [X] T035 [P] [US1] src/services/gateway/Gateway/appsettings.Development.json: discount-route, discount-mcp-route ve discount.cluster sil
- [X] T036 [US1] Sil: src/services/discount/ ve tests/Discount.Api.Tests/ (klasörlerin tamamı; T033-T034'e bağlı)
- [X] T037 [US1] src/others/Shared/IntegrationEvents.cs: DiscountChangedEvent sil; RabbitMqConstants.cs: DiscountChanged +
      OrderCreated.Queues.Discount sil; Utils/Constants/SchemaConstants.cs: DiscountSchemaName sil (T036'ya bağlı)
- [X] T038 [US1] src/others/Identity.Server/Config.cs: discount.read/write ApiScope'ları, discount.api ApiResource'u ve
      client scope taleplerini sil; src/others/Common/Utils/Constants/AuthorizationScopes.cs: DiscountRead/DiscountWrite sil
- [X] T039 [US1] src/ui/WebApp/Program.cs OIDC scope ekleri + src/ui/WebApp/Authentication/TokenService.cs scope dizesinden
      discount.read sil (K6: T038 ile AYNI commit — aksi halde login kırılır)
- [X] T040 [US1] Checkpoint: `dotnet build` + `dotnet test` yeşil; çözümde Discount projesi ve referansı yok

**Checkpoint**: Tüm story'ler tamam — sistem bir servis + bir DB eksik olarak derlenir, testler geçer.

---

## Phase 6: Polish & Doğrulama

**Purpose**: SC-001..SC-004 kanıtı ve opsiyonel governance temizliği.

- [X] T041 Süpürme (SC-001): `grep -ril "discount" src/ tests/` (bin/obj hariç, .cs/.json/.cshtml) boş dönmeli; kalıntı varsa temizle
- [X] T042 [P] Opsiyonel PATCH (K8): .specify/memory/constitution.md İlke I'deki Discount örneğini yaşayan bir BC ile değiştir
- [X] T043 quickstart.md canlı doğrulama: AppHost ayağa kalkar (SC-002), uçtan uca alışveriş + ingestion + login akışları (SC-003)
- [X] T044 Son koşu (SC-004): `dotnet test` — silinen Discount testleri dışında test sayısı azalmadı doğrula

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1. faz)**: Bağımsız; hemen başlar.
- **Foundational**: Görev yok; atlanır.
- **Uygulama sırası story önceliğinin TERSİDİR (K1, dıştan içe)**: US2 → US3 → US1. US1'in kontrat/scope silmeleri,
  tüketiciler (US2/US3) temizlenmeden derlenemez; bu yüzden çekirdek silme en sona kalır.
- **Polish**: Tüm story'ler bitince.

### Story içi kilit bağımlılıklar

- US2: T002-T004 (silmeler) → T005-T008 (Basket düzenlemeleri); T014 → T015-T016 (WebApp); testler (T019-T021) kod bitince.
- US3: T023 → T024-T025 (handler/kayıt, silinen tiplere referans kalmasın); T027 → T028 (kontrat + adapter birlikte).
- US1: T033-T034 → T036 (proje çözümden düşmeden klasör silinmez); T036 → T037 (DiscountChangedEvent'in tek yayıncısı
  Discount.Api); T038 + T039 AYNI commit (K6 — scope tanımı ve talepleri birlikte kalkar).
- US2 → US1: T012 (Storefront handler) T037'nin ön koşuludur. US3 → US1: T023-T025 (MCP istemcisi) T036'nın ön koşuludur.

### Parallel Opportunities

- US2 içinde: T002, T003, T004 (Basket silmeleri) + T009 (Order) + T011 (Storefront) + T014 (WebApp) paralel başlar.
- US2 testleri T019, T020, T021 paralel.
- US3 içinde: T023 + T026 + T029 paralel; T030, T031 paralel.
- US1 içinde: T035 (gateway) diğerlerinden bağımsız paralel.
- Polish'te T042 (anayasa PATCH) diğerlerine paralel.

---

## Implementation Strategy

### Artımlı teslim (her faz güvenli ara durak)

1. T001: taban çizgisi yeşil.
2. US2 tamamla → checkpoint T022: alışveriş yüzeyi indirimsiz, sistem hâlâ tam çalışır (Discount servisi henüz yerinde).
3. US3 tamamla → checkpoint T032: ingestion 4 adım, agent'lar indirimsiz.
4. US1 tamamla → checkpoint T040: servis + DB + tüm kayıtlar silindi. Feature'ın çekirdek değeri burada teslim olur.
5. Polish → SC-001..SC-004 kanıtlanır (T041-T044, quickstart.md).

### Not — MVP kavramı

Bu bir kaldırma feature'ıdır: değer ancak US1 kapanınca teslim olur; US2/US3 tek başına "ürün" değil güvenli ara
adımlardır. Kısmi teslim istenirse en anlamlı kesit US2'dir (kullanıcıya görünür sadeleşme).

---

## Notes

- Her görev/madde ≤150 karakter kuralına uyar; ayrıntı research.md/data-model.md/contracts/ altındadır.
- Her checkpoint'te commit önerilir; T038+T039 zorunlu olarak tek commit'tir.
- RabbitMQ'daki eski `discount.changed` exchange'i ve `discountDb` volume kalıntısı elle temizlenir (kapsam dışı, quickstart "Temizlik").