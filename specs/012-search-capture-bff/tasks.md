# Tasks: Kullanıcı Arama Kaydı + App-BFF

**Feature**: `specs/012-search-capture-bff` | **Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

Yeni **Bff** (edge orkestratör, `src/others/Bff` — BC değil, durumsuz) + yeni **Personalization.Api** BC (`src/services/personalization`, kendi DB). Akış: React → Gateway → Bff `/api/discovery/search` → Catalog REST → (login) `UserSearched` fanout → Personalization → `UserSearchRecord` append. Domain-TDD (İlke VI): yalnız saf birim (`UserSearchRecord.Create`) test-first; BFF/event host-gerekli → canlı doğrulama.

**Kısaltma**: `Bff/`=`src/others/Bff/`, `Pers/`=`src/services/personalization/Personalization.Api/`, `Tests/`=`tests/Personalization.Api.Tests/`, `Shared/`=`src/others/Shared/`.

---

## Phase 1: Setup

- [X] T001 Başlangıç yeşili: `dotnet build && dotnet test` temiz mi doğrula
- [X] T002 Projeler + `.slnx`: `Bff/Bff.Api` (Web/Minimal API), `Pers/Personalization.Api` (Web), `Tests/Personalization.Api.Tests` (xUnit+Shouldly); ref'ler Common/Shared/ServiceDefaults; sürümler yalnız `Directory.Packages.props`
- [X] T003 AppHost `src/aspire/AppHost/AppHost.cs`: `personalizationDb` (postgres.AddDatabase) + `Personalization.Api` (personalizationDb+rabbitmq WithReference/WaitFor) + `Bff` (catalog service-discovery + rabbitmq + gateway'den erişilir); Bff'e OIDC/issuer config

---

## Phase 2: Foundational (US'leri bloklar — paylaşılan kontrat)

- [X] T004 Shared kontrat `Shared/IntegrationEvents.cs` + `Shared/.../RabbitMqConstants.cs`: `record UserSearched(Guid UserId, string Query, string Mode, int ResultCount, DateTimeOffset SearchedAt)` + `RabbitMqConstants.UserSearched.Exchange` (fanout) + `Queues.Personalization` (contracts/user-searched-event.md)

---

## Phase 3: User Story 1 — Login arama kaydı (P1) 🎯 MVP

**Hedef**: login arama → sonuç döner + kullanıcıya özel `UserSearchRecord` append (uçtan uca zincir).

**Bağımsız test**: login token'la `/api/discovery/search` → sonuç + `personalizationDb`'de o userId ile 1 satır.

- [X] T005 [P] [US1] Test-first `Tests/UserSearchRecordTests.cs`: `Create_EmptyUserId_ReturnsError` · `Create_BlankQuery_ReturnsError` · `Create_NegativeResultCount_ReturnsError` · `Create_Valid_ReturnsOk` (data-model.md)
- [X] T006 [US1] `Pers/Domains/SearchHistory/UserSearchRecord.cs`: append-only belge + `Create(userId,query,mode,resultCount,searchedAt)` → `ResultDomain<UserSearchRecord>` (guard'lar); T005 geçsin
- [X] T007 [US1] `Pers/Extensions/MartenExtensions.cs`: `personalizationDb` + `UserSearchRecord` `DocumentAlias("user_search_record")` + `Index(x=>x.UserId)` + `ApplyAllDatabaseChangesOnStartup` + IntegrateWithWolverine
- [X] T008 [US1] `Pers/Extensions/MessagingExtensions.cs`: Wolverine+RabbitMQ — `UserSearched.Exchange` declare + `BindQueue(Queues.Personalization)` + `ListenToRabbitQueue` (binding tüketicide); uniform retry→error-queue (088)
- [X] T009 [US1] Tüketici `Pers/BffConsumers.cs`: `UserSearched` → `UserSearchRecord.Create` → `session.Store`+`SaveChanges` (append; Create Error → logla, iş durmaz); `Program.cs` `opts.Discovery.IncludeType(typeof(BffConsumers))` (Wolverine keşif tuzağı)
- [X] T010 [US1] `Pers/Program.cs`: AddPersonalizationMarten + Messaging + ServiceDefaults (dış HTTP yüzeyi YOK — yalnız tüketici)
- [X] T011 [P] [US1] `Bff/Program.cs`: JWT bearer OPSİYONEL (007 deseni: ValidateAudience=false, issuer/signature/lifetime) + Wolverine RabbitMQ **yalnız publish** (`UserSearched.Exchange` declare + `PublishMessage<UserSearched>().ToRabbitExchange`) + `HttpClient`(catalog, service discovery) + CurrentUser
- [X] T012 [US1] Bff arama slice `Bff/Features/Search/SearchDiscovery.cs`: `POST /api/discovery/search {query,mode?,limit?}` → Catalog `/api/catalog/products/search` çağır + sonucu döndür + **login ise** `UserSearched` publish (userId yoksa atla); publish/sonuç ayrık (publish hatası sonucu etkilemez — FR-006)
- [X] T013 [US1] Gateway rota `src/others/Gateway/appsettings.json`: `/api/discovery/**` → `bff` cluster (`http://bff`); edge dumb kalır
- [X] T014 [US1] React `~/dev/ECommerceWeb/src/api/catalog.ts`: arama → `/api/discovery/search` (Catalog yerine); login'de `Authorization: Bearer` gönder (useAuthSession token)
- [ ] T015 [US1] Doğrula (CANLI — Aspire): login token → `/api/discovery/search?q=orwell` → sonuç + `personalizationDb.mt_doc_user_search_record`'a 1 satır (query/mode/resultCount/searchedAt doğru); çok arama → ayrı satırlar (quickstart US1)

**Checkpoint**: US1 = MVP (login arama uçtan uca kaydedilir).

---

## Phase 4: User Story 2 — Anonim kaydedilmez (P2)

**Hedef**: anonim arama çalışır ama kayıt üretmez (login-only guard, T012'de).

- [X] T016 [US2] Doğrula (CANLI): token'sız `/api/discovery/search` → sonuç döner, `user_search_record` ARTMAZ; geçersiz/süresi dolmuş token → anonim muamelesi (kayıt yok). (Guard T012'de; bu faz sınırı doğrular — quickstart US2)

---

## Phase 5: User Story 3 — BFF şeffaf (P3)

**Hedef**: BFF üzerinden arama, doğrudan Catalog aramasıyla aynı sonucu/hızı verir.

- [X] T017 [US3] Doğrula (CANLI): `/api/discovery/search` sonucu vs Catalog `/api/catalog/products/search` — küme + sıra aynı; gecikme etkileşimli (saniye-altı, BFF hop ihmal edilebilir — quickstart US3, SC-003)

---

## Phase 6: Polish & Cross-Cutting

- [X] T018 [P] FLOW.md `src/services/personalization/FLOW.md`: BC tek cümle + süreç (⇦ UserSearched → 🟦 kayıt oluştur → 🟨 UserSearchRecord append); kenar-anchor (BffConsumers, UserSearchRecord.Create); `.csproj`'a linked `<None Include="..\FLOW.md" Link="FLOW.md"/>`; BC haritasına (CLAUDE.md) satır
- [ ] T019 [P] Dayanıklılık doğrula (CANLI, SC-004/005): Personalization KAPALIYKEN login arama → sonuç 200 döner; Personalization açılınca event tüketilir, kayıt tutar (retry). RabbitMQ kapalı → arama yine 200 (publish hatası aramayı bozmaz)

---

## Dependencies & Execution Order

- **Setup (T001-T003)** → önce (projeler + AppHost + personalizationDb)
- **Foundational (T004)** → kontrat; publisher (T012) + consumer (T008/T009) bunu bekler
- **US1 (T005-T015)** → MVP. Test-first T005→T006. Personalization kolu (T006-T010) ∥ Bff kolu (T011-T012) — ayrı projeler, ikisi de T004'e bağlı. Gateway (T013) + React (T014) Bff endpoint'inden sonra. T015 canlı en son
- **US2 (T016)** → US1 üstünde (guard zaten T012'de; yalnız doğrulama)
- **US3 (T017)** → US1 üstünde (şeffaflık doğrulama)
- **Polish (T018-T019)** → sonra

## Parallel Opportunities

- T005 (test) ∥ başka Setup işleri
- **Personalization kolu (T006-T010) ∥ Bff kolu (T011-T012)** — ayrı servisler, 1-servis-1-agent deseni uygun
- T018 (FLOW) ∥ T019 (dayanıklılık)

## MVP Scope

**US1 (T001-T015)** = login arama uçtan uca kaydı — BFF + Personalization + event zinciri. US2 anonim sınırını, US3 şeffaflığı doğrular (kod çoğunlukla US1'de). Her US ayrı demoable.

## Implement notları (önden FLAG)

- **Bff outbox YOK** (durumsuz) → publish RabbitMQ-durable; nadir publish-kaybı kabul (düşük-önem). Dayanıklılık tüketici retry/DLQ'da (T019).
- **Wolverine keşif tuzağı**: `BffConsumers` `*Consumer(s)` ile bitse de güvenli; bitmezse `IncludeType` ZORUNLU (T009).
- **Bff BC değil** — FLOW.md gerekmez; yalnız Personalization FLOW (T018).
