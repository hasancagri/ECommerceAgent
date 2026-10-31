# Tasks: Fiyat değişmezliği (sadeleştirme)

**Input**: specs/097-price-immutability/spec.md

**Tests**: Söküm işi — yeni saf domain yok, test-first N/A (İlke VI kapsamı dışı); mevcut testler
sökümle hizalanır (SetPrice testleri düşer).

**Organization**: US1 = Catalog + kontrat sökümü (MVP); US2 = Library tetik pasifleştirme; Polish = FLOW/doğrulama.

## Phase 1: US1 — Fiyat doğumda sabitlenir (P1) 🎯

- [X] T001 [US1] `Shared/IntegrationEvents.cs` — `ProductChangedEvent.OldPrice` alanını düş
      (`PriceAlarmTriggered` DOKUNMA — uyur)
- [X] T002 [US1] `Catalog.Api/Domains/Products/Product.cs` — `SetPrice` davranışını sök (fiyat>0
      Create guard'ı KALIR); özet yorumundaki fiyat-değişimi referanslarını düzelt
- [X] T003 [US1] `Catalog.Api/Domains/Products/Entities/ProductEntities.cs` — `ProductPriceChange` sil
      (dosya yalnız onu taşıyorsa dosya gider)
- [X] T004 [US1] `Catalog.Api/Extensions/MartenExtensions.cs` — `ProductPriceChange` şema kaydını sök
      (eski tablo dev full-reset notu — 095 emsali yorum)
- [X] T005 [US1] `Catalog.Api/Seeding/BookSeedHostedService.cs` — ilk-fiyat geçmiş satırı yazımını sök;
      `ProductChangedEvent` publish'inden OldPrice argümanını düş
- [X] T006 [US1] `Catalog.Api/FileConsumers.cs` — KAPSAM BÜYÜDÜ (kullanıcı, implement sırasında):
      dosyayı kullanıcı SİLDİ — kapak hattının Catalog ayağı komple öldü (CoverIngested tüketimi +
      kuyruk kablosu söküldü; File yayını tüketicisiz, ürünler kapaksız — kapaksızlık zaten kullanıcı kararıydı)
- [X] T007 [US1] `tests/Catalog.Api.Tests/ProductTests.cs` — SetPrice testlerini düş (Create fiyat>0
      testleri KALIR)

## Phase 2: US2 — Library tetiği pasifleşir (P2)

- [X] T008 [US2] `Library.Api/CatalogConsumers.cs` SİL + `Extensions/MessagingExtensions.cs`'ten
      ProductChanged exchange/queue/listen kablosu + `Program.cs`/MessagingExtensions `IncludeType` kaydı
      (Library artık ProductChanged TÜKETMEZ; PriceAlarm aggregate + PriceAlarmTests DOKUNMA)
- [X] T009 [US2] `tests/Library.Api.Tests/PriceAlarmTests.cs` — yalnız tetik-yoluna bağımlı test varsa
      düş; aggregate testleri kalır

## Phase 3: Polish & doğrulama

- [X] T010 [P] `src/services/catalog/FLOW.md` — adım 2a (fiyat geçmişi) düşer; adım 5'ten fiyat-değişimi
      dili arınır; domain kuralları: "Fiyat geçmişi append-only" kuralı yerine "Fiyat doğumda sabit (097)"
- [X] T011 [P] `src/services/library/FLOW.md` — alarm süreci "uykuda (tetiksiz, 097)" işaretlenir;
      ⇦ Catalog dikişi düşer
- [X] T012 [P] `CLAUDE.md` — catalog satırından "fiyat geçmişi (058)" düşer, "fiyat doğumda sabit (097)"
      girer; library satırına alarm-uykuda notu; origin'e 097 spec yolu
- [X] T013 Final: `dotnet build && dotnet test` + iki guard yeşil; RabbitMQ'da `library.product-changed`
      kuyruğu kalıntısı not edilir (dev reset'le gider)
- [X] T014 Canlı doğrulama (mevcut Aspire koşusu yeniden başlatılarak): açılış yeşil, seed idempotent
      ("0 eklendi"), kapak/embedding tüketicileri hatasız, DLQ boş (SC-003)

## Dependencies

- T001 önce (kontrat) → T005/T006/T008 derlemesi ona bağlı; T002-T007 Catalog içinde sıralı-ish;
  T008-T009 Library; Polish T010-T012 paralel; T013-T014 en son.
