# Tasks: Katalog Etiket Import (BookTag)

**Feature**: `specs/009-catalog-tag-import` | **Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

Mevcut Catalog BC genişler — yeni servis/DB/event YOK. Değişen dosyalar: `Product.cs`, `ValueObjects/ProductValueObjects.cs`, `Helpers/ExcelRowMapper.cs`, `Process/ImportJobWorker.cs` + testler. Saf domain test-first (İlke VI): test task'ı implementasyondan ÖNCE.

**Kısaltma**: `Catalog.Api/` = `src/services/catalog/Catalog.Api/`, `Tests/` = `tests/Catalog.Api.Tests/`.

---

## Phase 1: Setup

- [X] T001 Başlangıç yeşili sabitle: `dotnet build && dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj` — mevcut durum temiz mi doğrula

---

## Phase 2: Foundational (US1 + US2 bloklar — BookTag ikisinin de temeli)

- [X] T002 [P] Test-first `Tests/BookTagTests.cs`: `Create_ValidRaw_NormalizesValue` (trim+whitespace-collapse+upper: `"  Gözetim "` → `"GÖZETIM"`), `Create_EmptyRaw_ReturnsError` (VALUE_EMPTY), `Create_SameConceptDifferentCase_AreEqual` (value-equality) — contracts/booktag-behavior.md tablosu
- [X] T003 `BookTag` record `Catalog.Api/Domains/Products/ValueObjects/ProductValueObjects.cs`: `Value { get; private set; }` + parametresiz private ctor + parametreli private ctor + static `Create` (`NameNormalization.Normalize`, boş→`ResultDomain.Error(VALUE_EMPTY)`); T002 geçsin. Marten tuzağı: get-only+parametreli-ctor YASAK (research §3)

---

## Phase 3: User Story 1 — Etiketli import (P1) 🎯 MVP

**Hedef**: v3 enriched Excel → ürünler kendi etiketleriyle doğar; etiketsiz satır hatasız.

**Bağımsız test**: boş katalog + küçük xlsx → dry-run (etiketli sayaç) → confirm → etiketli ürünlerde etiket dolu, `Etiketler` boş satır etiketsiz; 27 orijinal sütun yerinde.

- [X] T004 [P] [US1] Test-first `Tests/ProductTagTests.cs`: `SetTags_DuplicateValues_Dedupes`, `SetTags_InvalidParts_Skipped`, `SetTags_EmptyList_ReturnsOkNoTags`, `SetTags_Replaces_PreviousTags` — contracts tablosu
- [X] T005 [US1] `Product` genişlet `Catalog.Api/Domains/Products/Product.cs`: `_tags List<BookTag>` (private) + `Tags IReadOnlyList<BookTag>` + `SetTags(IEnumerable<string> rawTags)` (her parça `BookTag.Create`, başarısız atla, `Value` dedupe, tam-değiştir, boş liste Ok); T004 geçsin
- [X] T006 [P] [US1] Test-first `Tests/ExcelRowMapperTests.cs` (mevcut dosyaya ekle): `Map_TagsCell_SplitsTrimsDistinct`, `Map_NoTagsColumn_EmptyList` (geriye-uyum), `Map_DoubleComma_SkipsEmpty`
- [X] T007 [US1] `ExcelRowMapper` + `ImportRow` genişlet `Catalog.Api/Helpers/ExcelRowMapper.cs`: `ImportRow.Tags List<string>` alanı + `Map`'te `"Etiketler"` oku → `SplitNames` deseniyle böl (NORMALİZE ETME — ham); T006 geçsin
- [X] T008 [US1] `ImportJobWorker` entegrasyon `Catalog.Api/Process/ImportJobWorker.cs`: ürün kurulum bloğunda (~249, `SetAuthors`/`SetCategory` yanı) `product.SetTags(importRow.Tags)` ekle — yalnız YENİ ürün (INSERT-ONLY zaten üstte atlar)
- [X] T009 [US1] Dry-run "etiketli satır" sayacı `Catalog.Api/Process/ImportJobWorker.cs` (+ ilgili rapor modeli): `Tags` boş olmayan satırları say, dry-run raporuna ekle; dry-run HİÇBİR ŞEY yazmaz (data-model.md rapor)
- [X] T010 [US1] Doğrula: `dotnet build && dotnet test`; küçük xlsx (etiketli + etiketsiz satır) ile Aspire import dry-run→confirm; etiket yazıldı, etiketsiz hatasız (quickstart US1)

