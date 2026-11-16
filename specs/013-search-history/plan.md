# Implementation Plan: Arama Geçmişi Görüntüleme

**Branch**: `013-search-history` | **Date**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/013-search-history/spec.md`

## Summary

Login kullanıcı kendi arama geçmişini ayrı React sayfasında görür (tekilleştirilmiş, sayfalı, en yeni üstte), öğeye tıklayıp arama ekranında tekrar arar, tek öğeyi/tümünü KALICI siler. Personalization BC ilk HTTP yüzeyini kazanır: 1 query (paged deduped list) + 2 command (delete-one, clear-all) + opsiyonel-değil zorunlu JWT (anonim 401, kendi `userId`'si). Tekilleştirme/normalize saf-fonksiyon (İlke VI test-first); kalan katmanlar canlı doğrulanır.

## Technical Context

**Language/Version**: C# .NET 10 (Personalization.Api) + TypeScript/React (ECommerceWeb repo)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (`IMessageBus`), Common.Auths (JWT bearer + `ICurrentUser`), YARP gateway; React + Tailwind + react-oidc-context

**Storage**: `personalizationDb` → `personalization.mt_doc_user_search_record` (012, mevcut Marten belgesi)

**Testing**: xUnit + Shouldly — yalnız saf domain (dedup/normalize fonksiyonu) test-first; query/command/endpoint/auth = Aspire canlı doğrulama (İlke VI, CLAUDE.md)

**Target Platform**: Aspire orkestreli backend servisi + tarayıcı SPA

**Project Type**: Web (backend BC + frontend)

**Performance Goals**: Geçmiş listesi saniye-altı (kullanıcı başına kayıt az); tekilleştirme in-memory

**Constraints**: Kullanıcı yalnız kendi verisi (sub filtresi); hard delete; ek RBAC scope yok

**Scale/Scope**: Kullanıcı başına onlarca–yüzlerce ham kayıt; 1 query + 2 command + 1 React sayfası

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden.*

- **İlke I (BC izolasyonu)**: PASS — Personalization yalnız KENDİ verisini okur/siler; başka BC'ye senkron çağrı YOK. "Tekrar ara" bir frontend aksiyonu (React → gateway → Catalog arama ucu, 012 yolu), BC-arası sync değil.
- **İlke II (zengin aggregate)**: N/A — `UserSearchRecord` AggregateRoot değil, ham log belgesi (012 kararı). Silme = command handler'da toplu `IDocumentSession` silme (sahibe filtreli); god-service yok.
- **İlke III (VSA + CQRS, repository yok)**: PASS — query/command slice'ları `Domains/SearchHistory/Features/{Queries,Commands}`; Minimal API endpoint; handler doğrudan `IQuerySession`/`IDocumentSession`.
- **İlke IV (Result pattern)**: PASS — handler'lar `FeaturePagedResultModel`/`FeatureResultModel` döner.
- **İlke V (scope yetki)**: NOTE (ihlal değil) — uçlar authenticated gerektirir ama NAMED scope aramaz; veri katı biçimde kullanıcıya-ait (sub). Kişisel-veri erişimi kimliğe göredir, rol-otoritesine değil → yeni `KnownScope` eklenmez. Bilinçli, dokümante.
- **İlke VI (Domain-TDD)**: PASS — tekilleştirme+normalize saf fonksiyonu test-first (`SearchHistoryProjection`); handler/endpoint/auth canlı.
- **İlke VII (FLOW legibility)**: PASS — Personalization FLOW.md'ye okuma+silme adımları + "append-only→kullanıcı-silme" invariant revizyonu (kapalı hotspot) eklenir.
- **İlke VIII (sır görünmezliği)**: N/A — sır/kimlik-değeri render edilmez.

İhlal yok → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/013-search-history/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── search-history-api.md
└── checklists/requirements.md
```

### Source Code (repository root)

```text
src/services/personalization/Personalization.Api/
├── Program.cs                                  # + opsiyonel-DEĞİL JWT (ValidateAudience=false, inline — Catalog deseni), ICurrentUser, HttpContextAccessor, UseAuthentication/Authorization, endpoint map
├── Domains/SearchHistory/
│   ├── UserSearchRecord.cs                      # mevcut (012) — dokunulmaz
│   ├── SearchHistoryProjection.cs               # YENİ saf fonksiyon: ham kayıt listesi → dedup+normalize+sırala (test-first)
│   ├── SearchHistoryEndpointExtension.cs        # YENİ: GET list + DELETE item + DELETE all, hepsi RequireAuthorization
│   └── Features/
│       ├── Queries/GetSearchHistory.cs          # YENİ: paged deduped (userId'ye filtreli)
│       └── Commands/
│           ├── DeleteSearchHistoryItem.cs        # YENİ: bir normalize sorgunun TÜM satırlarını sil (userId'ye filtreli)
│           └── ClearSearchHistory.cs             # YENİ: kullanıcının tüm kayıtlarını sil
├── Constants/PersonalizationResourceConstants.cs # + gerekirse hata kodları
└── GlobalUsings.cs                              # + Common.Auths, JwtBearer, IdentityModel.Tokens

src/others/Gateway/appsettings.json              # + route /api/personalization/** → personalization cluster
src/aspire/AppHost/AppHost.cs                    # gateway.WithReference(personalizationApi) + WaitFor

tests/Personalization.Api.Tests/
└── SearchHistoryProjectionTests.cs              # YENİ: dedup/normalize/sıralama saf-unit (test-first)

# ECommerceWeb repo (~/dev/ECommerceWeb)
src/api/personalization.ts                       # YENİ istemci: list/deleteItem/clearAll (bearer)
src/pages/SearchHistory.tsx                      # YENİ "Arama Geçmişim" sayfası
src/ (router + menü)                             # + rota /gecmis + menü bağlantısı; öğe tıkla → arama ekranına git+çalıştır
```

**Structure Decision**: Mevcut VSA deseni (004/011). Personalization'a `Domains/SearchHistory` slice'ları eklenir; tekilleştirme saf fonksiyona çıkarılır (İlke VI testlenebilirliği + okuma-anı hesap). JWT inline (Common extension `ValidateAudience=true` → audience'sız SPA token'ı reddeder; Catalog 012 deseni ValidateAudience=false kullanılır). Frontend ayrı repo, mevcut sayfa/rota desenini izler.

## Complexity Tracking

> İhlal yok — boş.
