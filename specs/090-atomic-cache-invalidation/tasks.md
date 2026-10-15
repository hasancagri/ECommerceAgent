# Tasks: Atomik Cache Boşaltma (handler-içi publish)

**Input**: `specs/090-atomic-cache-invalidation/spec.md` (ölçek: küçük — plan.md yok, bilinçli)

**Yaklaşım (tek karar)**: Handler `Handle(...)` imzasına `IMessageBus bus` parametresi eklenir
(Wolverine bunu mesaj context'i olarak enjekte eder → publish, `[Transactional]` commit'iyle AYNI
outbox'ta kalıcılaşır). Başarı yolunda, son `Ok(...)` dönüşünden hemen önce:
`await bus.PublishAsync(new CacheInvalidationRequested(<BcCacheTags.X>));`
Hata/NotFound dönüşlerinde publish YOK. Cascading tuple dönüşü KULLANILMAZ (InvokeAsync<T> yanıt-tipi
eşleşmesini karmaşıklaştırır, koşullu yayın tuple'da gürültülü).

## Phase 1: Setup

- [X] T001 `master`'dan `090-atomic-cache-invalidation` dalı aç (mevcut `docs/readme-deployment` dalındaki AppHost.cs değişikliğine DOKUNMA)

## Phase 2: Foundational

Yok — mevcut altyapı (`CacheInvalidationRequested` + handler + durable local queue routing) aynen kullanılıyor.

## Phase 3: US1 — Boşaltma isteği yazmayla atomik (P1) 🎯 MVP

**Goal**: 7 boşaltmalı command handler'ı boşaltma isteğini kendi içinden, commit'le atomik yayınlar.

**Independent Test**: Başarılı yazmada boşaltma zarfı commit'le kalıcılaşır (cache sonraki okumada tazelenir); başarısız yazmada zarf yok. Bu fazda attribute hâlâ yerinde → geçici çifte-publish olur (zararsız: fazladan epoch bump), US2 kapatır.

- [X] T002 [P] [US1] Catalog 3 handler'a success-path publish ekle: `src/services/catalog/Catalog.Api/Domains/Authors/Features/Agents/Commands/AdminCreateAuthor.cs`, `.../Categories/Features/Agents/Commands/AdminCreateCategory.cs`, `.../Categories/Features/Agents/Commands/AdminUpdateCategory.cs` (`Handle`'a `IMessageBus bus` param + `Ok` öncesi `PublishAsync(new CacheInvalidationRequested(CatalogCacheTags.AgentLists))` — her dosyadaki mevcut tag'i koru)
- [X] T003 [P] [US1] Customer 4 handler'a aynı desen: `src/services/customer/Customer.Api/Domains/AddressBooks/Features/Agents/Commands/AddAddress.cs`, `UpdateAddress.cs`, `RemoveAddress.cs`, `SetDefaultAddress.cs` (tag: dosyadaki mevcut `CustomerCacheTags.*`)
- [X] T004 [US1] Build + ilgili test projeleri yeşil: `dotnet build` + `dotnet test tests/Common.Tests` + Catalog/Customer test projeleri (rename-refactor tuzağı: bağımlı TEST projelerini de derle)

**Checkpoint**: Boşaltma artık atomik; eski yol hâlâ paralel çalışıyor.

## Phase 4: US2 — Tek boşaltma idiomı / söküm (P2)

**Goal**: Attribute temelli yol tümüyle sökülür; decorator yalnız `[Cached]` okuma yolunu işler.

**Independent Test**: Repo'da `InvalidatesCache` kalıntısı sıfır; tüm testler yeşil; 7 command'in boşaltma davranışı US1 yolundan aynen sürüyor.

- [X] T005 [US2] `src/others/Common/Utils/Caching/CachingMessageBus.cs`: `InvokeCoreAsync`'ten invalidation dalı + `BaseResultModel IsSuccess` koklaması sil; metot "[Cached] var → GetOrCreate, yok → innerCall" ikilisine insin; sınıf doc-comment'inden `[InvalidatesCache]` paragrafını çıkar
- [X] T006 [US2] `src/others/Common/Utils/Caching/InvalidatesCacheAttribute.cs` dosyasını SİL; 7 command record'undan `[InvalidatesCache(...)]` satırını kaldır (T002/T003 dosyaları)
- [X] T007 [US2] `src/others/Common/Utils/Caching/CacheInvalidationRequested.cs` doc-comment güncelle: yayıncı artık decorator değil "yazma handler'ının kendisi (outbox-atomik)"
- [X] T008 [US2] `tests/Common.Tests/CachingMessageBusInvalidationTests.cs` yeniden yaz: decorator'ın artık publish ETMEDİĞİ + `[Cached]` okuma yolunun değişmediği assert'leri; silinen attribute referansları temizlenir
- [X] T009 [US2] Kalıntı taraması: `grep -rn "InvalidatesCache" src tests docs CLAUDE.md` yalnız tarihsel spec'lerde (specs/) kalmalı; kod+doc'ta sıfır

## Phase 5: Polish

- [X] T010 [P] `docs/conventions.md` güncelle: (1) ~satır 180 "Cache (AOP decorator)" maddesi — `[InvalidatesCache]` yerine handler-içi atomik publish genel kural; (2) "Decorator-dışı yazma yolu" maddesi — istisna değil artık TEK idiom, yeniden yaz; (3) ~satır 80 `Agents/Commands|Queries` klasör-aday cümlesindeki `[InvalidatesCache]` adayı ifadesini düzelt
- [X] T011 [P] `CLAUDE.md` Teknoloji bölümü cache cümlesi güncelle ("boşaltma epoch-key + durable event" → handler-içi atomik publish vurgusu)
- [X] T012 Tüm çözüm: `dotnet build` + `dotnet test` yeşil; sonra PR aç (master'a)

## Dependencies

- US1 (T002-T004) ÖNCE, US2 (T005-T009) SONRA — attribute sökülmeden handler publish'i yerinde olmalı, yoksa boşaltma penceresiz kaybolur.
- T002 ∥ T003 paralel (farklı BC, farklı dosyalar). T010 ∥ T011 paralel.
- Polish, US2 bitmeden başlamaz (doc yeni durumu anlatır).

## Implementation Strategy

MVP = US1 (atomiklik kazanılır, risk sıfır — eski yol paralel durur). US2 söküm ayrı commit'te:
sorun çıkarsa revert tek adım. Tag/davranış değişikliği YOK; yalnız yayın NOKTASI taşınıyor.