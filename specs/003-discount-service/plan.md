# Implementation Plan: Discount.Api — Kampanya BC'sinin Dönüşü

**Input**: `specs/003-discount-service/spec.md` · **Tarih**: 2026-11-02

## Teknik yapı (Stock.Api 002 deseninin aynısı)

- **Proje:** `src/services/discount/Discount.Api` + `tests/Discount.Api.Tests` + `.slnx`; AppHost: `discountDb` + servis kaydı (health check + rabbit); `SchemaConstants.DiscountSchemaName` YENİ eklenir (eskisi listede yok).
- **Aggregate:** `Domains/Campaigns/Campaign.cs` — `ProductId`, `Rate` (decimal 0.01-0.90), `StartsAt`/`EndsAt` (DateTimeOffset), `IsCancelled`. Davranışlar: `Create(productId, rate, startsAt, endsAt)` (guard'lar), `Cancel()` (idempotent), `IsActiveAt(now)` saf karar metodu (pencere ∧ !cancelled). Zaman DIŞARIDAN verilir (`Date.Now` domain'de yok → test edilebilir).
- **Etkin-kampanya seçimi:** aynı ürüne çakışan kampanyalarda SON OLUŞTURULAN kazanır — karar sorgu/süreç katmanında (CreatedAt sırası), aggregate çakışmayı bilmez (BC-içi süreç işi).
- **Kontrat:** `IntegrationEvents.ProductDiscountChanged(Guid ProductId, decimal? Rate)` (null = indirim yok) + `RabbitMqConstants.ProductDiscountChanged` (exchange `discount.product-discount-changed`, queue `catalog.product-discount-changed`). Yayın YÜRÜRLÜK anında.
- **Yürürlük süreci:** `Process/CampaignWindowProcess.cs` (BackgroundService, dakikalık tick — EmbeddingReconcile iskeleti referans ama Marten'li): her turda ürün-başına etkin oranı hesaplar, SON DUYURULAN oranla karşılaştırır (kampanya üstünde `LastAnnouncedRate`? — hayır: ayrı küçük `ProductDiscountState` dokümanı: productId + announcedRate; fark görünce event + state güncelle). Fail-soft, idempotent.
- **Catalog tarafı:** `Product.DiscountRate` (decimal?, default null) + `SetDiscountRate(decimal?)` (0-0.90 guard; null = temizle) — IsSalable'a GİRMEZ; kökte `DiscountConsumers.cs` (DLQ deseni: ürün yoksa throw) + binding + IncludeType.
- **FLOW.md:** `src/services/discount/FLOW.md` + Catalog FLOW'una ⇦ Discount dikişi; CLAUDE.md BC satırı.
- **Test:** `CampaignTests` (guard'lar + IsActiveAt pencere/iptal) + Catalog `ProductTests`'e DiscountRate testleri. Süreç/consumer canlı doğrulanır.

## Riskler/notlar

- Süreç state'i (`ProductDiscountState`) Discount DB'sinde — "en son ne duyurdum" hafızası; restart'ta kayıp yok, fark-tabanlı duyuru tekrar güvenli (idempotent tüketici).
- launchSettings portu 5390 (boş).
- Scope: `AuthorizationScopes`'ta discount scope'ları 095'te silindi mi kontrol edilir; yoksa `DiscountWrite` eklenir (KnownScopes kapalı registry kuralı — Program.cs yalnız mevcut sabiti kullanır).