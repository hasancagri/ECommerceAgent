# Tasks: Arama Geçmişi Görüntüleme

**Input**: `/specs/013-search-history/` (plan.md, spec.md, research.md, data-model.md, contracts/)

**Tests**: İlke VI gereği YALNIZ saf domain (`SearchHistoryProjection` dedup/normalize) test-first ZORUNLU; query/command/endpoint/auth/UI = canlı doğrulama (CLAUDE.md).

**Kısaltma**: `Pers/`=`src/services/personalization/Personalization.Api/`, `Tests/`=`tests/Personalization.Api.Tests/`, `Web/`=`~/dev/ECommerceWeb/src/`.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup (altyapı — tüm story'leri açar)

- [X] T001 Pers/Program.cs: inline JWT bearer (Catalog 012 deseni — `ValidateAudience=false`, `MapInboundClaims=false`, issuer/imza/süre) + `AddAuthorization()` + `AddHttpContextAccessor()` + `AddTransient<ICurrentUser,CurrentUser>()`; `UseAuthentication()`/`UseAuthorization()` pipeline'a ekle
- [X] T002 [P] Pers/GlobalUsings.cs: `Common.Auths`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.IdentityModel.Tokens` ekle
- [X] T003 [P] src/others/Gateway/appsettings.json: rota `personalization` → `/api/personalization/{**catch-all}` + cluster `personalization` (`http://personalization-api`)
- [X] T004 src/aspire/AppHost/AppHost.cs: `gateway.WithReference(personalizationApi).WaitFor(personalizationApi)`
- [X] T005 [P] Pers/Constants/PersonalizationResourceConstants.cs: gerekiyorsa geçmiş hata/mesaj kodları

## Phase 2: Foundational (saf domain — İlke VI test-first; US1 + US3'ü bloklar)

- [X] T006 [P] Tests/SearchHistoryProjectionTests.cs: BAŞARISIZ testler önce — `Deduplicate_RepeatedQuery_ReturnsSingleEntry`, `Deduplicate_TurkishCaseVariants_GroupAsOne` ("Dostoyevski"=="dostoyevski"), `Deduplicate_PicksLatestModeAndTime`, `Deduplicate_SortsByLastSearchedDescending`, `Normalize_FoldsTurkishIAndCase`
- [X] T007 Pers/Domains/SearchHistory/SearchHistoryProjection.cs: `Normalize(string)` (Türkçe İ/ı + büyük/küçük fold, trim) + `Deduplicate(IEnumerable<UserSearchRecord>) → IReadOnlyList<SearchHistoryEntry>` (normalize-grupla, en son mod/zaman, LastSearchedAt azalan) — T006 yeşil olana dek

**Checkpoint**: `dotnet test tests/Personalization.Api.Tests` yeşil.

---

## Phase 3: US1 — Geçmişi görüntüle (P1, MVP)

**Hedef**: Login kullanıcı kendi tekilleştirilmiş + sayfalı geçmişini görür. **Bağımsız test**: quickstart US1 (görüntüle/boş/anonim-401/izolasyon/sayfalama).

- [X] T008 [US1] Pers/Domains/SearchHistory/Features/Queries/GetSearchHistory.cs: query(page,pageSize) + handler — `IQuerySession` ile `UserId==currentUser.Id` ham kayıtları çek → `SearchHistoryProjection.Deduplicate` → sayfala → `FeaturePagedResultModel<SearchHistoryEntry>` (`normalizedQuery` dışa sızmaz)
- [X] T009 [US1] Pers/Domains/SearchHistory/SearchHistoryEndpointExtension.cs: `GET /api/personalization/v1/search-history?page=&pageSize=` + `.RequireAuthorization()` + `CurrentUser.Load`; Program.cs'te `MapSearchHistoryEndpoints()` çağır
- [X] T010 [P] [US1] Web/api/personalization.ts: `getSearchHistory(page,pageSize,token)` → GET (bearer header)
- [X] T011 [US1] Web/pages/SearchHistory.tsx + rota `/gecmis` + menü bağlantısı: paged liste, en yeni üstte, boş-durum mesajı, login-korumalı (RequireAuth)
- [ ] T012 [US1] CANLI doğrula (Aspire): görüntüle + boş-durum + anonim 401 + kullanıcı izolasyonu + sayfalama (quickstart US1)

**Checkpoint**: US1 tek başına çalışır = MVP.

---

## Phase 4: US2 — Geçmişten tekrar ara (P2)

**Hedef**: Öğeye tıkla → arama ekranında o sorgu+mod çalışır. **Bağımsız test**: quickstart US2.

- [X] T013 [US2] Web: SearchHistory.tsx öğe tıklaması → arama/keşif ekranına yönlendir + sorgu+mod ile çalıştır (mevcut `searchProducts`/ChatWorkspace; query param ya da state ile)
- [ ] T014 [US2] CANLI doğrula: tıkla→sonuç + tekrar-arama listeyi şişirmez (recency güncellenir — dedup)

---

## Phase 5: US3 — Geçmişi sil (P3)

**Hedef**: Tek öğe KALICI sil + tümünü temizle (kendi verisi). **Bağımsız test**: quickstart US3.

- [X] T015 [P] [US3] Pers/Domains/SearchHistory/Features/Commands/DeleteSearchHistoryItem.cs: `[Transactional]` handler — `UserId==currentUser.Id` kayıtları çek, `Normalize(Query)==Normalize(hedef)` olanların Id'lerini `session.Delete` + SaveChanges (idempotent) → `FeatureResultModel`
- [X] T016 [P] [US3] Pers/Domains/SearchHistory/Features/Commands/ClearSearchHistory.cs: `[Transactional]` handler — `session.DeleteWhere<UserSearchRecord>(x => x.UserId==currentUser.Id)` + SaveChanges
- [X] T017 [US3] SearchHistoryEndpointExtension.cs: `DELETE .../search-history/item?query=` + `DELETE .../search-history` (ikisi `.RequireAuthorization()` + CurrentUser)
- [X] T018 [P] [US3] Web: SearchHistory.tsx sil (öğe) + tümünü-temizle (onay diyaloğu) butonları + personalization.ts `deleteItem(query,token)`/`clearAll(token)`
- [ ] T019 [US3] CANLI doğrula: tek sil (DB'de o sorgunun TÜM satırları gitti) + tümünü temizle + geri gelmez + kullanıcı izolasyonu (quickstart US3)

---

## Phase 6: Polish & cross-cutting

- [X] T020 [P] Pers/../FLOW.md (src/services/personalization/FLOW.md): okuma (geçmiş sorgu) + silme adımları + "append-only→kullanıcı-silme" invariant revizyonu (kapalı hotspot: `(closed: kullanıcı kendi kaydını hard siler)`)
- [X] T021 [P] CLAUDE.md BC haritası `personalization` satırı: ilk HTTP yüzeyi (search-history GET/DELETE, auth-zorunlu kendi-veri) + silme invariant revizyonu
- [X] T022 `dotnet build && dotnet test` yeşil + `cd ~/dev/ECommerceWeb && npm run build` temiz

---

## Dependencies

- Setup (T001-T005) → Foundational (T006-T007) → US1 (T008-T012) → US2 (T013-T014) → US3 (T015-T019) → Polish (T020-T022).
- T007 (projection) T008 (list) ve T015 (delete-match) için ŞART.
- T009/T017 aynı dosya (EndpointExtension) → sıralı (ikisi de Program.cs map'ine dokunur).
- US2, US3 UI'ı US1 sayfasına (T011) dayanır.

## Paralel fırsatlar

- T002/T003/T005 birlikte (farklı dosyalar).
- T006 (test) tek başına; T010 (api client) T008/T009'dan bağımsız başlar.
- T015/T016 birlikte (ayrı dosya); T018 (React) backend'den bağımsız yazılabilir, canlıda T017 ile birleşir.

## MVP

**US1 (Phase 1-3)** = teslim edilebilir MVP: login kullanıcı geçmişini görür. US2/US3 artımlı.
