# Implementation Plan: Catalog local embedding altyapısı (kitap vektörleri)

**Branch**: `096-catalog-local-embeddings` | **Date**: 2026-10-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/096-catalog-local-embeddings/spec.md`

## Summary

Her kitabın künye metni (ad + açıklama + yazar adları + kategori zinciri) local Ollama embedding
modeliyle (bge-m3) vektörleşir ve catalogDb'de Marten dokümanı olarak yaşar (`ProductEmbedding`,
1:1 ProductId). Üretimi Catalog'un kendi `Process/` arka plan döngüsü yapar: içerik-hash
karşılaştırmasıyla eksik/bayat kayıtları bulur, toplu embed eder, yazar — broker yok, seed'e
dokunulmaz, Ollama kapalıyken kendini onarır. Arama/tüketici yüzeyi YOK (sonraki spec); ADR yazılır.

## Technical Context

**Language/Version**: .NET 10 (C#, Nullable + ImplicitUsings)

**Primary Dependencies**: Marten + **Marten.PgVector 9.5.0** (`UsePgVector()` — props'ta pinli, eski
Storefront emsali `836de86~1`) · Ollama HTTP API (`POST /api/embed`, SDK'sız düz HttpClient) ·
Wolverine (yalnız mevcut kablo; bu feature mesaj eklemez)

**Storage**: catalogDb — `ProductEmbedding` Marten dokümanı (float[] JSONB; pgvector extension kayıtlı,
HNSW/kNN index BİLİNÇLİ YOK — tüketici yüzey yok, 11.4k satırda index kararı arama spec'inin işi)

**Testing**: xUnit + Shouldly saf unit — metin bileşimi/hash + aday-seçim karar çekirdeği test-first
(İlke VI); HTTP/DB yolları canlı doğrulama

**Target Platform**: Aspire AppHost; **Ollama dış bağımlılık** (kullanıcı makinesinde koşar, AppHost
BAŞLATMAZ — adres/model tip'li Options'tan)

**Project Type**: Tek BC'ye (Catalog) iç altyapı; dış yüzey yok

**Performance Goals**: İlk tam dolum (11.438) dakikalar mertebesi — batch embed (SC-005)

**Constraints**: Bulut embedding API'si YASAK (FR-004); açılış/seed bloklanmaz (FR-005); idempotent +
yeniden başlatılabilir döngü (FR-002)

**Scale/Scope**: 11.438 kitap × 1024 boyutlu vektör (bge-m3) ≈ 47 MB JSONB — tek Postgres rahat

**Artefakt kademesi**: Tam (yeni tablo/doküman + yeni süreç + yeni dış bağımlılık + ADR)

## Constitution Check

*GATE: Phase 0 öncesi geçti; Phase 1 sonrası yeniden değerlendirildi — geçer.*

- **İlke I (BC izolasyonu)**: ✅ Her şey Catalog içinde — kendi DB'si, kendi süreci; BC-arası kanal
  kullanılmıyor (broker'a bilinçli ÇIKILMIYOR — aynı DB içi iş). Ollama BC değil, dış araç.
- **İlke II (zengin aggregate)**: ✅ `ProductEmbedding` aggregate DEĞİL — türetilmiş teknik kayıt
  (read-model emsali `ProductCatalogRef`/eski `StorefrontProduct`); invariant taşımaz, aggregate
  açılmaz (anemik aggregate yasağına uygun). Product aggregate'ine DOKUNULMAZ.
- **İlke III (VSA)**: ✅ Kullanıcı tetiklemiyor → `Domains/` dışı, `Process/` (conventions kuralı);
  endpoint/slice yok. Repository yok — süreç `IDocumentSession` kullanır.
- **İlke IV (Result)**: ✅ Saf çekirdekler değer döner; süreç hatası log + retry (beklenen hata akışı
  Result'lık yüzey değil — handler/endpoint yok).
- **İlke V (scope yetki)**: N/A — yüzey yok. Ollama localhost; kimlik taşımaz (sır yok → İlke VIII N/A).
- **İlke VI (Domain-TDD)**: ✅ Saf birimler test-first: `EmbeddingText.Compose` (bileşim+kırpma+hash)
  ve aday-seçim kararı (`NeedsRefresh`: hash/model karşılaştırma).
- **İlke VII (FLOW.md)**: ✅ Planned sticky zaten dökülü (storm-önce-spec); implement'te as-built olur.

İhlal yok → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/096-catalog-local-embeddings/
├── plan.md              # bu dosya
├── research.md          # Phase 0 kararları (R1-R6)
├── data-model.md        # ProductEmbedding + metin bileşimi
├── quickstart.md        # canlı doğrulama (095 kapanışı DAHİL)
├── contracts/
│   └── ollama-embed-api.md   # tüketilen dış kontrat (POST /api/embed)
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Process/
│   └── EmbeddingReconcileProcess.cs   # YENİ: BackgroundService döngüsü (Process/ ilk sakini)
├── Embeddings/
│   ├── ProductEmbedding.cs            # YENİ: Marten dokümanı (aggregate değil → Domains/ dışı)
│   ├── EmbeddingText.cs               # YENİ: saf bileşim+kırpma+hash çekirdeği (test-first)
│   └── OllamaEmbeddingClient.cs       # YENİ: düz HttpClient sarmalayıcı (/api/embed, batch)
├── Options/EmbeddingOptions.cs        # YENİ: BaseUrl/Model/BatchSize/Interval/Enabled (+validasyon)
├── Extensions/MartenExtensions.cs     # UsePgVector() + ProductEmbedding şema kaydı
└── Program.cs                         # HostedService + HttpClient kaydı (orkestrasyon)

src/services/catalog/FLOW.md           # planned sticky → as-built
docs/adr/adr-search-fold-to-postgres.md  # YENİ: ES→pg katlama + local embedding ADR
tests/Catalog.Api.Tests/
├── EmbeddingTextTests.cs              # YENİ (test-first)
└── EmbeddingRefreshDecisionTests.cs   # YENİ (test-first)
```

**Structure Decision**: `Embeddings/` yeni üst-düzey klasör — aggregate değil, feature-slice değil;
`Seeding/` emsali (BC-içi teknik altyapı kendi adlı klasöründe). AppHost'a Ollama kaydı YOK (dış
bağımlılık; adres Options'tan). Yeni proje açılmaz.

## Complexity Tracking

İhlal yok — boş.
