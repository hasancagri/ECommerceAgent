# Tasks: Katalog JSON Seed Dönüşü + Excel Import ve Yayın Makinesi Sökümü

**Input**: Design documents from `/specs/091-catalog-json-seed/`

**Prerequisites**: plan.md, spec.md, research.md (K1-K8), data-model.md, contracts/

**Tests**: İlke VI gereği saf domain (fiyat>0 invariant, seed kayıt doğrulaması) test-first; handler/hosted-service/wiring test-sonra/canlı.

**Organization**: US1 seed doluşu (MVP) → US2 söküm → US3 kapak. US2, US1'den SONRA (seeder, silinecek `ImportProcessor`'dan get-or-create mantığını önce devralır).

## Phase 1: Setup (seed verisi)

**Purpose**: İki story'nin de (US1 doluş, US3 kapak) okuyacağı commit'li veri dosyası.

- [X] T001 ~~shape_books.py'ye stock ekle~~ GEREKSİZ ÇIKTI: `~/dev/catalog-data/books.enriched.json` zaten `stock` dahil 14 alan taşıyor (xlsx'in kaynağıymış); script zinciri DEĞİŞMEDİ, `contracts/seed-record.md` kaynağa göre güncellendi
- [X] T002 `books.enriched.json` doğrulandı (19.711 kayıt; fiyat≤0/negatif stok/mükerrer-boş ISBN = 0) ve `src/services/catalog/Catalog.Api/Seeding/Data/books.json` (14MB) olarak kopyalandı

---

## Phase 2: Foundational (yayın makinesinin aggregate'ten sökümü)

**Purpose**: US1 ürünü CANLI doğurur, US2 yüzeyi siler — ikisi de `Product`'ın yeni durum modeline muhtaç. Build yeşil kalsın diye aggregate + kırılan testler birlikte.

**⚠️ CRITICAL**: User story'ler bundan önce başlayamaz.

- [X] T003 Test-first (İlke VI): fiyat>0 invariant testleri yazıldı, FAIL görüldü — `ProductTests.cs` (`Create_ZeroPrice_ReturnsError`, `SetPrice_ZeroAmount_ReturnsError`; `Create` artık `ResultDomain<Product>`)
- [X] T004 `Product.cs`: `Published`+`Publish()`/`Unpublish()` söküldü; `Create` `ResultDomain<Product>` döner, `Create`/`SetPrice` fiyat≤0 reddi (`PRODUCT_PRICE_REQUIRED`); base `IsDeleted` dokunulmadı. NOT: derleme zinciri T013/T016'nın bir kısmını öne çekti — `AdminSetPublished.cs`+`PublishImported.cs` silindi; `AdminCreate/Update/Get/List` `IsPublished` düştü; `AdminCreateProduct` doğumda `ProductChangedEvent` YAYAR (canlı doğum, kullanıcı onaylı iki-event modeli); keşif+List* yayın filtreleri kalktı; `AdminRepublishProducts` tüm ürünleri backfill eder; `ImportProcessor` geçici fiyat-guard yaması (dosya Phase 4'te silinecek)
- [X] T005 `PublishImportedTests.cs` silindi + `ProductTests` publish testleri söküldü; test helper'ları fiyat>0'a geçti — 92/92 YEŞİL

