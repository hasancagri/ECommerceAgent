# Tasks: Catalog Kitap Domain Redesign (sade künye modeli)

**Input**: Design documents from `/specs/095-catalog-book-domain/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R7), data-model.md, contracts/product-changed-event.md, quickstart.md

**Tests**: İlke VI (Domain-TDD) — aggregate davranışları + saf çekirdekler (zincir çözümü, ağaç kurucu,
resolver) test-first; test task'ı implementasyonundan ÖNCE. Handler/seed-wiring/consumer test-sonra
veya canlı doğrulama.

**Organization**: User story bazlı; model redesign'ın kendisi tüm story'lerin ön koşulu olduğundan
Foundational'da (US3'ün söküm özü burada erir — spec US3 "US1/US2'siz değer üretmez" der).

> **KAPSAM DEĞİŞİKLİĞİ (2026-10-31):** Discount BC implement sırasında kullanıcı kararıyla KOMPLE
> söküldü — T015-T018 önce yapıldı, sonra BC ile birlikte kalktı (tarihçe git'te). Söküm işleri:
> `src/services/discount` + testleri + `discount_query.proto` + Order DiscountClient kablosu +
> `discount.*` scope'ları + `discount.events` kuyruğu + AppHost kaydı silindi; FLOW/CLAUDE.md hizalandı.
> `ProductChangedEvent.CategoryIds` kontratta kaldı (096 keşif). T020'nin Discount maddeleri düştü.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

Görev yok — yeni proje/paket/klasör gerekmiyor; mevcut BC yerleşimi korunuyor (plan.md Structure Decision).

---

## Phase 2: Foundational (model redesign + kontrat — TÜM story'leri bloklar)

**⚠️ Not**: T003 sonrası çözüm Phase 3 sonuna dek derlenmeyebilir (seed eski modele bağlı) — ara commit'ler build-kırık olabilir, faz sonu checkpoint build değil test-odaklıdır.

- [X] T001 [P] Yeni Product davranış testleri YAZ (failing): `tests/Catalog.Api.Tests/ProductTests.cs` — `Create(name, gtin, price, description)` (fiyat>0, boş ad/gtin reddi), `SetDescription`, `SetCategory` (boş Guid reddi), `SetRelatedCategories` (dedup + leaf'i içermez); sökülen metot (Sku/Tag/Assign/Dimensions/Seo/Identifiers) testlerini sil
- [X] T002 [P] Yeni Category testleri YAZ (failing): `tests/Catalog.Api.Tests/CategoryBrandTests.cs` → `CategoryTests.cs` — `Create(name, parentCategoryId?)` boş ad reddi, NormalizedName üretimi; Rename/SetParent/Reorder/SetPublished/SetSeo testleri silinir (metotlar ölüyor)
- [X] T003 Product aggregate redesign: `src/services/catalog/Catalog.Api/Domains/Products/Product.cs` — data-model.md'deki alan/metot seti; sökümler FR-001/002; T001 yeşile döner
- [X] T004 Category sadeleştir: `src/services/catalog/Catalog.Api/Domains/Categories/Category.cs` — Name + NormalizedName + ParentCategoryId?, tek davranış `Create`; T002 yeşile döner
- [X] T005 [P] ProductTag komple söküm: `src/services/catalog/Catalog.Api/Domains/ProductTags/` sil + `tests/Catalog.Api.Tests/ProductTagTests.cs` sil (FR-003)
- [X] T006 [P] VO temizlik: `src/services/catalog/Catalog.Api/Domains/Products/ValueObjects/ProductValueObjects.cs` — ProductCategoryAssignment ve ProductDimensions sil; SeoMetadata'nın Catalog'da kullanımı kalmadıysa sil; Money kalır
- [X] T007 [P] Kategori zincir çözümü saf çekirdek testi YAZ (failing): `tests/Catalog.Api.Tests/CategoryChainResolverTests.cs` — leaf→(adlar: leaf+ata, id'ler: leaf+ata); köksüz leaf→tek eleman; related zincire GİRMEZ
- [X] T008 Zincir helper impl: `src/services/catalog/Catalog.Api/Helpers/CategoryChainResolver.cs` — saf çekirdek (in-memory kategori koleksiyonu üstünde); seed + FileConsumers paylaşır (R3); T007 yeşil
- [X] T009 Kontrat: `src/others/Shared/IntegrationEvents.cs` — `ProductChangedEvent`e `List<Guid>? CategoryIds = null` (additive) + `Categories` semantik yorumu (leaf+ata adları); bkz. contracts/product-changed-event.md

**Checkpoint**: Aggregate testleri yeşil; kontrat hazır — story fazları başlayabilir.

---

## Phase 3: User Story 1 - Seed yeni modelle katalogu doldurur (Priority: P1) 🎯 MVP

**Goal**: Açılışta 11.438 kitap yeni modele yüklenir; kategori ağacı (31 üst + ~180 alt) seed'den deterministik kurulur.

**Independent Test**: Temiz DB + AppHost açılışı; quickstart §2 sayımları (ürün/kategori, leaf/related atamaları, Gtin-atla idempotensi).

- [X] T010 [P] [US1] Ağaç kurucu saf çekirdek testi YAZ (failing): `tests/Catalog.Api.Tests/CategoryTreeSeedTests.cs` — path listesi→düğüm seti (≤2 seviye), ad ağaç-global unique ihlalinde deterministik hata, aynı path tekrarı idempotent (R4)
- [X] T011 [US1] Faz A impl: `src/services/catalog/Catalog.Api/Seeding/BookSeedHostedService.cs` — books.json tek tarama → saf ağaç kurucu (Seeding içinde) → Category.Create ile kapalı ağaç; T010 yeşil
- [X] T012 [P] [US1] `SeedBookRecord` hizası: `src/services/catalog/Catalog.Api/Seeding/SeedBookRecord.cs` + `tests/Catalog.Api.Tests/SeedBookRecordTests.cs` — alan seti aynı; Validate (ISBN/fiyat) korunur, derleme hizası
- [X] T013 [US1] Faz B impl: `BookSeedHostedService` satır işleme — Gtin-varsa-atla, `Create(name, gtin, price, description)`, tek leaf `SetCategory` ("Genel" fallback), `SetRelatedCategories` (çözülemeyen related atlanır), SetAuthors/SetPublisher/SetBookMeta/SetTranslators, ProductPriceChange append, `ProductAdded` + fat `ProductChangedEvent` (T008 helper'la Categories+CategoryIds) AYNI commit
- [ ] T014 [US1] Canlı doğrulama: quickstart §2 — temiz DB açılış sayımları + "Popüler Kültür" örneği + ikinci açılış idempotensi

**Checkpoint**: Katalog yeni modelle doluyor — MVP.

---

## Phase 4: User Story 2 - Downstream tüketiciler kesintisiz (Priority: P2)

**Goal**: Library/Discount `ProductChangedEvent`'ten beslenmeye devam eder; üst-kategori kampanyası leaf kitabı yakalar; kapak hattı (FileConsumers) geri gelir.

**Independent Test**: quickstart §3-§5 — event alan örneği (Categories/CategoryIds leaf+ata), Discount izdüşüm + kampanya kapsamı, kapak ImageUrl dolumu, Library alarm davranışı değişmedi.

- [X] T015 [P] [US2] Resolver testleri GÜNCELLE (failing önce): `tests/Discount.Api.Tests/CampaignSelectionResolverTests.cs` — Category kolu `CategoryIds.Contains`; üst-kategori scopeRef leaf kitabı bulur; diğer kollar değişmez
- [X] T016 [US2] İzdüşüm: `src/services/discount/Discount.Api/Domains/ProductCatalogRefs/ProductCatalogRef.cs` — `CategoryId: Guid` → `CategoryIds: List<Guid>`; Apply imzası hizalanır
- [X] T017 [US2] Resolver impl: `src/services/discount/Discount.Api/Domains/Campaigns/CampaignSelectionResolver.cs` — saf çekirdek + Marten sorgusu `CategoryIds.Contains(scopeRef)`; T015 yeşil
- [X] T018 [US2] Tüketici map: `src/services/discount/Discount.Api/CatalogConsumers.cs` — `evt.CategoryIds ?? [evt.CategoryId]` (null-dayanıklı, eski event kırılmaz)
- [X] T019 [US2] FileConsumers GERİ YAZ: `src/services/catalog/Catalog.Api/FileConsumers.cs` — CoverIngested → Gtin lookup → SetImage → fat ProductChangedEvent (T008 helper; referans `git show 7c641d9~1:src/services/catalog/Catalog.Api/FileConsumers.cs`); Wolverine keşfi: sınıf adı `*Consumers` — `MessagingExtensions`'taki `IncludeAssembly` yeter mi doğrula, gerekirse `IncludeType` ekle (TUZAK: sessiz yutma)
- [ ] T020 [US2] Canlı doğrulama: quickstart §3-§5 — RabbitMQ event örneği, discountDb `ProductCatalogRef`, kapak dolumu, CoverIngested kuyruğu birikmiyor

**Checkpoint**: US1 + US2 birlikte canlı doğrulanmış.

---

## Phase 5: User Story 3 - Ölü model sökümü ve test hizası (Priority: P3)

**Goal**: Sökümün artıkları temizlenir; çözüm 0 hata derlenir, tüm testler yeşil, ölü referans kalmaz (SC-004).

**Independent Test**: `dotnet build && dotnet test` + grep süpürmesi boş.

- [X] T021 [P] [US3] Repo-geneli ölü referans süpürme: `grep -rn "ProductTag\|SetIdentifiers\|AssignToCategory\|ProductCategoryAssignment\|ProductDimensions\|ShortDescription\|MarkAsNew\|ShowOnHomepage\|ParentGroupedProductId" src tests` — kalanları temizle (Shared kontrat alanları R6 gereği MUAF)
- [X] T022 [US3] Kalan Catalog testleri hiza: `tests/Catalog.Api.Tests/{ProductAuthorsTests,ProductBookMetaTests,ProductPublisherTests}.cs` — yeni Create imzasıyla derleme + yeşil
- [X] T023 [US3] Tam yeşil: `dotnet build && dotnet test` — 0 hata, tüm test projeleri geçer (bağımlı test projeleri dahil — rename TUZAĞI)

**Checkpoint**: SC-004 sağlandı.

---

## Phase 6: Polish & Cross-Cutting

- [X] T024 [P] FLOW.md hizası (FR-013, İlke VII): `src/services/catalog/FLOW.md` — tek-leaf + kapalı ağaç + iki-fazlı seed + FileConsumers as-built; `src/services/discount/FLOW.md` izdüşüm satırı (CategoryIds); `scripts/check-flow-links.sh` yeşil
- [X] T025 [P] CLAUDE.md BC haritası catalog satırı güncelle (ProductTag söküldü, tek-leaf model, origin'e `specs/095-catalog-book-domain` ekle); `scripts/check-claude-spec-links.sh` yeşil
- [ ] T026 quickstart.md tam koşu (§1-§6) — SC-001..004 kapanış kanıtı

---

## Dependencies & Execution Order

- **Phase 2 Foundational** her şeyi bloklar. İçinde: T001,T002,T005,T006,T007 paralel başlar; T003←T001; T004←T002; T008←T007+T004; T009 bağımsız [P].
- **US1 (Phase 3)**: Foundational sonrası. T010,T012 paralel; T011←T010+T004; T013←T011+T003+T008+T009; T014←T013.
- **US2 (Phase 4)**: T009 + (canlı doğrulama için US1) gerekir. T015 paralel başlar; T016←T015; T017←T016; T018←T016; T019←T008+T009 (US1'den bağımsız yazılabilir); T020←T013+T019.
- **US3 (Phase 5)**: US1+US2 sonrası süpürme. T021 paralel; T022; T023 en son.
- **Polish**: hepsinden sonra; T024/T025 paralel.

### Parallel Example: Foundational

```text
Aynı anda: T001 (ProductTests), T002 (CategoryTests), T005 (ProductTag söküm),
T006 (VO temizlik), T007 (zincir testi), T009 (Shared kontrat)
```

### Parallel Example: US2

```text
Aynı anda: T015 (resolver testleri), T019 (FileConsumers — Catalog tarafı, farklı BC/dosya)
```

## Implementation Strategy

- **MVP = Foundational + US1**: model + seed; temiz açılışta katalog doluyor (SC-001). Burada durup doğrula.
- Sonra US2 (event/Discount/kapak — SC-002/003), sonra US3 süpürme (SC-004), sonra Polish (FLOW/CLAUDE.md/guard).
- Commit düzeni: faz sonu checkpoint'lerde; Phase 2 ortasında build kırık olabilir (yukarıdaki not) — PR tek parça gider.
