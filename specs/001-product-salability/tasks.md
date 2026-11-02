# Tasks: Satışa-Uygunluk Modeli + Kitap Künyesi Genişlemesi

**Input**: `specs/001-product-salability/spec.md` (kademe: Küçük — plan yok)
**Kapsam**: Tek aggregate (`src/services/catalog/Catalog.Api/Domains/Products/Product.cs`) + testleri. Dış yüzey/event/kontrat değişikliği YOK. Domain-TDD (İlke VI): her davranışın test task'ı implementasyondan ÖNCE.

## Phase 1: US1 — Eksik künyeli kitap yaşayabilir (P1) 🎯 MVP

**Goal**: Fiyatsız/yazarsız doğum + fiyat sonradan atanır/değişir (>0 guard).
**Independent Test**: `dotnet test --filter "FullyQualifiedName~ProductTests"` — yeni davranış testleri yeşil, mevcutlar güncellenmiş beklentiyle yeşil.

- [X] T001 [US1] Test-first: fiyat davranış testleri `tests/Catalog.Api.Tests/ProductTests.cs` — `Create_WithoutPrice_SucceedsNotSalable`, `Create_ZeroOrNegativePrice_ReturnsError` (fiyat verilmişse >0), `SetPrice_ValidAmount_SetsPrice`, `SetPrice_ZeroOrNegative_ReturnsErrorAndKeepsCurrent`, `SetPrice_OnPricedProduct_ChangesPrice`; 097 beklentili mevcut Create testlerini yeni semantiğe uyarla (KIRMIZI bırak).
- [X] T002 [US1] Test-first: yazar davranış testleri `tests/Catalog.Api.Tests/ProductTests.cs` — `SetAuthors_EmptyList_ClearsAuthors` (artık hata DEĞİL), `SetAuthors_Duplicates_Dedupes` (mevcut korunur).
- [X] T003 [US1] Implement `src/services/catalog/Catalog.Api/Domains/Products/Product.cs`: `Create` fiyatı opsiyonel alır (fiyat verilmişse >0 guard; verilmezse fiyatsız doğum — `Price` "yok" durumunu temsil eder); `SetPrice(Money)` geri gelir (>0 guard, serbest değişim); `SetAuthors` boş listeyi kabul eder (dedupe sürer). 097 yorum satırları güncellenir. T001-T002 YEŞİL.

**Checkpoint**: Fiyatsız/yazarsız kitap domain'de yaşıyor; fiyat atanıp değişebiliyor.

## Phase 2: US2 — Satışa-uygunluk tek tanım (P2)

**Goal**: `IsSalable` türetilmiş durumu tek tanımda: fiyat>0 ∧ stok>0 ∧ ≥1 yazar (stok ayağı bugün boş geçer).
**Independent Test**: Doğruluk tablosu testleri yeşil.

- [X] T004 [US2] Test-first: satışa-uygunluk doğruluk tablosu `tests/Catalog.Api.Tests/ProductTests.cs` — `IsSalable_PricedAndAuthored_ReturnsTrue`, `IsSalable_WithoutPrice_ReturnsFalse`, `IsSalable_WithoutAuthors_ReturnsFalse`, `IsSalable_AfterSetPriceCompletesCriteria_ReturnsTrue`, `IsSalable_AfterAuthorsCleared_ReturnsFalse`.
- [X] T005 [US2] Implement `Product.cs`: satışa-uygunluk TEK tanım (saf getter/metot; `/// <summary>` kriteri yazar: fiyat>0 VE stok>0 VE ≥1 yazar — stok kavramı dönene dek değerlendirme dışı, kodda açık yorumla işaretli). Kural başka hiçbir yere kopyalanmaz. T004 YEŞİL.

**Checkpoint**: Gelecek yüzeylerin okuyacağı tek kaynak hazır.

## Phase 3: US3 — Künye genişlemesi (P3)

**Goal**: Cilt tipi + popülerlik sayaçları künyede.
**Independent Test**: Atama/okuma + geçersiz değer red testleri yeşil.

- [X] T006 [US3] Test-first: künye testleri `tests/Catalog.Api.Tests/ProductTests.cs` — `SetBinding_KnownValue_SetsBinding`, `SetPopularity_ValidValues_SetsCounters`, `SetPopularity_NegativeCounter_ReturnsError`, `SetPopularity_RatingOutOfRange_ReturnsError` (puan 0-5), `SetPopularity_ZeroValues_Succeeds`.
- [X] T007 [US3] Implement `Product.cs`: `BindingType` enum (aggregate dosyasında — konvansiyon; değerler: Unknown/KartonKapak/Ciltli/KendindenKapak/Plastik) + `SetBinding`; popülerlik sayaç alanları (puan, puan adedi, satın alma adedi, okuyacağım/okuyorum/okudum, favori — düz alanlar) + `SetPopularity` (negatif red, puan 0-5, satışa-uygunluğa etkisiz). T006 YEŞİL.

**Checkpoint**: Künye alanlarının evi hazır (veri girişi sonraki dilimlerin işi).

## Phase 4: Polish

- [X] T008 Doküman hizası: `src/services/catalog/FLOW.md` 2b planned sticky'si as-built'e döner (ek düşer, backtick anchor eklenir: satışa-uygunluk + SetPrice); "Fiyat doğumda sabit (097)" kural satırı yeni kuralla değiştirilir ("fiyat sonradan atanır/değişir, >0"); `CLAUDE.md` catalog satırındaki "fiyat doğumda sabit (097)" ifadesi güncellenir.
- [X] T009 Tam doğrulama: `dotnet build && dotnet test` — tüm çözüm yeşil; değişen tek üretim dosyası `Product.cs` olmalı (kontrat dosyalarına dokunulmadığı `git diff`le teyit edilir).

## Dependencies

- US1 (T001→T003) önce — US2'nin kriterleri fiyat/yazar semantiğine dayanır.
- US2 (T004→T005) US1 sonrası; US3 (T006→T007) bağımsız, US1'den sonra herhangi bir anda [P] koşulabilir.
- T008-T009 en son.

## Implementation Strategy

MVP = Phase 1 (US1). Her phase kendi içinde test-first: önce KIRMIZI testler, sonra geçirecek en küçük değişiklik. Tek üretim dosyası değiştiği için paralellik testlerle sınırlı (T006, T003 sonrası T004-T005'e paralel yazılabilir).