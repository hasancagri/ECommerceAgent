# Tasks: Temel Veri + Gateway (004)

**Input**: specs/004-data-import-gateway/ (plan.md, research.md, data-model.md, contracts/, quickstart.md)
**Not**: Domain-TDD (İlke VI) — ImportJob + ExcelRowMapper test task'ları implementasyondan ÖNCE.

## Phase 1: Setup

- [x] T001 Directory.Packages.props'a ClosedXML + Yarp.ReverseProxy sürümlerini ekle
- [x] T002 `src/others/Gateway/Gateway.csproj` iskeleti (boş YARP host: Program.cs + appsettings.json) + `ECommerceAgent.slnx`'e ekle
- [x] T003 AppHost'a Gateway kaydı (service discovery ile Catalog/File referansları) — `src/aspire/AppHost/Program.cs`

## Phase 2: Foundational (kontrat — US1 ve US3'ün ortak ön şartı)

- [x] T004 `ProductAdded`'a additive `string? CoverSourceUrl = null` ekle — `src/others/Shared/IntegrationEvents.cs` (yorum: 004, eski tüketici kırılmaz) + tüm çözümü derle

## Phase 3: US1 — Admin Excel ile kataloğu doldurur (P1) 🎯 MVP

**Goal**: Excel upload → arka plan insert-only import → rapor. **Independent Test**: quickstart §2 (20 satırlık deneme dosyası; added+skipped+errored = satır sayısı; ikinci yükleme tamamı skipped).

