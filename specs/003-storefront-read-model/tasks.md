# Tasks: Storefront Composite Read Model (Ürün Vitrin Görünümü)

> **As-built uyarısı (2026-07-20, PR #10 + #11):** Bu görev listesi ilk tasarımın
> execution kaydıdır (3 ayrı doküman, Bootstrap, timestamp guard, MCP tool). Uygulama
> sonrası tasarım değişti; güncel hâl için bkz. [spec.md](./spec.md) Amendment +
> data-model.md. Aşağıdaki görevler TARİHSEL'dir, yeniden yazılmadı.

**Input**: Design documents from `/specs/003-storefront-read-model/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (hepsi mevcut)

**Tests**: plan.md'nin Testing bölümü kapsamı açıkça belirtiyor (idempotent upsert, stale-event
guard, kısmi-satır davranışı, Discount invariant) — repo konvansiyonuna uyan saf domain/mapper
birim testleri dahil edildi (host/entegrasyon harness'ı yok, IDocumentSession gerektiren handler'lar
test edilmez — yalnızca pure `TryApply`/`From` metotları).

**Organization**: Fazlar user story'e göre gruplu; her faz bağımsız tamamlanabilir/test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalıştırılabilir (farklı dosyalar, tamamlanmamış bir task'a bağımlılık yok)
- **[Story]**: Hangi user story'e ait (US1/US2/US3)

## Path Conventions

Yeni servis: `src/services/storefront/Storefront.Api/`, testler: `tests/Storefront.Api.Tests/`.
Değişen mevcut servisler: `src/services/{catalog,stock,discount}/*.Api/`. Repo konvansiyonu.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Yeni `Storefront.Api` servisinin iskeleti + repo-geneli kayıt noktaları

- [X] T001 `Storefront.Api` proje iskeleti oluştur: `Storefront.Api.csproj` (Stock.Api.csproj'daki
      paket referanslarını kopyala: Marten, Marten.Newtonsoft, WolverineFx.*, Asp.Versioning.Http,
      Scrutor, JwtBearer, ModelContextProtocol.AspNetCore + Common/Shared/ServiceDefaults proje
      referansları), `GlobalUsings.cs` (Stock.Api'ninkini örnek al), `Dependencies/DependencyExtensions.cs`
      (Scrutor `AddAllDependencies` — Stock.Api ile birebir aynı), boş `Program.cs` (yalnızca
      `WebApplication.CreateBuilder` + `AddOpenApiDocumentation()`) — hepsi
      `src/services/storefront/Storefront.Api/` altında
- [X] T002 [P] `ECommerceAgent.slnx`'e `src/services/storefront/Storefront.Api/Storefront.Api.csproj`
      (`/src/services/` klasörü altına) ve `tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj`
      (`/tests/` klasörü altına) ekle
- [X] T003 [P] `tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj` (Stock.Api.Tests.csproj'u
      örnek al, `ProjectReference` yeni Storefront.Api'ye) + `GlobalUsings.cs` oluştur
- [X] T004 [P] `src/others/Shared/Utils/Constants/SchemaConstants.cs`'e
      `StorefrontSchemaName = "storefrontManagement"` ekle
- [X] T005 [P] `src/others/Common/Utils/Constants/AuthorizationScopes.cs`'e
      `StorefrontRead = "storefront.read"` ekle (storefront.api bölümü)
- [X] T006 [P] `src/ui/WebApp/Authentication/TokenService.cs`'teki `ReadScopes`'a `storefront.read`
      ekle (`"catalog.read discount.read stock.read storefront.read"`)
- [X] T007 [P] `src/aspire/AppHost/AppHost.cs`: `storefrontDb = postgres.AddDatabase("storefrontDb")`,
      `storefrontApi = builder.AddProject<Projects.Storefront_Api>("storefront-api")`
      (`.WithReference(storefrontDb, rabbit, catalogApi, stockApi, discountApi)` — bootstrap için
      3 kaynağa erişmesi lazım, bkz. T052) ekle; `gateway`/`web`'e `.WithReference(storefrontApi)` ekle
- [X] T008 [P] `src/services/gateway/Gateway/appsettings.Development.json`'a `storefront-route`
      (`Match: "{version}/storefront/{**catch-all}"` → `PathPattern: "/api/{version}/storefront/{**catch-all}"`,
      `AuthorizationPolicy: "ClientCredential"`), `storefront-mcp-route` (`/mcp/storefront/{**catch-all}`
      → `/mcp/{**catch-all}`) ve `storefront.cluster` (`http://storefront-api`) ekle (mevcut
      catalog-route/stock-mcp-route deseniyle birebir)

**Checkpoint**: Proje derlenir (boş), Aspire'da servis görünür, henüz iş mantığı yok.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tüm user story'lerin üzerine kurulacağı ortak kontrat + doküman tipleri

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story'e başlanamaz

- [X] T009 [P] `src/others/Shared/IntegrationEvents.cs`'e `ProductChangedEvent`, `StockChangedEvent`,
      `DiscountChangedEvent` record'larını ekle (contracts/integration-events.md'deki imzalarla birebir)
- [X] T010 [P] `src/others/Shared/RabbitMqConstants.cs`'e `ProductChanged`, `StockChanged`,
      `DiscountChanged` exchange/queue sabitlerini ekle (contracts/integration-events.md'deki isimlerle)
- [X] T011 [P] `Domains/StorefrontView/CatalogInfo.cs`: düz sınıf (aggregate değil), alanlar
      `ProductId`/`Name`/`ImageUrl`/`IsDeleted`/`UpdatedAtUtc`; statik `Create(...)` fabrikası;
      `TryApply(name, imageUrl, isDeleted, occurredAtUtc) : bool` — `occurredAtUtc <= UpdatedAtUtc`
      ise `false` döner (stale-guard, uygulamaz), yoksa alanları günceller ve `true` döner
- [X] T012 [P] `Domains/StorefrontView/StockInfo.cs`: aynı desen — `ProductId`/`IsInStock`/`UpdatedAtUtc`,
      `Create(...)`, `TryApply(isInStock, occurredAtUtc) : bool`
- [X] T013 [P] `Domains/StorefrontView/DiscountInfo.cs`: aynı desen — `ProductId`/`Rate` (`decimal?`)/
      `UpdatedAtUtc`, `Create(...)`, `TryApply(rate, occurredAtUtc) : bool` (`rate: null` geçerli bir
      uygulanan değerdir, stale-guard yalnızca `occurredAtUtc`'a bakar)
- [X] T014 `Program.cs`: Marten wiring (`storefrontDb` connection string, `SchemaConstants.StorefrontSchemaName`,
      `UseNewtonsoftForSerialization`, `opts.Schema.For<CatalogInfo>().Identity(x => x.ProductId)` —
      `StockInfo`/`DiscountInfo` için aynısı — `IntegrateWithWolverine()`,
      `ApplyAllDatabaseChangesOnStartup()`), `AddApiVersioning`, `AddAuthenticationAndAuthorizationExtension`
      (`AuthorizationScopes.StorefrontRead`), `AddGlobalExceptionHandler`, `AddAllDependencies`,
      `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` (Stock.Api/Program.cs'i örnek al;
      RabbitMQ bloğu henüz eklenmez, bkz. T033/T044) — depends on: T011, T012, T013

**Checkpoint**: 3 doküman tipi + Marten şeması hazır; okuma/yazma slice'ları eklenebilir.

---

## Phase 3: User Story 1 - Ürün vitrin bilgisi tek çağrıda birleşik döner (Priority: P1) 🎯 MVP

**Goal**: `GetProductStorefrontView` sorgusu 3 dokümanı Storefront'un kendi DB'sinden birleştirip
tek yanıt döner; eksik alan (kısmi satır) hata fırlatmaz; kimlik doğrulama gerekmez (FR-002/003/007/008).

**Independent Test**: `CatalogInfo`/`StockInfo`/`DiscountInfo` doğrudan (event olmadan) yazılmış bir
`ProductId` için sorgu çağrılır; yanıtın 3 kaynağın alanlarını içerdiği tek çağrıda doğrulanır; eksik
`StockInfo`/`DiscountInfo` durumunda ilgili alanlar `null` döner, hata fırlamaz.

### Tests for User Story 1

- [X] T015 [P] [US1] `CatalogInfo.TryApply` testleri (ilk oluşturma `Create`, daha yeni event uygular,
      eski/eşit `occurredAtUtc` stale-guard ile atlar) `tests/Storefront.Api.Tests/CatalogInfoTests.cs`
- [X] T016 [P] [US1] `StockInfo.TryApply` testleri (aynı desen) `tests/Storefront.Api.Tests/StockInfoTests.cs`
- [X] T017 [P] [US1] `DiscountInfo.TryApply` testleri (aynı desen + `Rate: null` uygulanabilir olduğunu
      doğrulayan test) `tests/Storefront.Api.Tests/DiscountInfoTests.cs`

### Implementation for User Story 1

- [X] T018 [US1] `Domains/StorefrontView/Features/Queries/GetProductStorefrontView.cs`: query record
      (`[RequiredScope(AuthorizationScopes.StorefrontRead)]`), `ProductStorefrontViewResponse` (statik
      `From(CatalogInfo catalog, StockInfo? stock, DiscountInfo? discount)` mapper — `IsInStock`/
      `DiscountRate` sırasıyla `stock?.IsInStock`/`discount?.Rate`), handler (`session.LoadAsync<CatalogInfo>`
      → null ise `NotFound()`, ardından `StockInfo`/`DiscountInfo` `LoadAsync` — bulunamazsa `null` kabul) —
      depends on: T011, T012, T013
- [X] T019 [P] [US1] `ProductStorefrontViewResponse.From` mapping testleri — tam satır + kısmi satır
      (`stock`/`discount` null → `IsInStock`/`DiscountRate` null) `tests/Storefront.Api.Tests/ProductStorefrontViewResponseTests.cs`
      — depends on: T018
- [X] T020 [US1] `Domains/StorefrontView/StorefrontViewEndpointExtension.cs`: `api/v{version:apiVersion}/storefront/products`
      grubu, `GET /{productId:guid}` → `RequireAuthorization(AuthorizationScopes.StorefrontRead)` —
      depends on: T018
- [X] T021 [US1] `Domains/StorefrontView/StorefrontMcpTools.cs`: `GetProductStorefrontView`'i
      `bus.InvokeAsync` ile saran ince MCP tool (`[McpServerTool]`, repo konvansiyonu) — depends on: T018
- [X] T022 [US1] `Program.cs`: `apiVersionSet`, `UseAuthentication`/`UseAuthorization`,
      `app.AddStorefrontViewGroupEndpointExtension(apiVersionSet)`, `app.MapMcp("/mcp")` — depends on: T020, T021

**Checkpoint**: Okuma tarafı kod-tamam; canlı uçtan uca test için US2/Bootstrap ile veri beslemesi gerekir.

---

## Phase 4: User Story 2 - Kaynak veri değişince görünüm güncel yansıtır (Priority: P2)

**Goal**: Catalog/Stock event yayınlar; Storefront event-tetikli idempotent upsert ile günceller (FR-004/006).

**Independent Test**: Bir ürün oluşturulur (Catalog), stoğu tükenir (Stock) — birkaç saniye sonra
Storefront görünümü `isInStock: false` yansıtır; aynı event 100 kez tekrarlanırsa sonuç değişmez.

### Implementation for User Story 2

- [X] T023 [P] [US2] `Catalog.Api` `Features/Commands/CreateProduct.cs`: `Store` sonrası mevcut
      `ProductCreatedEvent`e ek olarak `ProductChangedEvent(product.Id, cmd.Name, cmd.ImageUrl,
      IsDeleted: false, DateTime.UtcNow)` yayınla
- [X] T024 [P] [US2] `Catalog.Api` `Features/Commands/UpdateProduct.cs`: handler'a `IMessageBus bus`
      parametresi ekle, `Store` sonrası `ProductChangedEvent` yayınla
- [X] T025 [P] [US2] `Catalog.Api` `Features/Commands/DeleteProduct.cs`: handler'a `IMessageBus bus`
      ekle, `Store` sonrası `ProductChangedEvent(..., IsDeleted: true, ...)` yayınla
- [X] T026 [US2] `Catalog.Api/Program.cs`: `product.changed` exchange'i declare et (fanout,
      `BindQueue(RabbitMqConstants.ProductChanged.Queues.Storefront)`),
      `opts.PublishMessage<ProductChangedEvent>().ToRabbitExchange(...)` ekle — depends on: T023, T024, T025
- [X] T027 [P] [US2] `Stock.Api` `Features/Commands/IncreaseStock.cs`: `Store` sonrası
      `StockChangedEvent(cmd.ProductId, IsInStock: stock.Quantity > 0, DateTime.UtcNow)` yayınla
      (handler'a `IMessageBus bus` ekle)
- [X] T028 [P] [US2] `Stock.Api` `Features/Commands/DecreaseStock.cs`: aynı desen
- [X] T029 [P] [US2] `Stock.Api/StockEventHandlers.cs` `ProductCreatedHandler`: yeni açılan her
      `ProductStock` için `StockChangedEvent` yayınla (ilk stok kaydı — `IsInStock: quantity > 0`)
- [X] T030 [US2] `Stock.Api/Program.cs`: `stock.changed` exchange'i declare et
      (`BindQueue(RabbitMqConstants.StockChanged.Queues.Storefront)`),
      `opts.PublishMessage<StockChangedEvent>().ToRabbitExchange(...)` — depends on: T027, T028, T029
- [X] T031 [P] [US2] `Domains/StorefrontView/Ingestion/ProductChangedHandler.cs`: `session.LoadAsync<CatalogInfo>`
      → yoksa `CatalogInfo.Create(...)` + `Store`, varsa `TryApply(...)` `true` dönerse `Store` — depends on: T011
- [X] T032 [P] [US2] `Domains/StorefrontView/Ingestion/StockChangedHandler.cs`: aynı desen `StockInfo` için — depends on: T012
- [X] T033 [US2] `Storefront.Api/Program.cs`: `UseWolverine` bloğuna RabbitMQ ekle
      (`UseRabbitMq(...).AutoProvision()`, `opts.ListenToRabbitQueue(ProductChanged.Queues.Storefront)`,
      `opts.ListenToRabbitQueue(StockChanged.Queues.Storefront)`, `ScopeAuthorizationMiddleware` policy,
      `opts.Discovery.IncludeAssembly(...)`) — depends on: T014, T031, T032

**Checkpoint**: Catalog/Stock değişiklikleri Storefront'a idempotent şekilde yansır (US3 hariç).

---

## Phase 5: User Story 3 - Ürüne özel indirim tanımlanabilir (Priority: P2)

**Goal**: `Discount.Api` kullanıcı-bazlı modelden ürün-bazlı modele döner (research.md madde 7);
`SetProductDiscount`/`RemoveProductDiscount` `DiscountChangedEvent` yayınlar; Storefront'a yansır.

**Independent Test**: Bir ürüne indirim tanımlanır → Discount'un kendi sorgusundan doğrulanır →
yeniden tanımlanınca üzerine yazılır (tek aktif oran) → kaldırılınca Discount'ta 404, Storefront'ta
`discountRate: null` döner.

### Implementation for User Story 3

- [X] T034 [US3] `Discount.Api/Domains/Discounts/Discount.cs`: `UserId`+`Code` yerine `ProductId`;
      `Create(Guid productId, decimal rate)` — `productId == Guid.Empty` ise hata, yoksa
      `DiscountRate.Create(rate)` ile doğrula (aralık/validasyon DEĞİŞMEZ, sadece imza)
- [X] T035 [US3] Kaldır: `Domains/Discounts/ValueObjects/DiscountCode.cs`, `DiscountCodeGenerator.cs`,
      `Features/Commands/CreateDiscount.cs`, `Features/Queries/GetDiscountByCode.cs`,
      `Features/Agent/GetDiscountByCode.cs`
- [X] T036 [US3] `EventHandlers.cs`: `OrderCreatedHandler`'ı kaldır (dosyada başka handler yoksa dosyayı sil)
- [X] T037 [US3] `Features/Commands/SetProductDiscount.cs`: `SetProductDiscountCommand(Guid ProductId, decimal Rate)`
      — `ProductId`'ye göre var olan `Discount`'u bul, varsa güncelle yoksa `Discount.Create` ile oluştur
      (upsert, FR-012), `Store` sonrası `DiscountChangedEvent(ProductId, Rate, DateTime.UtcNow)` yayınla;
      var olmayan/silinmiş `ProductId` reddi Catalog'a senkron sorgu GEREKTİRMEZ — Discount kendi tarafında
      yalnızca kendi verisini doğrular (FR-013, bkz. research.md madde 1: cross-service senkron çağrı yok)
- [X] T038 [US3] `Features/Commands/RemoveProductDiscount.cs`: `ProductId`'ye göre bul, yoksa `NotFound()`,
      bulursa aggregate'i soft-delete et (`IsDeleted = true`, `Product.Delete()` deseniyle aynı),
      `Store` sonrası `DiscountChangedEvent(ProductId, Rate: null, DateTime.UtcNow)` yayınla
- [X] T039 [US3] `Features/Queries/GetDiscountByProductId.cs`: `ProductId`'ye göre `!IsDeleted` filtresiyle
      sorgula, `[RequiredScope(AuthorizationScopes.DiscountRead)]`
- [X] T040 [US3] `DiscountEndpointExtension.cs`: grup route'larını `GET/PUT/DELETE /products/{productId:guid}`
      olacak şekilde güncelle (`SetProductDiscount` → `DiscountWrite`, `RemoveProductDiscount` → `DiscountWrite`,
      `GetDiscountByProductId` → `DiscountRead`) — depends on: T037, T038, T039
- [X] T041 [US3] `DiscountMcpTools.cs`: kod-bazlı `get_discount` tool'unu `GetDiscountByProductId`'yi saran
      `get_discount_by_product` ile değiştir — depends on: T039
- [X] T042 [US3] `Discount.Api/Program.cs`: `OrderCreated` exchange declare + `Queues.Discount` listen
      satırlarını kaldır; `discount.changed` exchange'i declare et
      (`BindQueue(RabbitMqConstants.DiscountChanged.Queues.Storefront)`),
      `opts.PublishMessage<DiscountChangedEvent>().ToRabbitExchange(...)` ekle — depends on: T036, T037, T038
- [X] T043 [P] [US3] `Domains/StorefrontView/Ingestion/DiscountChangedHandler.cs`: `ProductChangedHandler`
      ile aynı desen, `DiscountInfo` için (`Rate: null` geçerli bir uygulama) — depends on: T013
- [X] T044 [US3] `Storefront.Api/Program.cs`: `opts.ListenToRabbitQueue(DiscountChanged.Queues.Storefront)`
      ekle (T033'ün RabbitMQ bloğuna) — depends on: T033, T043
- [X] T045 [P] [US3] `Discount.cs` aggregate testleri: `Create(productId, rate)` geçerli/boş-productId/
      geçersiz-rate senaryoları; eski `UserId`/`Code`-bazlı testleri güncelle
      `tests/Discount.Api.Tests/DiscountTests.cs`; artık geçersiz `DiscountCodeTests`'i sil
- [X] T046 [P] [US3] `DiscountInfo.TryApply`'ye `Rate: null` (indirim kaldırma) senaryosu ekle —
      T017'de zaten varsa atla, yoksa `DiscountInfoTests.cs`'e ekle

**Checkpoint**: 3 user story de kod-tamam; tam event zinciri (Catalog/Stock/Discount → Storefront) çalışır.

---

## Phase 6: Bootstrap (FR-011, cross-cutting)

**Purpose**: İlk açılışta mevcut ürünler için başlangıç doldurması (research.md madde 5) — hiçbir
story'e özel değil, US1'in doküman tiplerine ve US2/US3'ün upsert mantığına bağımlı.

- [X] T047 [P] `Catalog.Api`: `GetAllProducts.GetAllProductsQuery`'e `[RequiredScope(AuthorizationScopes.CatalogRead)]`
      ekle (repo konvansiyonu). Mevcut `GET api/v1/products` REST ucu (zaten var) bootstrap'ın kaynağıdır —
      MCP tool eklenmedi (implementasyonda kullanıcıyla görüşülüp düz HTTP'ye karar verildi, bkz. plan.md
      Complexity Tracking)
- [X] T048 [P] `Stock.Api`: yeni `Features/Queries/GetAllStock.cs` (`Query<ProductStock>().ToListAsync`,
      `[RequiredScope(AuthorizationScopes.StockRead)]`) + `GET api/v1/stocks/all` REST ucu (MCP tool DEĞİL —
      bkz. T047 notu)
- [X] T049 [P] `Discount.Api`: yeni `Features/Queries/GetAllProductDiscounts.cs`
      (`Query<Discount>().Where(!IsDeleted).ToListAsync`, `[RequiredScope(AuthorizationScopes.DiscountRead)]`)
      + `GET api/v1/discounts/all` REST ucu (MCP tool DEĞİL) — depends on: T034
- [X] T050 `Storefront.Api`: `appsettings.Development.json`'a `Bootstrap:IdentityServer` bölümü
      (`Authority`, `ClientId: "m2m.client"`, `ClientSecret: "dev-secret"` — Identity.Server/Config.cs'teki
      MEVCUT `m2m.client` zaten `catalog.read discount.read stock.read` scope'larına sahip, YENİ client
      GEREKMEZ) + `Program.cs`'te `"identity"` + `"catalog-api"`/`"stock-api"`/`"discount-api"` adlı
      `HttpClient`'lar (Aspire service discovery adresleriyle)
- [X] T051 `Bootstrap/StorefrontBootstrapHostedService.cs` (`IHostedService`): `StartAsync`'te
      `Duende.IdentityModel.Client` ile client_credentials token al (WebApp `TokenService.GetClientAccessTokenAsync`
      deseniyle), ardından `catalog-api`/`stock-api`/`discount-api`'nin `GET .../all` (Catalog için `/`) REST
      uçlarını Bearer token'lı düz `HttpClient.GetFromJsonAsync<T>` ile çağır — MCP client KULLANILMADI
      (implementasyon kararı, bkz. plan.md Complexity Tracking), dönen kayıtları
      `CatalogInfo.Create`/`StockInfo.Create`/`DiscountInfo.Create` ile (Ingestion handler'larıyla aynı
      idempotent-upsert mantığı) session'a yaz — depends on: T047, T048, T049, T050
- [X] T052 `Storefront.Api/Program.cs`: `builder.Services.AddHostedService<StorefrontBootstrapHostedService>()`
      kaydet — depends on: T051

**Checkpoint**: Fresh DB ile açılan Storefront, mevcut ürünler için event beklemeden veri ile dolar.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T053 [P] `dotnet build` — tüm çözüm (yeni Storefront.Api + değişen Catalog/Stock/Discount) derlenir
- [X] T054 [P] `dotnet test` — yeni/güncellenen tüm unit testler (Storefront.Api.Tests, Discount.Api.Tests) geçer
- [X] T055 Aspire AppHost üzerinden quickstart.md Senaryo 1/2/3 + Kısmi satır + Bootstrap doğrulamasını
      canlı çalıştır — TÜM senaryolar geçti. Canlı doğrulama sırasında 2 gerçek bug bulunup düzeltildi:
      (1) `DiscountRate`/`DiscountCode`-şekilli value object'ler Marten/Newtonsoft round-trip'inde
      `[JsonConstructor]` olmadan deserialize edilemiyordu (`DiscountRate.cs`'a eklendi; aynı şekle
      sahip `Basket.Api/Domains/Baskets/ValueObjects/Discount.cs` kapsam dışı, ayrı takip gerekir);
      (2) `Storefront.Api/Program.cs` `AddServiceDefaults()` çağırmıyordu — bootstrap'ın `http://catalog-api`
      gibi mantıksal adları çözmesi için gereken Aspire service-discovery HttpClient handler'ı eksikti
      (DNS hatası: "nodename nor servname provided"). Ayrıca T001/T007 eksik kalan `Storefront.Api/
      Properties/launchSettings.json` ve `AppHost.csproj` ProjectReference'ı da bu adımda tamamlandı.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Bağımsız — hemen başlar
- **Foundational (Phase 2)**: Setup'a bağımlı değil ama pratikte T001 sonrası yapılır — TÜM story'leri BLOKLAR
- **US1 (Phase 3)**: Foundational bitince başlar — okuma tarafı, diğer story'lerden bağımsız
- **US2 (Phase 4)**: Foundational bitince başlar (US1'e paralel çalışabilir, ayrı dosyalar); canlı
  uçtan-uca doğrulama US1'in endpoint'ine bağımlı
- **US3 (Phase 5)**: Foundational bitince başlar; T043/T044 US2'nin T033'üyle aynı `Program.cs`
  bloğuna dokunduğu için T033'ten SONRA yapılmalı (dosya çakışması, gerçek bir bağımlılık)
- **Bootstrap (Phase 6)**: US1 (doküman tipleri) + US2/US3'ün upsert mantığı + US3'ün Discount
  sorgusu tamamlanmış olmalı (T049 → T034'e bağımlı)
- **Polish (Phase 7)**: Tüm önceki fazlar bitince

### Kritik Dosya-Çakışması Notu

T033 ve T044 aynı `Storefront.Api/Program.cs` RabbitMQ bloğunu düzenler — sırayla yapılmalı (US2
önce, US3 sonra ekler), paralel [P] değildir.

---

## Parallel Example: Foundational + US1

```bash
# Foundational (Phase 2) — 3 doküman tipi paralel:
Task: "CatalogInfo.cs doküman tipi + TryApply"
Task: "StockInfo.cs doküman tipi + TryApply"
Task: "DiscountInfo.cs doküman tipi + TryApply"

# US1 (Phase 3) — testler paralel:
Task: "CatalogInfo.TryApply testleri"
Task: "StockInfo.TryApply testleri"
Task: "DiscountInfo.TryApply testleri"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Setup + Foundational tamamla
2. US1 (Phase 3) tamamla — okuma tarafı kod-tamam
3. **DUR ve DOĞRULA**: `CatalogInfo`/`StockInfo`/`DiscountInfo`'yu elle (Marten üzerinden) seed edip
   `GET /storefront/v1/products/{id}` çağır — kısmi satır davranışını doğrula
4. Canlı demo için US2 (event beslemesi) gerekir — MVP kod-tamam ama veri-boş demodur

### Incremental Delivery

1. Setup + Foundational → temel hazır
2. US1 → okuma API'si kod-tamam
3. US2 → Catalog/Stock event'leri canlı akar → görünüm gerçek zamanlı güncellenir
4. US3 → Discount ürün-bazlı modele döner, indirim alanı canlı akar
5. Bootstrap → fresh ortamda geçmiş veri de dolar
6. Her adım quickstart.md'nin ilgili senaryosuyla doğrulanır

---

## Notes

- [P] görevler farklı dosyalarda, tamamlanmamış bir göreve bağımlı değil
- Handler'lar (`IDocumentSession`/`IQuerySession` gerektiren) repo konvansiyonu gereği unit test
  EDİLMEZ — yalnızca pure `TryApply`/`From` metotları test edilir (bkz. Tests bölümü)
- Her checkpoint'te durup o faza kadarki işi doğrula
- Kaçının: aynı dosyada çakışan paralel task, story'ler-arası gizli bağımlılık (T033/T044 hariç —
  gerekçeli, bkz. Kritik Dosya-Çakışması Notu)