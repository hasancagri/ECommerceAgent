# Implementation Plan: Discount'ın Sistemden Tamamen Kaldırılması

**Branch**: `018-remove-discount` | **Date**: 2026-07-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/018-remove-discount/spec.md`

## Summary

Discount BC'si (servis + DB + testler) silinir; ona dokunan tüm izler (Basket kuponu, Storefront bileşimi,
IngestionAgent DiscountWrite, kontratlar, Identity scope'ları, WebApp UI, ChatAgent MCP) temizlenir.
Yaklaşım: dıştan içe kaldırma — önce tüketiciler (UI/agent), sonra kontratlar, en son servisin kendisi.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Aspire (AppHost), Marten, Wolverine (+RabbitMQ), Duende, YARP, MAF, Refit

**Storage**: Postgres (Marten); `discountDb` ve `discountManagement` şeması tamamen düşer

**Testing**: xUnit + Shouldly; saf domain birim testleri

**Target Platform**: Aspire ile orkestre edilen dağıtık .NET servisleri (dev: macOS)

**Project Type**: Mikroservis çözümünden bir BC'nin sökülmesi (yalnız silme/sadeleştirme)

**Performance Goals**: Hedef değişmez; bir servis + bir DB eksilir, kalan akışlar aynı hızda kalır

**Constraints**: Build + tüm testler yeşil; `dotnet run` (AppHost) Discount olmadan tam ayağa kalkar

**Scale/Scope**: ~45 kod dosyası (silme/düzenleme), 1 proje + 1 test projesi silinir, 10 test dosyası güncellenir

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Değerlendirme | Durum |
|------|---------------|-------|
| I. BC İzolasyonu | BC bütün olarak kalkar; kalan BC'ler yalnız kontrat değişikliği görür, DB erişimi yok | PASS |
| II. Zengin Aggregate | Yeni aggregate yok; Basket/Order/StorefrontView'dan davranış+alan silinir, invariant'lar korunur | PASS |
| III. Vertical Slice + CQRS | Slice'lar bütün olarak silinir; kalan slice yapısı değişmez, repository eklenmez | PASS |
| IV. Result Pattern | Yeni hata yolu yok; silinen slice'larla resource sabitleri de kalkar | PASS |
| V. Scope-Tabanlı Yetki | `discount.read/write` scope'ları tanımdan ve taleplerden silinir; rol eklenmez | PASS |
| Anayasa notu | Anayasa v1.3.0 İlke I örneğinde "Discount" geçer (açıklayıcı örnek); ilke değişmez, amendment gerekmez | PASS |

Post-design re-check: PASS — tasarım yalnız silme; hiçbir ilkeye yeni yük binmez. Anayasadaki
Discount örneği implementasyonda PATCH amendment ile başka örneğe çevrilebilir (opsiyonel, T-görevi).

## Project Structure

### Documentation (this feature)

```text
specs/018-remove-discount/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 — kaldırma sırası + riskler
├── data-model.md        # Faz 1 — değişen entity'lerin son hali
├── quickstart.md        # Faz 1 — canlı doğrulama rehberi
├── contracts/           # Faz 1 — değişen/silinen kontratlar
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root)

```text
SİLİNİR:
src/services/discount/                          # Discount.Api projesi (tamamı)
tests/Discount.Api.Tests/                       # Discount test projesi (tamamı)
src/agents/IngestionAgent/Workflows/05_DiscountWrite/
src/services/basket/.../Features/{Commands,Agent}/{Apply,Remove}DiscountCoupon.cs
src/services/basket/.../ValueObjects/Discount.cs
src/ui/WebApp/Services/Refit/IDiscountRefitService.cs
src/ui/WebApp/Pages/Basket/Dto/{GetDiscountByCouponResponse,ApplyDiscountRateRequest}.cs

DÜZENLENİR:
ECommerceAgent.slnx                # 2 proje kaydı düşer
src/aspire/AppHost/AppHost.cs                   # discountDb + discount-api + 4 referans
src/services/gateway/Gateway/appsettings.Development.json  # 2 route + 1 cluster
src/others/Shared/{IntegrationEvents,RabbitMqConstants}.cs # event + sabitler
src/others/Shared/Utils/Constants/SchemaConstants.cs       # DiscountSchemaName
src/others/Common/Utils/Constants/AuthorizationScopes.cs   # DiscountRead/Write
src/others/Identity.Server/Config.cs            # scope + resource + client talepleri
src/services/basket/...                         # Basket.cs, BasketItem, endpoint/MCP/GetBasket
src/services/order/...                          # Order.cs, CreateOrder
src/services/storefront/...                     # StorefrontView, EventHandlers, 2 query
src/services/supplier/Supplier.Api/...          # feed kontratı + products.json
src/services/supplier/Supplier.Gateway/...      # SupplierFeedAdapter
src/agents/IngestionAgent/...                   # handler, Program, ConstValues, WriterResult, GlobalUsings
src/agents/ChatAgent/{Program,ConstValues}.cs   # MCP kaydı + araçlar + talimatlar
src/ui/WebApp/...                               # Program, TokenService, BasketService, OrderService,
                                                # StorefrontService, sayfalar, DTO/ViewModel'ler
tests/{Basket,Order,Storefront,Supplier.Gateway,IngestionAgent}.*/  # discount izleri temizlenir
```

**Structure Decision**: Mevcut yapı korunur; yalnız Discount BC'si ve izleri kaldırılır. Yeni klasör açılmaz.

## Complexity Tracking

Anayasa ihlali yok — tablo boş.