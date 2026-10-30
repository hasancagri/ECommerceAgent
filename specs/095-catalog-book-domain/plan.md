# Implementation Plan: Catalog Kitap Domain Redesign (sade künye modeli)

**Branch**: `095-catalog-book-domain` | **Date**: 2026-10-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/095-catalog-book-domain/spec.md`

## Summary

Catalog domain modeli PDF-only Kitapyurdu gerçeğine indirgenir: sade Product künyesi (kimlik Gtin/ISBN,
tek leaf kategori + editoryal RelatedCategoryIds), kapalı 2 seviyeli Category ağacı (yalnız seed'den,
doğumda sabit ebeveyn), ProductTag + nop-mirası alanların sökümü. Teknik yaklaşım: aggregate redesign +
iki-fazlı seed (önce ağaç, sonra ürünler) + fat `ProductChangedEvent` (ad VE id zinciri yayın anında
ağaçtan çözülür). Arama/pgvector 096'da.

**Keşif bulguları (plan bunları kodlar):**

1. **Discount üst-kategori kampanyası bugün çalışmıyor:** `CampaignSelectionResolver.Matches` tek leaf
   Guid eşitliği (`p.CategoryId == scopeRef`). US2/SC-002 için event'e additive `CategoryIds` (leaf+ata,
   related HARİÇ) eklenir; Discount izdüşümü listeye geçer. Spec'in "Discount kodu değişmez"
   varsayımından ZORUNLU sapma — bkz. research.md R1.
2. **`Catalog.Api/FileConsumers.cs` temiz-sayfa sökümünde (7c641d9) yanlışlıkla silinmiş** — CoverIngested
   kuyruğu dinleniyor ama handler yok, kapak hattı kopuk. FR-011 gereği bu feature'da yeni modele göre
   geri yazılır (eski içerik git tarihinde referans).

## Technical Context

**Language/Version**: .NET 10 (C#, Nullable + ImplicitUsings)

**Primary Dependencies**: Marten (Postgres doc-store, Newtonsoft), Wolverine (in-proc bus + RabbitMQ fanout), Scrutor

**Storage**: catalogDb (Marten; şema `ApplyAllDatabaseChangesOnStartup`, migration komutu yok) + discountDb izdüşümü

**Testing**: xUnit + Shouldly, saf unit (`tests/Catalog.Api.Tests`, `tests/Discount.Api.Tests`); host harness yok

**Target Platform**: Aspire AppHost (tüm sistem birlikte açılır)

**Project Type**: Mikroservis BC'leri (Catalog + Discount izdüşümü + Shared kontrat)

**Performance Goals**: Açılış seed 11.438 satır kabul edilebilir sürede (mevcut satır-satır akış korunur)

**Constraints**: Dış yüzey YOK (MCP söküldü, REST henüz yok) → geri-uyum ekseni dar; temiz-DB açılışı hedef, migration yazılmaz

**Scale/Scope**: 11.438 kitap, 31 üst + ~180 alt kategori; 2 BC + Shared kontrat + FLOW.md

**Artefakt kademesi**: Tam — event kontrat etkisi (İlke I) + aggregate redesign + iki BC.

## Constitution Check

*GATE: Phase 0 öncesi geçti; Phase 1 tasarım sonrası yeniden değerlendirildi — geçer.*

- **İlke I (BC izolasyonu)**: ✅ Kontrat değişikliği additive (`CategoryIds` default null); Discount
  kendi `ProductCatalogRef` izdüşümünü event'ten besler, Catalog DB'sine erişmez; kategori ağacı
  Discount'a kopyalanmaz (fat-event, tüketici lookup yapmaz).
- **İlke II (zengin aggregate)**: ✅ Invariant'lar aggregate'te: fiyat>0 (`Create`/`SetPrice`), tek-leaf
  zorunlu, related∌leaf + dedup, Category ebeveyni doğumda sabit (mutator yok). Davranışsız kalan
  ProductTag silinir (anemik aggregate yasağıyla da uyumlu).
- **İlke III (VSA+CQRS, repository yok)**: ✅ Yüzey yok; değişiklik seed handler + event consumer +
  aggregate. Handler'lar `IDocumentSession` doğrudan.
- **İlke IV (Result)**: ✅ Davranış metotları `ResultDomain` sürer.
- **İlke V (scope yetki)**: N/A — dış yüzey açılmıyor.
- **İlke VI (Domain-TDD)**: ✅ Yeni/değişen aggregate davranışları test-first; tasks.md'de test task'ları
  implementasyondan önce gelecek.
- **İlke VII (FLOW.md)**: ✅ Catalog FLOW.md aynı PR'da yeni sürece hizalanır (FR-013); Discount FLOW.md
  izdüşüm satırı güncellenir.
- **İlke VIII (sır)**: N/A.

İhlal yok → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/095-catalog-book-domain/
├── plan.md              # bu dosya
├── research.md          # Phase 0 kararları (R1-R7)
├── data-model.md        # Phase 1: yeni Product/Category + sökümler + Discount izdüşümü
├── quickstart.md        # Phase 1: canlı doğrulama senaryoları
├── contracts/
│   └── product-changed-event.md   # as-is → to-be kontrat
└── tasks.md             # /speckit-tasks üretir (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Domains/Products/Product.cs            # redesign (alan söküm + SetCategory/SetRelatedCategories)
├── Domains/Products/ValueObjects/…        # ProductCategoryAssignment ölür; Money kalır
├── Domains/Products/Entities/…            # ProductPriceChange değişmez
├── Domains/Categories/Category.cs         # sade düğüm (Create-only)
├── Domains/ProductTags/                   # SİLİNİR
├── Helpers/                               # kategori ad/id zinciri çözümü (seed + FileConsumers ortak)
├── Seeding/{BookSeedHostedService,SeedBookRecord}.cs  # iki-fazlı seed
├── FileConsumers.cs                       # GERİ YAZILIR (CoverIngested → SetImage → fat event)
└── Extensions/MessagingExtensions.cs      # değişmez (kuyruk zaten dinleniyor)

src/others/Shared/IntegrationEvents.cs     # ProductChangedEvent + CategoryIds (additive)

src/services/discount/Discount.Api/
├── CatalogConsumers.cs                    # CategoryIds map
├── Domains/ProductCatalogRefs/ProductCatalogRef.cs    # CategoryId → CategoryIds
└── Domains/Campaigns/CampaignSelectionResolver.cs     # Contains eşleşmesi

src/services/catalog/FLOW.md               # aynı PR hizası
src/services/discount/FLOW.md              # izdüşüm satırı

tests/Catalog.Api.Tests/                   # ProductTag/Sku/Dimensions/çoklu-kategori testleri söküm;
                                           # yeni davranış testleri (test-first)
tests/Discount.Api.Tests/                  # resolver Contains testleri
```

**Structure Decision**: Mevcut BC yerleşimi korunur; yeni proje/klasör açılmaz. Tek yeni dosya sınıfı:
Catalog `Helpers/` altında kategori-zinciri çözüm helper'ı + geri yazılan `FileConsumers.cs`.

## Complexity Tracking

İhlal yok — boş.