**Checkpoint**: US1 tek başına = MVP (etiketler Product'a oturdu).

---

## Phase 4: User Story 2 — Aynı kavram gruplanır (P2)

**Hedef**: Distopya/distopya/DİSTOPYA tek normalize değere iner; İ/ı eşleşmeyi bozmaz; tekrar ayıklanır.

**Bağımsız test**: üç casing varyantı ayrı satırlarda → üç ürünün ilgili `BookTag.Value` birebir aynı.

- [X] T011 [P] [US2] Test `Tests/BookTagTests.cs`: `Create_TurkishCaseVariants_SameValue` (Distopya/distopya aynı; İ/ı NameNormalization davranışı), `Create_CollapsesWhitespace`; `Tests/ProductTagTests.cs`: `SetTags_CaseVariants_SingleTag` (varyantlar tek etikete iner)
- [X] T012 [US2] Doğrula: `dotnet test`; casing/İ-ı varyantları tek `Value`'ya iniyor (quickstart US2). NameNormalization davranışı kod geneliyle tutarlı (research §2)

**Checkpoint**: US1 + US2 = etiket oturdu + normalize-gruplama hazır.

---

## Phase 5: Polish & Cross-Cutting

- [X] T013 Marten deserialize CANLI doğrulama (research §3): Aspire import sonrası etiketli ürünü OKU (sorgu/okuma yolu) — 500 YOK; unit yetmez, bu adım bilinçli canlı (quickstart Marten tuzağı satırı)
- [X] T014 INSERT-ONLY + geriye-uyum doğrula: aynı dosyayı tekrar import → var olan ISBN etiketi EZİLMEZ; `Etiketler` sütunu OLMAYAN eski Excel hatasız geçer (quickstart)
- [X] T015 [P] FLOW.md as-built `src/services/catalog/FLOW.md`: adım 4b `_(planned)_` ekini düşür, kenar-anchor ekle (`Product.SetTags`, `BookTag.Create`)
- [X] T016 Tam koşu: `_v3_enriched.xlsx` Aspire import → SC-001 (etiketli satırların ≥%98'i ürün etiketli) raporla; SC-003 örnek etiketle gruplama elle teyit

---

## Dependencies & Execution Order

- **Setup (T001)** → her şeyden önce
- **Foundational (T002-T003)** → US1+US2 bloklar (BookTag ikisinin temeli)
- **US1 (T004-T010)** → MVP; Foundational'a dayanır. Test-first: T004→T005, T006→T007, sonra T008→T009→T010
- **US2 (T011-T012)** → BookTag normalize davranışına dayanır (Foundational); US1'den bağımsız test edilebilir
- **Polish (T013-T016)** → US1+US2 sonrası

## Parallel Opportunities

- T002 ∥ (yok, foundational tek zincir: T002 testi → T003 impl)
- US1 içi test task'ları ayrı dosyalar: T004 ∥ T006 (ProductTagTests ∥ ExcelRowMapperTests)
- T015 ∥ T013/T014 (FLOW.md bağımsız)

## MVP Scope

**US1 (T001-T010)** = teslim edilebilir MVP: etiketler Product'a oturur, import yazar, etiketsiz hatasız. US2 normalize-gruplamayı doğrular (çoğu Foundational'da gelir). Marten canlı doğrulama (T013) tuzak-kapalı güvencesi — atlanmaz.
