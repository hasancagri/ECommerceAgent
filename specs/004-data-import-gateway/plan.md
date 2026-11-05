# Implementation Plan: Temel Veri + Gateway (004)

**Branch**: `004-data-import-gateway` | **Date**: 2026-11-04 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-data-import-gateway/spec.md`

## Summary

Katalog içeriğini doğuran admin Excel import'u (insert-only, ISBN skip, arka plan işi + rapor), kapak zincirinin uçtan uca canlanması (ProductAdded additive `CoverSourceUrl` → File indirme → CoverIngested → Catalog `SetImage`) ve ilk dış kapı (YARP gateway: tek origin + kenar yetki — Dilim 1'de `X-Admin-Api-Key`, Dilim 2'de JWT'ye evrilir). İlk REST admin deseni bu dilimde kurulur.

## Technical Context

**Language/Version**: .NET 10 / C# (`Nullable`+`ImplicitUsings`)
**Primary Dependencies**: Marten (Postgres doc store) · Wolverine (in-proc + RabbitMQ; import işi DURABLE LOCAL queue) · ClosedXML (xlsx okuma — YENİ paket) · Yarp.ReverseProxy (YENİ paket) · Aspire
**Storage**: catalogDb (ImportJob belgesi + mevcut aggregate'ler) · fileDb + R2 (kapak) · Gateway DB'siz
**Testing**: xUnit + Shouldly, saf unit (ImportJob davranışları + satır-eşleme saf birimleri test-first)
**Target Platform**: Aspire AppHost (tüm servisler + YENİ Gateway projesi)
**Project Type**: mikroservis çözümü — mevcut Catalog/File genişler, yeni `src/others/Gateway`
**Performance Goals**: 23.600 satır tek işte biter (SC-001); satır hatası işi durdurmaz
**Constraints**: INSERT-ONLY (ISBN skip) · import anında yayında · stok/indirim sütunları OKUNMAZ · sır ekran/log'a çıkmaz (İlke VIII)
**Scale/Scope**: 5 sayfalı Excel, 27 sütun, 23.600 satır; 3 REST ucu; 1 kontrat genişlemesi; 1 yeni tüketici; 1 yeni proje

## Constitution Check

- **İlke I (BC izolasyonu)**: ✅ Import Catalog-içi; kapak zinciri integration event'le (additive alan + mevcut kontrat); Gateway BC değil (DB'siz proxy, domain modeli yok); File→Catalog veri akışı yalnız event.
- **İlke II (zengin aggregate)**: ✅ ImportJob gerçek yaşam döngüsü/invariant'lı aggregate; durum geçişleri davranış metotlarında.
- **İlke III (VSA+CQRS)**: ✅ StartImport (Command) / GetImportStatus + GetImportErrors (Query) ayrı slice; repository yok; satır işleme süreç-güdümlü → `Process/` (Domains dışı — konvansiyon).
- **İlke IV (Result)**: ✅ Tüm handler/aggregate metotları Result döner; hata kodları `CatalogResourceConstants`'a eklenir.
- **İlke V (scope yetki)**: ⚠️ Gerekçeli sapma — Dilim 1'de merkezi IdP token'ı yerine custom auth şeması (`X-Admin-Api-Key`). Anayasa izni: "JWT-olmayan custom şema meşru — koşul zorlamanın scope olması"; anahtar principal'ine admin scope claim'i yazılır, `[RequiredScope]`/`.RequireAuthorization` aynen çalışır. Dilim 2'de JWT'ye geçilir (spec FR-016). Complexity Tracking'de.
- **İlke VI (Domain-TDD)**: ✅ ImportJob davranışları + satır→künye eşleme saf birimi (ör. `ExcelRowMapper`, kategori zinciri ayrıştırma) test-first; tasks'ta test task'ları önce.
- **İlke VII (FLOW)**: ✅ Catalog FLOW (adım 0 + 6b) ve File FLOW (5b + 6) planned sticky'leri bu branch'te güncellendi; implement bitince `_(planned)_` ekleri düşer, anchor'lar as-built olur.
- **İlke VIII (sır)**: ✅ API anahtarı yalnız config/secret; yanıt, log, hata mesajı, ekran — hiçbirine yazılmaz.

**Post-design re-check**: sapma yalnız İlke V geçişi (aşağıda gerekçeli) — GEÇER.

## Project Structure

### Documentation (this feature)

```text
specs/004-data-import-gateway/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/ (rest-import.md · events.md · gateway-routes.md)
└── tasks.md (speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/others/Gateway/                        # YENİ — YARP proxy (Aspire'a kayıt, .slnx'e ekle)
  Program.cs · appsettings.json (rotalar) · Auth/AdminApiKeyHandler.cs · Options/GatewayAuthOptions.cs
src/others/Shared/IntegrationEvents.cs     # ProductAdded += CoverSourceUrl (additive)
src/services/catalog/Catalog.Api/
  Domains/ImportJobs/
    ImportJob.cs                           # yeni aggregate
    ImportJobEndpointExtension.cs
    Features/Commands/StartImport.cs       # multipart upload → iş kuyruğa
    Features/Queries/GetImportStatus.cs · GetImportErrors.cs
  Process/ImportJobProcess.cs              # durable local queue tüketicisi (IncludeType kaydı!)
  Import/ExcelRowMapper.cs                 # saf eşleme birimi (test-first) + ImportRow modeli
  FileConsumers.cs                         # YENİ — CoverIngested → Product.SetImage (IncludeType!)
  Constants/CatalogResourceConstants.cs    # yeni hata kodları
src/services/file/File.Api/
  CatalogConsumers.cs                      # CoverSourceUrl indirme hattı genişlemesi
src/aspire/AppHost/AppHost.csproj          # Gateway kaydı
Directory.Packages.props                   # ClosedXML + Yarp.ReverseProxy sürümleri
tests/Catalog.Api.Tests/                   # ImportJobTests.cs · ExcelRowMapperTests.cs (test-first)
tests/Gateway.Tests/                       # YOK — gateway saf domain içermiyor (İlke VI kapsam dışı; canlı doğrulama quickstart'ta)
```

**Structure Decision**: Mevcut BC'ler genişler; tek yeni çalıştırılabilir `src/others/Gateway`. Import süreci `Process/` konvansiyonunda (kullanıcı tetikli upload slice'ı Domains'te, satır işleyen süreç dışarıda). FLOW.md linked-file kuralı yeni projeye uygulanmaz (BC değil).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| İlke V geçici custom şema (API anahtarı; merkezi IdP token'ı değil) | Yerel kimlik servisi Dilim 2'de; dış IdP bağımlılığı bilinçli sökülüyor (SSO-yok kararı) | Dış IdP'yi geçici kullanmak ters yön bağımlılığı uzatır; korumasız uç (dev-only) admin yazma yüzeyini açık bırakır — kullanıcı B'yi seçti (2026-11-04) |