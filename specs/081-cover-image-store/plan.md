# Implementation Plan: Kapak Görseli Deposu (File.Api)

**Branch**: `081-cover-image-store` | **Date**: 2026-09-21 | **Spec**: [spec.md](./spec.md)

## Summary

Yeni **File.Api** destek servisi (DB'siz; gateway/mail-mcp emsali) kitap kapaklarını **ISBN** ile
anahtarlanmış **kalıcı yerel dosya deposunda** tutar ve stabil URL'den (`GET /files/v1/covers/{isbn}`)
anonim servis eder. Depolama bir **arayüz** (`IFileStore`) ardında — bugün `LocalDiskFileStore`, ileride
S3/MinIO aynı arayüzün implementasyonu (FR-008). Bir-kez **migration hosted service** kaynak listeden
(`catalog-import.xlsx` → `{isbn, imageUrl}`) indirip depoya yazar; idempotent (varsa atla). Aspire dev'de
File.Api host process → host diskine yazar → reset'e dayanıklı (container/volume yok).

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings)

**Primary Dependencies**: ASP.NET Minimal API + YARP (gateway route) + ClosedXML (xlsx okuma, migration) +
`HttpClient` (görsel indirme) + Scrutor (DI). Marten/Wolverine YOK (domain DB'si yok). Şablon: `src/agents/Mail.Mcp` (DB'siz web servisi).

**Storage**: Domain DB YOK. Kalıcılık = yerel dosya deposu (config'li kök dizin, host path); içerik
`{root}/covers/{isbn}` + content-type yan-dosyada `{isbn}.ct`. S3/MinIO backend sonraki spec.

**Testing**: xUnit + Shouldly; saf birim (ISBN→key sanitize + traversal guard, migration skip-existing kararı) test-first (İLKE VI). Serve/indirme = test-sonra/quickstart.

**Target Platform**: Aspire AppHost içinde servis (dev'de host process → host diski kalıcı).

**Project Type**: Web-service (destek/infra) — HTTP dosya servis + startup migration. Domain BC değil.

**Performance Goals**: Kapak isteği <1 sn (SC-003, diskten stream). Migration ~19.7k satır, per-satır timeout'lu, hata-toleranslı.

**Constraints**: Backend arayüz ardında (S3 swap kontratı bozmasın); kapak okuma anonim; ISBN path-traversal guard'lı; Options pattern (IConfiguration doğrudan okuma yok).

**Scale/Scope**: ~19.7k kapak; tek düğüm yerel disk (dev). Çoklu-düğüm/dağıtım = S3 sonraki spec.

## Constitution Check

*GATE: Phase 0 öncesi + Phase 1 sonrası.*

- **İLKE I (BC İzolasyonu)** ✓ — File.Api'nin domain DB'si yok; başka BC DB/tablosuna erişmez. Migration
  kaynağı repo-dışı bir dosya (catalog-import.xlsx) — BC DB'si değil. Destek servisi (gateway/mail-mcp gibi).
- **İLKE II/III (Aggregate/VSA-CQRS)** ✓ (N/A gerekçeli) — domain aggregate yok; infra servisi. Minimal API
  endpoint + hosted service; domain slice/Marten yok (Complexity Tracking'de gerekçe).
- **İLKE IV (Result)** ✓ (N/A) — serve HTTP (200 dosya / 404); migration iç süreç (log özeti). Domain Result yok.
- **İLKE V (Scope Yetki)** ✓ — kapak okuma **anonim** (İlke V anonim okuma yüzeyine izin verir; vitrin görseli).
  Yeni scope yok. Upload/dış-tetik yok (kapsam dışı); migration startup iç süreci.
- **İLKE VI (Domain-TDD)** ✓ — saf mantık (key sanitize/traversal guard, skip-existing) test-first; serve/indirme test-sonra.
- **İLKE VII (FLOW.md)** ✓ (N/A gerekçeli) — domain **süreci** yok (infra dosya servisi); gateway/mail-mcp gibi FLOW.md taşımaz.

**MCP notu**: kapak = URL'den çekilen görsel → **HTTP zorunlu** (MCP tool olamaz). İlke I "MCP yalnız agent"
domain operasyonları içindir; statik dosya servis anonim HTTP okuma yüzeyidir (meşru).

**Gate: PASS** — ihlal yok; N/A'lar Complexity Tracking'de gerekçeli.

## Project Structure

### Source (repository root)

```text
src/services/file/File.Api/
├── File.Api.csproj                 # sürümsüz PackageReference (ClosedXML + ASP.NET); Marten/Wolverine YOK
├── Program.cs                      # Minimal API + IFileStore DI + migration hosted service + auth(anon serve)
├── GlobalUsings.cs · Properties/launchSettings.json
├── Dependencies/DependencyExtensions.cs
├── Options/CoverStoreOptions.cs        # RootPath (kalıcı kök dizin)
├── Options/CoverMigrationOptions.cs    # Enabled + SourceXlsxPath + DownloadTimeoutSeconds
├── Storage/IFileStore.cs               # Put/TryGet/Exists (key, stream, content-type) — FR-008 arayüz
├── Storage/LocalDiskFileStore.cs       # {root}/covers/{isbn} + {isbn}.ct sidecar; key sanitize/guard
├── Storage/CoverKey.cs                 # saf: isbn → güvenli key (traversal reddi) — test-first
├── Endpoints/CoverEndpoints.cs         # GET /files/v1/covers/{isbn} (anon) → stream/404
└── Migration/
    ├── CoverMigrationHostedService.cs  # run-once (config-gated), idempotent skip, {yazıldı,atlandı,başarısız} log
    └── XlsxCoverSource.cs              # ClosedXML ile {isbn,imageUrl} satırları oku

# Dokunulan mevcut yapılar
src/aspire/AppHost/AppHost.cs           # + AddProject<File_Api>("file-api") + config env (RootPath/SourceXlsxPath)
src/aspire/AppHost/AppHost.csproj       # + ProjectReference File.Api
src/services/gateway/Gateway/appsettings.Development.json  # + files-route + file.cluster (anon /files/**)
Directory.Packages.props                # + ClosedXML
ECommerceAgent.slnx        # + File.Api (+ File.Api.Tests)

tests/File.Api.Tests/                   # CoverKey sanitize/guard + migration skip-existing (saf, test-first)
```

**Structure Decision**: Mail.Mcp şablonu (DB'siz web servisi). Domain klasörü YOK — infra: `Storage/`
(depolama arayüzü + yerel impl), `Endpoints/` (serve), `Migration/` (bir-kez besleme). Depolama `IFileStore`
ardında → S3/MinIO sonraki spec aynı arayüzü implemente eder, serve/migration değişmez.

## Complexity Tracking

- **VSA/CQRS/Marten/Result/FLOW.md yok**: File.Api domain BC değil, infra dosya servisidir (gateway/mail-mcp
  emsali). Domain slice/aggregate/DB olmadığı için bu ilkeler N/A; zorlamak boş-doğru yapı üretirdi.
- **ClosedXML yeni bağımlılık**: xlsx okuma için (migration kaynağı kullanıcının kanonik dosyası). Alternatif
  ham OpenXML/zip parse kırılgan; CSV'e önden çevirme ekstra elle adım. CPM ile props'a eklenir.