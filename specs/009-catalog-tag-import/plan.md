# Implementation Plan: Katalog Etiket Import (BookTag)

**Branch**: `009-catalog-tag-import` | **Date**: 2026-11-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/009-catalog-tag-import/spec.md`

## Summary

008'in ürettiği `_v3_enriched.xlsx`'in `Etiketler` sütununu mevcut Excel import yoluna (004: dry-run→onay→INSERT-ONLY, `ImportJobWorker`) ekler. Her satırdaki etiket metni virgülle bölünür, normalize edilir (`NameNormalization` reuse), `BookTag` value object'lerine dönüşür; `Product` kendi `BookTag` listesini taşır. Depolama kitaba-ait (ortak Tag tablosu yok); değer-eşitliği kitaplar-arası gruplamayı verir. Arama/pg-hibrit kullanımı AYRI dilim — bu dilim etiketi Product'a oturtur ve normalize-eşleşmeye hazırlar.

## Technical Context

**Language/Version**: .NET 10 (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten (Postgres document store, Newtonsoft — BookTag nested serialize), ClosedXML (mevcut import okuması), Wolverine (mevcut import mesajı yok — `ImportJobWorker` durum-güdümlü polling)

**Storage**: catalogDb — `Product` belgesi içinde nested `BookTag` listesi (ayrı tablo yok)

**Testing**: xUnit + Shouldly saf unit. Test-first (İlke VI): `BookTag.Create` (normalize), `Product.SetTags` (dedup by value), `ExcelRowMapper` etiket parse. Hepsi mock'suz saf birim.

**Target Platform**: Aspire AppHost altında Catalog.Api servisi (macOS dev)

**Project Type**: Mevcut BC genişletme (yeni servis/DB yok)

**Performance Goals**: Import zaten arka plan işi; ~22.5k satır × ~5 etiket = normalize+dedup saf CPU, ihmal edilebilir ek yük

**Constraints**: INSERT-ONLY korunur (var olan ISBN etiketi ezilmez); BookTag Marten'de okunabilmeli (deserialize tuzağı — aşağıda)

**Scale/Scope**: 5 yayınevi sayfası, ~22.5k ürün, ürün başına ~5 etiket

## Constitution Check

*GATE: Phase 0 öncesi geçer. Phase 1 sonrası yeniden.*

| İlke | Durum | Not |
|---|---|---|
| I BC İzolasyonu | GEÇER | Yalnız Catalog içi; yeni BC/DB yok, event/senkron çağrı eklenmez |
| II Zengin Aggregate | GEÇER | `BookTag` = VO (record + private ctor + static `Create`); koleksiyon private, mutasyon yalnız `Product.SetTags`'ten; etiket ayrı aggregate DEĞİL |
| III VSA/CQRS | GEÇER | Yeni slice yok — import worker (`Process/`) içi; etiket yazımı aggregate davranışı |
| IV Result Pattern | GEÇER | `BookTag.Create` → `ResultDomain<BookTag>`; `Product.SetTags` → `ResultDomain` |
| V Scope Yetki | N/A | Yeni dış yüzey yok; mevcut admin-anahtar import ucu değişmez |
| VI Domain-TDD | GEÇER | `BookTag`/`Product.SetTags`/mapper saf — test-first, test task implementasyondan önce |
| VII FLOW.md | GEÇER | Catalog FLOW.md'ye planned sticky (adım 4b) eklendi; implement'te as-built'e döner |
| VIII Sır | N/A | Hassas değer yok |
| Teknoloji (Marten/Aspire) | GEÇER (tuzak) | BookTag nested serialize — deserialize ctor tuzağı araştırmada çözüldü |

**Gate: PASS** — ihlal yok; Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/009-catalog-tag-import/
├── plan.md              # Bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── booktag-behavior.md   # BookTag.Create + Product.SetTags + mapper parse sözleşmesi
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Domains/Products/
│   ├── Product.cs                              # + _tags List<BookTag> + Tags IReadOnlyList + SetTags()
│   └── ValueObjects/ProductValueObjects.cs     # + BookTag record (VO)
├── Helpers/
│   ├── ExcelRowMapper.cs                        # + "Etiketler" okuma → ImportRow.Tags; ImportRow'a Tags alanı
│   └── NameNormalization.cs                     # DEĞİŞMEZ (reuse)
└── Process/
    └── ImportJobWorker.cs                       # ürün kurulumunda product.SetTags(...) çağrısı eklenir

tests/Catalog.Api.Tests/
├── BookTagTests.cs                              # YENİ: Create normalize/guard
├── ProductTagTests.cs                           # YENİ: SetTags dedup/replace
└── ExcelRowMapperTests.cs                       # mevcut: + etiket parse senaryoları
```

**Structure Decision**: Mevcut Catalog BC genişletilir. Yeni dosya yalnız VO (mevcut `ProductValueObjects.cs` içine) + testler; mantık mevcut `Product`/`ExcelRowMapper`/`ImportJobWorker`'a eklenir. Yeni slice/endpoint/DB yok — import zaten var olan yüzey.

## Complexity Tracking

Anayasa ihlali yok — tablo boş.
