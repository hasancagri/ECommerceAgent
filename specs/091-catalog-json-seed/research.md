# Research — 091 Katalog JSON Seed Dönüşü

Faz 0: tüm kararlar kullanıcıyla plan oturumunda (2026-10-18) netleşti; NEEDS CLARIFICATION kalmadı. Kayıt:

## K1 — Doluş mekanizması: açılış hosted service (051 deseninin dönüşü)

- **Decision**: Catalog'da config-gated hosted service (`Seeding/BookSeedHostedService`); commit'li `books.json` okur, ISBN=Gtin bulun-veya-kur, ürün+`ProductAdded` aynı transaction. Get-or-create (yazar/yayınevi/kategori) mantığı sökülen `ImportProcessor`'dan taşınır.
- **Rationale**: 051'de kanıtlanmış desen (commit `aa5540e`, `BookImportHostedService`); sıfır-tören dev doluşu; idempotent her açılış.
- **Alternatives**: Marten `InitialData` (transaction/event yayını handler disipliniyle uyumsuz); CLI/script (ayrı adım = tören, kullanıcı sıfır-tören istiyor); Excel import korunarak otomatikleştirme (sökülmek istenen şeyin kendisi).

## K2 — Yayın makinesi tamamen sökülür

- **Decision**: `Product.Published` alanı, `Publish()/Unpublish()` metotları, `AdminSetPublished` + `PublishImported` slice'ları ve keşif query'lerindeki `x.Published` filtreleri kalkar. Ürün canlı doğar. Fiyat>0, `Product.Create`/`SetPrice`'ta `ResultDomain` invariant'ı olur.
- **Rationale**: Kullanıcı kararı ("vitrinden gizle/geri aç da kalksın"); taslak/yayın akışı tedarikçi-tarzı tören olarak görülüyor; fiyat>0 kapısı yayına değil var oluşa taşınınca "satılamaz kart" invariant'ı korunur.
- **Alternatives**: Yalnız seed yolu yayında doğar (taslak makinesi kalır — reddedildi); gizleme kalır (reddedildi).
- **DİKKAT**: Base `IsDeleted` denetim alanı (AggregateRoot/BaseModel, TÜM aggregate'lerde `!x.IsDeleted` guard'ları) yayın makinesi DEĞİL — kalır. Sökülen yalnız `Published` ekseni.

## K3 — Event kontratı additive korunur

- **Decision**: `ProductChangedEvent.IsDeleted` alanı kontratsal kalır; Catalog her yayında `IsDeleted: false` gönderir (zaten `AdminSetPublished` dışındaki tüm yayın yerleri false gönderiyordu; tek `true` kaynağı siliniyor). Storefront/Library/Discount tüketicilerine dokunulmaz.
- **Rationale**: Additive kontrat kuralı (conventions); alan silmek üç tüketiciyi kırar. Ölü alan temizliği ileride ayrı feature.
- **Alternatives**: Alanı kontrattan silmek (üç BC'de koordineli değişiklik — kapsam şişer, kazanç kozmetik).

## K4 — Kapak kaynağı: aynı seed JSON

- **Decision**: `XlsxCoverSource` → `JsonCoverSource` (isbn+imageUrl okur); `CoverMigrationOptions.SourceXlsxPath` → `SourceJsonPath`; AppHost iki servise de aynı commit'li dosyanın yolunu env ile verir. ClosedXML Catalog + File csproj'larından ve (başka tüketici kalmadığından) `Directory.Packages.props`'tan düşer.
- **Rationale**: Tek gerçek-kaynak dosya; repo-dışı `~/dev/catalog-data/catalog-import.xlsx` bağımlılığı biter (SC-004). Migration'ın karar/indirme/R2 zinciri (MigrationDecision, R2Sync, RegistryBackfill) xlsx'e bağımlı değil — dokunulmaz.
- **Alternatives**: Migration'ı kapatıp R2'ye güvenmek (R2 backfill duraklamış — dev'de kapaksız kalınır); Catalog'dan HTTP feed (BC-arası yeni senkron kontrat — gereksiz tören).

## K5 — InitialStock: seed satırından

- **Decision**: `shape_books.py` her kayda `stock` alanı üretir (gerçekçi dağılım, script içinde deterministik); seeder `ProductAdded.InitialStock`'a satır değerini koyar. `admin_create_product` yolu InitialStock=0 kalır.
- **Rationale**: Kullanıcı kararı; satır bazlı stok dev senaryolarını (stokta yok / az kaldı) test edilebilir kılar.
- **Alternatives**: Sabit 100 (051 davranışı — reddedildi).

## K6 — Seed dosyası evi + parse

- **Decision**: `books.json` Catalog `Seeding/Data/`'ya commit edilir (051'deki ev); File.Api'ye yol AppHost env ile verilir (bugünkü `coverSourceXlsx` deseninin aynısı — dosya yolu config'i, BC izolasyonunu bozmaz). Parse mevcut JSON stack'iyle (yeni paket yok); kayıt tipi her serviste KENDİ okuyucusu (bilinçli tekrar — alan adları sözleşme, `contracts/seed-record.md`).
- **Rationale**: Dosya tek, okuyucular ayrı → BC modeli sızmaz; AppHost zaten dosya-yolu enjeksiyonu yapıyor.
- **Alternatives**: İki kopya commit (≥12MB x2 repo şişkinliği); Shared'a ortak DTO (BC-arası model sızıntısı).

## K7 — Hata davranışı

- **Decision**: Seed dosyası YOKSA seed sessiz atlanır (log: bilgi) — prod/seed-kapalı senaryo. Dosya VAR ama parse edilemiyorsa fail-fast (bozuk commit'li veri = geliştirme hatası). Satır hataları (fiyat≤0, mükerrer ISBN) satırı atlar, uyarı loglar, seed sürer.
- **Rationale**: OpenAI key fail-fast emsali (açılışta yüksek sesle); ama dosya yokluğu meşru konfigürasyon.
- **Alternatives**: Her durumda fail-fast (prod'u kırar); her durumda sessiz atla (bozuk veri fark edilmez).

## K8 — Governance bakımı

- **Decision**: Anayasa İlke V capability-link istisnası ÖRNEKSİZ kalıyor → istisna metni UYKUDA işaretlenir (telemetri istisnası emsali, v1.12.0 formatı), PATCH amendment implement PR'ında. CLAUDE.md catalog satırı + yüzey modeli + anayasadaki 083 referansı ve iki FLOW.md aynı PR'da güncellenir.
- **Rationale**: İlke VII (FLOW aynı PR) + anayasa governance kuralı (örnek bayatlatılmaz).