- [x] T005 [P] [US1] ImportJobTests yaz (test-first: Create_*, Start_*, RecordProgress_*, Complete_*, Fail_* — durum geçişi/sayaç/mutasyon-reddi guard'ları) — `tests/Catalog.Api.Tests/ImportJobTests.cs`
- [x] T006 [US1] ImportJob aggregate'ini testleri geçecek şekilde yaz (data-model.md şeması) — `src/services/catalog/Catalog.Api/Domains/ImportJobs/ImportJob.cs`
- [x] T007 [P] [US1] ExcelRowMapperTests yaz (test-first: 27 sütun → künye eşleme; fiyat fallback Liste→KY→null; kategori zinciri "Kitap >" kökü atma; tarih dd.MM.yyyy; bozuk satır sebepli hata) — `tests/Catalog.Api.Tests/ExcelRowMapperTests.cs`
- [x] T008 [US1] ExcelRowMapper + ImportRow saf birimini yaz (ClosedXML'e dokunmaz — satır değerleri→model; research R2 tablosu) — `src/services/catalog/Catalog.Api/Import/ExcelRowMapper.cs`
- [x] T009 [US1] Import hata kodlarını ekle (IMPORT_FILE_INVALID, IMPORT_ALREADY_RUNNING, IMPORT_JOB_NOT_FOUND…) — `src/services/catalog/Catalog.Api/Constants/CatalogResourceConstants.cs`
- [x] T010 [US1] StartImport command slice'ı: multipart .xlsx doğrula (sütun kontrolü dahil, FR-400), geçici diske yaz, aktif iş varsa 409 (tek-iş), ImportJob doğur + durable local queue'ya `ProcessImportJob(jobId)` publish — `src/services/catalog/Catalog.Api/Domains/ImportJobs/Features/Commands/StartImport.cs`
- [x] T011 [US1] ImportJobProcess süreç handler'ı: sayfa=yayınevi döngüsü, satır→ExcelRowMapper, ISBN mevcutsa skip, get-or-create (yazar/yayınevi/kategori — leaf==mid çift-Store tuzağına batch-içi tekilleştirme), Product.Create+Set* zinciri, ProductAdded(CoverSourceUrl) yayını, 100 satırda bir RecordProgress flush, bitişte Complete/Fail + geçici dosya sil — `src/services/catalog/Catalog.Api/Process/ImportJobProcess.cs`
- [x] T012 [US1] Wolverine kablosu: durable local queue tanımı + `opts.Discovery.IncludeType(typeof(ImportJobProcess))` (tuzak!) — `src/services/catalog/Catalog.Api/Extensions/MessagingExtensions.cs` / Program.cs
- [x] T013 [P] [US1] GetImportStatus + GetImportErrors query slice'ları — `src/services/catalog/Catalog.Api/Domains/ImportJobs/Features/Queries/GetImportStatus.cs`, `GetImportErrors.cs`
- [x] T014 [US1] ImportJobEndpointExtension: POST /api/admin/catalog/imports (202) + GET status + GET errors; Program.cs'e map — `src/services/catalog/Catalog.Api/Domains/ImportJobs/ImportJobEndpointExtension.cs`

**Checkpoint**: Aspire'da Catalog'a DOĞRUDAN istekle quickstart §2 senaryosu geçer (gateway henüz yok).

## Phase 4: US2 — Tek kapıdan güvenli erişim (P2)

**Goal**: YARP tek origin + kenar admin-anahtar yetkisi + servis ikinci katı. **Independent Test**: quickstart §1 (anonim 200 / anahtarsız 401 servise ulaşmaz / anahtarlı 202).

- [x] T015 [P] [US2] Gateway rotaları: contracts/gateway-routes.md tablosunu appsettings'e yaz (admin/anonim rota sınıfları) — `src/others/Gateway/appsettings.json`
- [x] T016 [US2] GatewayAuthOptions (Options pattern, ValidateOnStart) + AdminApiKeyHandler: admin rotasında `X-Admin-Api-Key` doğrula; eşleşmezse 401 (proxy'ye düşmeden); anahtar log'a yazılmaz — `src/others/Gateway/Options/GatewayAuthOptions.cs`, `src/others/Gateway/Auth/AdminApiKeyHandler.cs`, Program.cs kablosu
- [x] T017 [US2] Catalog ikinci kat: aynı anahtarı doğrulayan custom auth şeması + admin import uçlarına `.RequireAuthorization` (admin scope claim'i şemadan) — `src/services/catalog/Catalog.Api/Extensions/` + ImportJobEndpointExtension güncelle
- [x] T018 [US2] ForwardedHeaders'ı ServiceDefaults'a ekle (XForwardedFor|XForwardedProto, KnownNetworks/Proxies temiz — bilinen tuzak) — `src/aspire/ServiceDefaults/Extensions.cs`
- [x] T019 [US2] Quickstart §1 canlı doğrulama (Aspire: anonim rota 200, anahtarsız 401 + Catalog log'unda iz yok, anahtarlı 202)

**Checkpoint**: Tüm import trafiği gateway'den geçer; US1 bağımsız test edilebilir kalır.

## Phase 5: US3 — Kitaplar kapaklarıyla görünür (P3)

**Goal**: CoverSourceUrl → File indirme → CoverIngested → Catalog ImageUrl. **Independent Test**: quickstart §4.

- [x] T020 [US3] File CatalogConsumers genişlet: CoverSourceUrl doluysa ve R2'de ISBN yoksa HttpClient'la indir → IFileStore + kayıt defteri → CoverIngested; R2'de varsa indirme atla; indirilemezse event yok (ürün kapaksız, iş durmaz) — `src/services/file/File.Api/CatalogConsumers.cs`
- [x] T021 [US3] Catalog FileConsumers (YENİ): CoverIngested → Gtin'le ürün bul → `Product.SetImage(url)` (idempotent) + `IncludeType(typeof(FileConsumers))` kaydı (tuzak!) + binding'i tüketici kurar — `src/services/catalog/Catalog.Api/FileConsumers.cs` + MessagingExtensions
- [x] T022 [US3] Quickstart §4 canlı doğrulama (örnek ürünlerde ImageUrl dolu; R2-mevcut ISBN'de indirme atlandı; error-queue boş)

**Checkpoint**: Uçtan uca kapak zinciri canlı.

## Phase 6: Polish & Cross-Cutting

- [x] T023 [P] FLOW.md as-built: Catalog adım 0/6b + File 5b/6 `_(planned)_` eklerini düşür, kenar-anchor'ları gerçek sınıf adlarıyla yaz — `src/services/catalog/FLOW.md`, `src/services/file/File.Api/FLOW.md`
- [x] T024 [P] CLAUDE.md hizala: BC haritası (catalog import yolu + kapak dönüşü, file satırı), Yüzey modeli (gateway + ilk REST admin ucu), "bugün ürün doğuran yol YOK" cümlelerini düşür
- [x] T025 build+test YEŞİL ✓; SC-002 küçük dosyayla ✓; GERÇEK DOSYA KOŞULDU (2026-11-05): 23.600 işlendi = 22.587 eklendi + 66 mükerrer-atlandı + 947 hatalı (tamamı 'ISBN boş' — kaynak verinin gerçeği); DB 22.606 ürün/7.087 yazar/287 kategori/5 yayınevi; 11.449 fiyatlı (%48.5 ✓); kapak zinciri arkada akıyor (file.product-added kuyruğu boşaldıkça ImageUrl doluyor)

## Dependencies & Execution Order

- Phase 1 → Phase 2 → US1 → US2 → US3 → Polish (öncelik sırası = bağımlılık sırası; US2 yalnız T014'ün uçlarına, US3 yalnız T004+T011'in yayınına dayanır)
- [P] fırsatları: T005+T007 (iki test dosyası) · T013 (status/errors) T011'le paralel · T015 diğer US2 işlerinden önce bağımsız · T023+T024
- MVP = Phase 1-3 (US1): gateway'siz bile Catalog'a doğrudan istekle katalog dolar.

## Implementation Strategy

US1 bitince katalog dolu (asıl değer); US2 kapıyı kurar (ilk REST deseni); US3 görseli bağlar. Her checkpoint'te durup canlı doğrulama yapılır; `_(planned)_` FLOW ekleri yalnız implement bitince düşer.