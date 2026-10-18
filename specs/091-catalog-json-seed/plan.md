# Implementation Plan: Katalog JSON Seed Dönüşü + Excel Import ve Yayın Makinesi Sökümü

**Branch**: `091-catalog-json-seed` | **Date**: 2026-10-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/091-catalog-json-seed/spec.md`

## Summary

083 Excel import yolu (token-link ekran + ImportSession/ImportRow staging + ImportProcessor) ve ürün yayın makinesi (taslak/yayına-alma/gizleme) tamamen sökülür. Katalog doluşu 051 deseninin dönüşüyle commit'li `books.json`'dan açılışta idempotent seed olur; ürün canlı doğar, fiyat>0 aggregate invariant'ına döner. File.Api kapak migration'ı xlsx yerine AYNI seed JSON'dan beslenir; ClosedXML iki projeden düşer.

## Technical Context

**Language/Version**: .NET 10 / C# (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten (Postgres doc store), Wolverine (in-proc bus + RabbitMQ fanout), Aspire AppHost; `System.Text.Json`/Newtonsoft ile seed parse (yeni paket GEREKMEZ); ClosedXML KALKAR (Catalog + File csproj)

**Storage**: catalogDb (ImportSession/ImportRow şemaları silinir), fileDb değişmez; commit'li `books.json` (Catalog `Seeding/Data/`)

**Testing**: xUnit + Shouldly, saf unit (İlke VI); fiyat>0 invariant + seed satır dönüşümünün saf kısmı test-first

**Target Platform**: Aspire orkestrasyonlu servisler (dev)

**Project Type**: Çok-servisli BC mimarisi — dokunulan: Catalog.Api, File.Api, AppHost, Shared, scripts/book-import

**Performance Goals**: Seed ~19.7k kayıt; açılış doluşu tek seferlik, idempotent ikinci tur hızlı atlama (ISBN=Gtin lookup). Hedef: dev açılışını dakikalar mertebesinde bloklamaz (hosted service, app'i bekletmez)

**Constraints**: `ProductChangedEvent` kontratı ADDITIVE korunur (`IsDeleted` alanı kalır, hiç true gitmez) — Storefront/Library/Discount dokunulmaz. MCP tool silme bilinçli dış kırılma. Anayasa İlke V istisna örneği kalkar → PATCH amendment (UYKUDA işaretleme)

**Scale/Scope**: ~19.7k seed kaydı; 4 MCP tool silinir; 2 FLOW.md + CLAUDE.md + anayasa güncellenir

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Durum | Not |
|---|---|---|
| I — BC izolasyonu | PASS | Seed JSON iki BC'ye AppHost config yoluyla verilen DOSYA; DB paylaşımı yok. Sözleşme = alan adları (bilinçli tekrar: her BC kendi okuyucusunu taşır). Event kontratları additive korunur |
| II — Zengin aggregate | PASS | Fiyat>0 invariant'ı `Product` aggregate metoduna gider (handler'a değil); yayın durumu aggregate'ten sökülür |
| III — VSA + CQRS | PASS | Seeder süreç-güdümlü → `Domains/` DIŞI (`Seeding/`, `Process/` emsali); silinen slice'lar zaten Agents klasöründe |
| IV — Result pattern | PASS | Fiyat guard'ı `ResultDomain` döner; seed satır reddi Result'la loglanır |
| V — Scope yetki | PASS (PATCH gerek) | Capability-link istisnasının yaşayan tek örneği (083 ImportSession) kalkıyor → istisna UYKUDA işaretlenir (telemetri istisnası emsali, v1.12.0). İlke kalkmaz; amendment implement PR'ında |
| VI — Domain-TDD | PASS | Fiyat>0 + seed satır doğrulamasının saf kısmı test-first; tasks.md'de test task'ları önce |
| VII — FLOW legibility | PASS | catalog + file FLOW.md aynı PR'da güncellenir (adım 0 ve 7 söküm, seed policy, "yalnız yayındaki yayar" kuralının düşmesi) |
| VIII — Sır insan-yüzeyde yok | PASS | Sır içeren yüzey yok; token-link ekranı tamamen siliniyor |

**Gate: PASS** — ihlal yok; V için planlı PATCH amendment var (ihlal değil, örnek bakımı).

## Project Structure

### Documentation (this feature)

```text
specs/091-catalog-json-seed/
├── plan.md              # bu dosya
├── research.md          # Faz 0 kararları
├── data-model.md        # Faz 1: Product durum değişimi + seed kaydı + silinenler
├── quickstart.md        # Faz 1: canlı doğrulama rehberi
├── contracts/
│   ├── seed-record.md   # books.json kayıt sözleşmesi (Catalog + File ortak okur)
│   └── mcp-surface-diff.md # silinen tool'lar (dış kırılma kaydı)
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Seeding/                      # YENİ: BookSeedHostedService + SeedBookRecord (+ Data/books.json)
├── Import/                       # SİLİNİR (4 dosya)
├── Domains/Products/
│   ├── Product.cs                # Publish/Unpublish/IsPublished/IsDeleted söküm; fiyat>0 invariant
│   └── Features/Agents/
│       ├── Commands/ImportCatalog.cs, PublishImported.cs, AdminSetPublished.cs   # SİLİNİR
│       └── Queries/GetImportStatus.cs                                            # SİLİNİR
├── Options/ImportOptions.cs      # SİLİNİR → SeedOptions gelir
├── Extensions/MartenExtensions.cs# ImportSession/ImportRow kayıtları düşer
└── Program.cs                    # import kabloları düşer, seeder kaydı gelir

src/services/file/File.Api/
├── Migration/XlsxCoverSource.cs  # SİLİNİR → JsonCoverSource
├── Migration/CoverMigrationHostedService.cs  # kaynak çağrısı değişir
└── Options/CoverMigrationOptions.cs          # SourceXlsxPath → SourceJsonPath

src/aspire/AppHost/AppHost.cs     # coverSourceXlsx düşer; seed JSON yolu Catalog + File env
src/others/Shared/McpToolNames.cs + McpToolDescriptions.cs  # 4 tool const silinir
scripts/book-import/shape_books.py # stock alanı üretir
tests/Catalog.Api.Tests/           # Import/* + PublishImportedTests silinir; yeni invariant + seed testleri
```

**Structure Decision**: Mevcut VSA düzeni korunur. Seeder kullanıcı-tetiklemeli değil → `Domains/` dışında `Seeding/` (051 emsali; conventions "süreç-güdümlü handler Domains dışı" kuralı). Kapak kaynağı File.Api Migration klasöründe kalır, yalnız format değişir.

## Complexity Tracking

İhlal yok — tablo boş.
