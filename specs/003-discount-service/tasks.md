# Tasks: Discount.Api — Kampanya BC'sinin Dönüşü

**Input**: spec.md + plan.md · Domain-TDD: test task'ları implementasyondan ÖNCE.

## Phase 1: Setup

- [X] T001 `src/services/discount/Discount.Api` iskeleti (Stock.Api 002 kopyası: csproj/GlobalUsings/launchSettings(5390)/Program/Extensions/Dependencies/appsettings) + `tests/Discount.Api.Tests` + `.slnx` + `SchemaConstants.DiscountSchemaName` + scope kontrolü (`DiscountWrite` yoksa KnownScopes/AuthorizationScopes'a eklenir).
- [X] T002 AppHost: `discountDb` + discount servisi (health check + rabbit ref) + ghcr listesine ekle.

## Phase 2: US1 — Campaign domain'i (P1) 🎯 MVP

- [X] T003 [US1] Test-first `tests/Discount.Api.Tests/CampaignTests.cs`: `Create_ValidInput_SetsFields`, `Create_RateOutOfRange_ReturnsError` (0, negatif, >0.90), `Create_EndBeforeStart_ReturnsError`, `Create_EmptyProductId_ReturnsError`, `Cancel_ActiveCampaign_Deactivates`, `IsActiveAt_WithinWindow_ReturnsTrue`, `IsActiveAt_BeforeStartOrAfterEnd_ReturnsFalse`, `IsActiveAt_WhenCancelled_ReturnsFalse`.
- [X] T004 [US1] Implement `Domains/Campaigns/Campaign.cs` (Create/Cancel/IsActiveAt — zaman parametreyle) + `Constants/DiscountResourceConstants.cs`; MartenExtensions index (ProductId). T003 YEŞİL.

## Phase 3: US2 — Duyuru + Catalog oran kopyası (P2)

- [X] T005 [US2] Kontrat: `ProductDiscountChanged(Guid ProductId, decimal? Rate)` + RabbitMq sabitleri; Discount MessagingExtensions exchange deklare + publish kaydı.
- [ ] T006 [US2] ERTELENDİ (kullanıcı kararı): Yürürlük süreci `CampaignWindowProcess` + `ProductDiscountState` sonraki dilimde — duyuru mekanizması o gelince canlanır; bu dilimde kontrat+tüketici hazır bekler.
- [X] T007 [US2] Test-first `tests/Catalog.Api.Tests/ProductTests.cs`: `SetDiscountRate_Valid_SetsRate`, `SetDiscountRate_Null_ClearsRate`, `SetDiscountRate_OutOfRange_ReturnsError`, `SetDiscountRate_DoesNotAffectSalability`.
- [X] T008 [US2] Implement Catalog: `Product.DiscountRate` + `SetDiscountRate` (IsSalable'a girmez); kökte `DiscountConsumers.cs` (DLQ deseni) + binding + IncludeType. T007 YEŞİL.
- [X] T009 [US2] Test-first `tests/Discount.Api.Tests/CampaignSelectionTests.cs`: etkin-oran seçimi saf birimi — `ResolveActiveRate_NoActiveCampaign_ReturnsNull`, `ResolveActiveRate_SingleActive_ReturnsRate`, `ResolveActiveRate_Overlapping_LastCreatedWins`, sonra seçim mantığını saf static helper'a koy (`CampaignSelection.ResolveActiveRate`) ve süreç onu kullansın.

## Phase 4: Polish

- [X] T010 `src/services/discount/FLOW.md` + Catalog FLOW ⇦ Discount dikişi + CLAUDE.md BC satırı (+ catalog satırına DiscountRate kopyası).
- [X] T011 Tam doğrulama: `dotnet build && dotnet test` yeşil.

## Dependencies

Setup → US1 → US2 → Polish. T009 seçim helper'ı T006 sürecinden önce de yazılabilir (süreç onu çağırır) — uygulama sırası: T005 → T009 → T006.