# Tasks: Kişisel Ana Sayfa (Sipariş-Temelli Heuristik Feed)

**Input**: Design documents from `/specs/054-personal-home-feed/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: İlke VI gereği saf feed sıralayıcısı (ranker) TEST-FIRST (T003, implementasyonu T007'den
önce). Handler/endpoint/UI için test task'ı yok (canlı doğrulama quickstart'ta).

**Organization**: Task'lar user story bazında; her story bağımsız doğrulanabilir.

## Phase 1: Setup

- [X] T001 Branch aç: master'dan `054-personal-home-feed` (implement bu branch'te)
- [X] T002 `tests/Storefront.Api.Tests` projesi yoksa oluştur (xUnit + Shouldly, Central Package
      Management sürümsüz referans) ve `ECommerceAgent.slnx`'e bağla; varsa geç

## Phase 2: Foundational

Yok — feature tek BC + WebApp dokunuşu; tüm iş story fazlarında. (Boş-doğru task üretilmedi.)

---

## Phase 3: User Story 1 — Alışveriş geçmişi olan kullanıcı kişisel vitrin görür (P1) 🎯 MVP

**Goal**: `OrderCompleted` → `UserPurchase` birikimi + kişisel feed endpoint'i + ana sayfada feed.

**Independent Test**: quickstart S1 — sipariş tamamla, ≤1 dk sonra ana sayfada yalnız
kategori/yazar eşleşmeli, alınmamış kitaplar (≤12 kart).

### Tests (İlke VI — önce yaz, FAIL gör)

- [X] T003 [US1] Saf ranker testleri (FAIL durumda):
      `tests/Storefront.Api.Tests/PersonalFeedRankerTests.cs` — kapsam: yazar eşleşmesi kategori
      eşleşmesinden önce; tie → RatingAverage DESC (null son) → Name ASC; satın alınan ProductId
      ve FamilyCode elenir; aile → tek temsilci (stok>0 önce, ucuz önce, ProductId ASC); aynı
      kitap tek kez; 12'ye kesme; boş girdi → boş sonuç

### Implementation

- [X] T004 [P] [US1] `UserPurchase` dokümanı:
      `src/services/storefront/Storefront.Api/Domains/UserPurchase/UserPurchase.cs` —
      `Id` (kompozit `"{UserId:N}:{ProductId:N}"`), `UserId`, `ProductId`, statik `KeyFor` +
      `Create` (read-model, aggregate DEĞİL; Reviews `PurchasedProduct` emsali)
- [X] T005 [US1] `src/services/storefront/Storefront.Api/Program.cs` — `Schema.For<UserPurchase>()`
      + `Index(x => x.UserId)`; `order.completed` exchange → `storefront.events` kuyruğuna
      `BindQueue` (tüketici binding kurar; mevcut 3-exchange desenine 4. eklenir)
- [X] T006 [US1] `src/services/storefront/Storefront.Api/StorefrontEventHandlers.cs` —
      `Handle(OrderCompleted)` overload: her `Items[].ProductId` için
      `session.Store(UserPurchase.Create(evt.UserId, item.ProductId))` (PK upsert = idempotent)
- [X] T007 [US1] Saf ranker implementasyonu (T003 yeşile döner):
      `src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetPersonalFeed.cs`
      içinde internal static saf yardımcı (in-memory satırlar üzerinde eşleşme türü + eleme +
      aile temsilcisi + sıralama + 12 kesme)
- [X] T008 [US1] `GetPersonalFeed` query slice'ının kalanı (aynı dosya): query record + Handler
      (`IQuerySession`: `UserPurchase[userId]` → sahip olunan `StorefrontView` satırları →
      kategori/yazar/aile kümeleri → satılabilir aday sorgusu [Name+Price dolu, `!IsDeleted`,
      `Authors.Any` jsonb / `CategoryId` eşleşmesi] → ranker) + `PersonalFeedItemResponse`
      (contracts/personal-feed-endpoint.md şekli, `matchType` dahil) +
      `FeatureListResultModel<T>` dönüşü
- [X] T009 [US1] Endpoint: aynı slice'ta `MapGet("/personal-feed")` — `CurrentUser.Load`,
      `.RequireAuthorization` (scope `storefront.read`);
      `src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontViewEndpointExtension.cs`'e
      map satırı
- [X] T010 [US1] WebApp istemcisi:
      `src/ui/WebApp/Services/Refit/IStorefrontRefitService.cs` —
      `[Get("/api/v1/storefront/products/personal-feed")]`;
      `src/ui/WebApp/Services/StorefrontService.cs` — `GetPersonalFeedAsync()`
- [X] T011 [US1] Ana sayfa feed çizimi: `src/ui/WebApp/Pages/Index.cshtml` +
      `src/ui/WebApp/Pages/Index.cshtml.cs` — authenticated ise personal-feed çağır, kartları
      mevcut ürün-kart düzeniyle listele (anonim/boş dal T012'de)

**Checkpoint**: Siparişli kullanıcı ana sayfada kişisel liste görür (quickstart S1).

---

## Phase 4: User Story 2 — Sinyalsiz kullanıcı boş durum + kategori yönlendirmesi (P2)

**Goal**: Anonim/siparişsiz kullanıcıya ürün listesi YOK; boş durum + kategori kartları.

**Independent Test**: quickstart S2 — gizli pencere + siparişsiz yeni üye; ürün kartı yok,
kategori kartı var, kart tıklaması kategori listesine gider.

### Implementation

- [X] T012 [US2] `src/ui/WebApp/Pages/Index.cshtml.cs` — anonim ise personal-feed'i HİÇ çağırma;
      anonim VEYA feed boş → kategori verisini mevcut kaynaktan yükle
      (`Categories/Index`'in kullandığı servis yolu yeniden kullanılır, yeni endpoint YOK)
- [X] T013 [US2] `src/ui/WebApp/Pages/Index.cshtml` — boş durum bölümü: "keşfe başla" mesajı +
      kategori kartları (`/Products?categoryId=...` linkli); fallback ürün listesi YOK

**Checkpoint**: US1 + US2 birlikte: her kullanıcı tipi doğru ana sayfa görür.

---

## Phase 5: User Story 3 — Genel vitrin öğeleri kalkar, gezinme bozulmaz (P3)

**Goal**: "Öne Çıkan Kitaplar", "Tüm Kitaplara Göz At" ve navbar "Tüm Kitaplar" gider;
`/Products` + kategori gezinmesi aynen kalır.

**Independent Test**: quickstart S3 — ana sayfa kaynağında bu öğeler yok; kategori/yazar/yayınevi
listeleri 054 öncesiyle aynı.

### Implementation

- [X] T014 [US3] `src/ui/WebApp/Pages/Index.cshtml` — "Öne Çıkan Kitaplar" bölümü +
      "Tüm Kitaplara Göz At" bağlantısı sil; `Index.cshtml.cs`'ten `GetProductsAsync` (filtresiz
      liste) çağrısını kaldır
- [X] T015 [P] [US3] `src/ui/WebApp/Pages/Shared/_Layout.cshtml` — navbar "Tüm Kitaplar" girişi
      sil; "Tüm Kategoriler" ve diğer girişler DURUR
- [X] T016 [US3] Regresyon kontrolü: `/Products?categoryId=...`, yazar/yayınevi/spec filtreleri,
      arama — davranış değişmedi (canlı, quickstart S3.3)

**Checkpoint**: Üç story tamam; ana sayfa saf kişisel.

---

## Phase 6: Polish & Cross-Cutting

- [X] T017 [P] `src/services/storefront/FLOW.md` — süreç adımı ekle: Order kaynağı
      (`OrderCompleted → UserPurchase` birikimi) + kişisel feed okuma adımı + sınır güncelle;
      kenar-anchor tip adları kodla eşleşsin
- [X] T018 [P] `CLAUDE.md` BC haritası storefront satırına kişisel feed/`UserPurchase` ibaresi
      ekle (satır ≤300 karakter kuralına dikkat)
- [X] T019 Build + testler: `dotnet build` + `dotnet test` (T003 dahil tümü yeşil);
      `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` PASS
- [X] T020 Canlı doğrulama: quickstart S1–S4 (Aspire AppHost; öncesinde `customer` rolünde
      `storefront.read` scope işaretli mi kontrol — değilse admin ekranından işaretle)

---

## Dependencies & Execution Order

- **Phase 1** → her şeyden önce (T002, T003'ün önkoşulu).
- **US1 içi**: T003 (test, FAIL) → T007 (ranker, yeşil). T004 → T005/T006. T007+T004 → T008 →
  T009 → T010 → T011. T004 diğerlerinden bağımsız başlayabilir [P].
- **US2**: T011 sonrası (aynı dosyalar: Index.cshtml/.cs) — çakışmayı önlemek için sıralı.
- **US3**: T014 Index dosyalarına dokunur → US2 sonrası; T015 [P] (_Layout, bağımsız).
- **Polish**: tüm story'ler sonrası; T017/T018 [P].

### Parallel Opportunities

- T004 (UserPurchase.cs) ‖ T003 (test dosyası) — farklı dosyalar.
- T015 (_Layout) ‖ T014 dışındaki US3 işleri.
- T017 ‖ T018 (FLOW.md / CLAUDE.md).
- UI story'leri (US2/US3) aynı dosyaları paylaştığından sıralı yürütülür — tek geliştirici için
  doğal akış zaten P1→P2→P3.

## Implementation Strategy

- **MVP = US1**: T001–T011 sonrası dur, quickstart S1 ile doğrula (siparişli kullanıcı feed'i).
  Bu noktada eski "öne çıkan" bölümü hâlâ durur — S1 doğrulaması onu görmezden gelir.
- **Increment 2 = US2**: boş durum; anonim/yeni kullanıcı deneyimi tamamlanır (S2).
- **Increment 3 = US3**: genel vitrin sökümü + regresyon (S3).
- **Kapanış**: Polish (FLOW/CLAUDE/guard + canlı S1–S4) → PR.