**Checkpoint**: `dotnet build && dotnet test` yeşil (import slice'ları henüz duruyor, derleniyor — `Publish()` çağıran `PublishImported.cs`/`AdminSetPublished.cs` bu fazda geçici olarak derlenemiyorsa T005 kapsamında stub DEĞİL, Phase 4 öne alınmış sayılır: bu iki slice'ın silinmesi T004 ile aynı commit'te yapılabilir)

---

## Phase 3: User Story 1 - Katalog açılışta seed'den dolar (Priority: P1) 🎯 MVP

**Goal**: Boş DB + Aspire açılışı = dolu, aranabilir, stoklu katalog; sıfır elle adım.

**Independent Test**: Boş catalogDb ile aç → seed logları; `search_products` döner; Stock OnHand = satır `stock`; ikinci açılış sıfır yeni kayıt.

### Tests for User Story 1 (İlke VI — saf kısım)

- [X] T006 [P] [US1] Test-first: seed kayıt doğrulama testleri yaz, FAIL gör — `tests/Catalog.Api.Tests/SeedBookRecordTests.cs` (`Validate_NonPositivePrice_ReturnsError`, `Validate_EmptyIsbn_ReturnsError`, `Validate_NegativeStock_ReturnsError`, `Validate_ValidRecord_Succeeds`)

### Implementation for User Story 1

- [X] T007 [US1] `src/services/catalog/Catalog.Api/Seeding/SeedBookRecord.cs`: okuma DTO'su + saf `Validate` (alanlar `contracts/seed-record.md`); T006 yeşil
- [X] T008 [US1] `src/services/catalog/Catalog.Api/Options/SeedOptions.cs` (`Enabled`, `SourceJsonPath`) + `appsettings.Development.json` section; Options pattern (BindConfiguration + ValidateOnStart)
- [X] T009 [US1] `src/services/catalog/Catalog.Api/Seeding/BookSeedHostedService.cs`: `ImportProcessor`/`ProcessImportRow`'dan get-or-create (yazar/yayınevi/kategori; leaf==mid tuzağına dikkat) + ürün kurma mantığını DEVRAL; **satır satır upsert (kullanıcı kararı): kayıt başına Gtin lookup → varsa atla, yoksa ürün+`ProductAdded(InitialStock=stock)` satırın KENDİ commit'inde** (yarıda kesilme = kaldığı yerden; hız ikincil); mükerrer ISBN/geçersiz satır uyarıyla atla; dosya yoksa bilgi-log atla, parse hatasında fail-fast (K7); `CacheInvalidationRequested(AgentLists)` yayını korunur
- [X] T010 [US1] `src/services/catalog/Catalog.Api/Program.cs`: seeder hosted service + SeedOptions kaydı (import kabloları henüz Phase 4'te)
- [X] T011 [US1] `src/aspire/AppHost/AppHost.cs`: Catalog'a seed JSON yolu env (`Seed__SourceJsonPath` → commit'li dosyanın mutlak yolu; proje-içi dosya olduğundan content-copy ya da path çözümü not düş)

**Checkpoint**: US1 bağımsız doğrulanabilir (quickstart adım 1).

---

## Phase 4: User Story 2 - Excel import ve yayın yüzeyi söküldü (Priority: P2)

**Goal**: Import/yayın tool'ları, token-link ekranı, staging saklama ve yayın filtreleri tamamen gider.

**Independent Test**: MCP tool listesinde 4 tool yok; `admin_create_product` fiyat>0 ile canlı ürün; `/catalog-import/x` 404; fiyat=0 Result hatası.

### Implementation for User Story 2

- [X] T012 [US2] `src/services/catalog/Catalog.Api/Import/` klasörünü sil (ImportSession, ImportRow, ImportProcessor, ImportUploadEndpointExtension) — T009 mantığı devraldıktan SONRA
- [X] T013 [P] [US2] Agent slice'larını sil: `Domains/Products/Features/Agents/Commands/ImportCatalog.cs`, `PublishImported.cs`, `AdminSetPublished.cs` + `Queries/GetImportStatus.cs`
- [X] T014 [P] [US2] `src/others/Shared/McpToolNames.cs` 4 tool const + `src/others/Shared/McpToolDescriptions.cs` ilgili description'ları sil (`contracts/mcp-surface-diff.md`)
- [X] T015 [US2] Catalog temizliği: `Program.cs` (ImportOptions bind, ImportProcessor kaydı, MapImportUploadEndpoints), `Extensions/MartenExtensions.cs:48-53` (ImportSession/ImportRow şema+index), `Options/ImportOptions.cs` sil, appsettings `ImportOptions` section sil, `Catalog.Api.csproj`'dan ClosedXML düş, `GlobalUsings.cs` artık-kullanılmayan using temizliği
- [X] T016 [US2] Query/DTO temizliği: `SearchProducts.cs`/`GetProduct.cs` `x.Published` filtresi kalkar; `AdminGetProduct`/`AdminListProducts`/`AdminCreateProduct`/`AdminUpdateProduct` response'larından `IsPublished` düşer; `AdminUpdateProduct` yayın-koruma nüansı kalkar; `AdminRepublishProducts` tüm ürünleri backfill eder
- [X] T017 [US2] Import testlerini sil: `tests/Catalog.Api.Tests/Import/ImportRowTests.cs`, `ImportSessionTests.cs` (klasörüyle); `dotnet build && dotnet test` yeşil

**Checkpoint**: US1 + US2 birlikte çalışır (quickstart adım 1-2).

---

## Phase 5: User Story 3 - Kapaklar seed JSON'dan beslenir (Priority: P3)

**Goal**: File.Api kapak migration'ı repo-dışı xlsx yerine commit'li seed JSON okur.

**Independent Test**: Temiz staging ile açılış → kapaklar `{RootPath}/covers/` altına iner, ürün ImageUrl dolar; imageUrl boş kayıt atlanır.

### Implementation for User Story 3

- [X] T018 [P] [US3] `src/services/file/File.Api/Migration/JsonCoverSource.cs` yaz (isbn+imageUrl okur, bilinmeyen alanları yok sayar); `XlsxCoverSource.cs` sil
- [X] T019 [US3] `Migration/CoverMigrationHostedService.cs` kaynağı `JsonCoverSource`'a bağla; `Options/CoverMigrationOptions.cs` `SourceXlsxPath` → `SourceJsonPath`; `File.Api.csproj`'dan ClosedXML düş
- [X] T020 [US3] `src/aspire/AppHost/AppHost.cs`: `coverSourceXlsx` kalk, `CoverMigration__SourceJsonPath` = T011'deki AYNI commit'li dosya yolu

**Checkpoint**: Üç story bağımsız doğrulanabilir.

---

## Phase 6: Polish & Cross-Cutting

- [X] T021 `Directory.Packages.props`'tan ClosedXML sürümünü düş (iki csproj da temizlendikten sonra; başka tüketici olmadığını grep'le doğrula)
- [X] T022 [P] `src/services/catalog/FLOW.md`: adım 0 (xlsx import) ve adım 7 (yayın anahtarı) söküm; seed policy adımı (🟪 açılışta seed JSON'dan doluş → 🟧 Ürün doğdu); "yalnız yayındaki yayar" kuralı düşer; 083/074 notları as-built güncellenir; domain kuralları (yayın kapısı → fiyat>0 oluşturma invariant'ı)
- [X] T023 [P] `src/services/file/FLOW.md`: migration kaynağı xlsx→seed JSON; `XlsxCoverSource` anchor'ı `JsonCoverSource` olur
- [X] T024 [P] `CLAUDE.md`: catalog BC satırı (Excel import/yayın anahtarı → seed; silinen tool'lar), yüzey modeli capability-link örneği (083 ekranı gitti), Komutlar bölümü (book-import açıklaması + seed notu), Origin spec sütununa `specs/091-catalog-json-seed` eklenmesi gerekiyorsa ekle
- [X] T025 `.specify/memory/constitution.md` PATCH amendment: İlke V capability-link istisnası UYKUDA (yaşayan örnek kalmadı; telemetri istisnası v1.12.0 emsali); version bump + Sync Impact Report
- [X] T026 Doğrulama: `dotnet build && dotnet test` + `scripts/check-claude-spec-links.sh` + `scripts/check-flow-links.sh` + `scripts/check-agent-query-schema.sh`
- [ ] T027 Canlı doğrulama: `quickstart.md` adımları (Aspire full-reset, seed doluşu, MCP yüzeyi, kapak, `query_storefront`)

---

## Dependencies & Execution Order

- **Phase 1 → 2 → 3 (US1) → 4 (US2)** sıralı: US2'nin sileceği `ImportProcessor` mantığını US1 seeder'ı önce devralır (T009 → T012). T004-T005, `PublishImported`/`AdminSetPublished` derlemesini kırarsa o iki slice'ın silinmesi (T013'ün parçası) T004'le aynı commit'e öne alınabilir — checkpoint notu.
- **US3 (Phase 5)**: Phase 1 (T002) sonrası bağımsız; yalnız T020, T011 ile aynı dosyaya (AppHost.cs) dokunur — sıralı yap.
- **Polish**: hepsinden sonra; T021 ClosedXML, T015+T019'a bağlı.

### Parallel Opportunities

- T006 diğer US1 task'larıyla; T013/T014 kendi aralarında; T018 US2 ile paralel; T022/T023/T024 birbirleriyle paralel.

---

## Implementation Strategy

MVP = Phase 1-3 (US1): seed dolan katalog — import yüzeyi henüz dururken bile değer verir (seed + import yan yana kısa süre yaşayabilir, ISBN idempotency çakışmayı önler). Sonra US2 söküm, US3 kapak, Polish. Her checkpoint'te `dotnet build && dotnet test`.
