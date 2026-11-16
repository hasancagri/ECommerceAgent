# Implementation Plan: Kullanıcı Arama Kaydı + App-BFF

**Branch**: `012-search-capture-bff` | **Date**: 2026-11-16 | **Spec**: [spec.md](./spec.md)

## Summary

Yeni **app-BFF** servisi istemci-yüzü aramanın tek giriş noktası olur (istemci → Gateway edge → BFF → BC). BFF, Catalog'un REST aramasını (011) çağırıp sonucu döndürür; login aramada `UserSearched` integration event'i yayınlar (RabbitMQ fanout). Yeni **Personalization.Api** BC bu event'i tüketip ham arama kaydını izole `personalizationDb`'ye append eder. Anonim arama çalışır ama kaydedilmez. Kişiselleştirme/öneri/ML YOK — yalnız sinyal temeli.

## Technical Context

**Language/Version**: .NET 10 (`Nullable`+`ImplicitUsings`)

**Primary Dependencies**: Wolverine (in-proc bus + RabbitMQ fanout), Marten (Personalization document store), Npgsql/Postgres, JWT bearer (OIDC, mevcut), Aspire service discovery + HttpClient (BFF→Catalog). BFF: Minimal API + WolverineFx.RabbitMQ (yalnız publish). YARP (Gateway rota).

**Storage**: `personalizationDb` (yeni, Postgres/Marten) — `UserSearchRecord` document (append-only). BFF **durumsuz** (DB yok). Catalog DB değişmez.

**Testing**: Saf birim test-first (İlke VI): `UserSearchRecord.Create` validasyonu. BFF orkestrasyon + event publish/consume host-gerekli → Aspire canlı doğrulama (quickstart).

**Target Platform**: Aspire AppHost; yeni `Bff` + `Personalization.Api` + `personalizationDb` container.

**Project Type**: Mevcut sisteme (1) yeni BFF app servisi (BC değil, orkestratör) + (2) yeni Personalization BC + React istemci değişikliği.

**Performance Goals**: Arama etkileşimli kalır (<1 sn, BFF hop'u ihmal edilebilir); kayıt eventual (saniyeler).

**Constraints**: BC izolasyonu — Catalog değişmez (BFF public REST'ini çağırır); kayıt aramayı bloklamaz (publish `[Transactional]` outbox, request path dışı); login-only capture; BFF durumsuz (mobile-hazır ortak giriş).

**Scale/Scope**: 1 yeni app servisi + 1 yeni BC + 1 yeni DB + 1 integration event + 1 gateway rotası + React arama yönlendirme.

## Constitution Check

| İlke | Durum | Not |
|---|---|---|
| I BC İzolasyonu | GEÇER | Personalization kendi DB'si; cross-BC tek kanal = `UserSearched` integration event (fanout). BFF **BC değil** — app/edge orkestratör, domain DB'si yok; Catalog'un **public REST aramasını** çağırır (tarayıcının çağırdığı yüzey — BC→BC backchannel değil). Constitution "repoda BFF yok" notu bu dilimde bilinçli değişir (bkz. Complexity). |
| II Zengin Aggregate | GEÇER (sınırlı) | `UserSearchRecord` append-only log belgesi — `Create` fabrikası + validasyon (userId/query guard); mutasyon yok (okuma-sonrası değişmez). Rich-aggregate yüzeyi küçük (log satırı). |
| III VSA/CQRS | GEÇER | Personalization tüketici = `Process/`/`Consumers` (süreç-güdümlü, kullanıcı slice değil). BFF = orkestrasyon endpoint (Minimal API), salt okuma+publish. |
| IV Result Pattern | GEÇER | Personalization handler `FeatureResultModel`; `UserSearchRecord.Create` `ResultDomain`. |
| V Scope Yetki | GEÇER | Arama anonim (keşif); capture login'de. BFF JWT opsiyonel (token varsa userId). Yeni scope YOK (capture login'in yan-etkisi). |
| VI Domain-TDD | GEÇER | `UserSearchRecord.Create` test-first; BFF/event canlı doğrulanır (İlke VI host dayatmaz). |
| VII FLOW.md | GEÇER | Personalization `FLOW.md` (⇦ UserSearched → append). BFF domain süreci yok (orkestratör) → FLOW gerekmez, yüzey plan+quickstart'ta. |
| VIII Sır | N/A | Sır render yok; userId iç kimlik, insan-yüzeye dönmez (kayıt okunmaz bu dilimde). |
| Teknoloji | GEÇER | Wolverine fanout + Marten + Aspire — mevcut desenler. BFF yeni ama .NET/Minimal API standart. |

**Gate: PASS** — tek bilinçli ekleme (BFF) Complexity'de gerekçeli.

## Project Structure

### Documentation (this feature)

```text
specs/012-search-capture-bff/
├── plan.md · research.md · data-model.md · quickstart.md
└── contracts/
    ├── bff-search.md              # BFF arama endpoint'i (istemci sözleşmesi)
    ├── user-searched-event.md     # UserSearched integration event kontratı
    └── personalization-ingest.md  # tüketici + UserSearchRecord saklama
```

### Source Code (repository root)

```text
src/others/Shared/                         # + IntegrationEvents/UserSearched.cs (paylaşılan kontrat)
src/others/Gateway/appsettings.json        # + rota: /api/discovery/** → bff cluster (edge dumb kalır)

src/others/Bff/                            # YENİ app-BFF (BC değil, orkestratör; src/others — Gateway yanı)
├── Bff.Api.csproj
├── Program.cs                             # JWT auth + Wolverine(RabbitMQ publish) + HttpClient(catalog, service discovery)
├── Features/Search/SearchDiscovery.cs     # POST /api/discovery/search → Catalog REST çağır + sonuç + (login) publish UserSearched
└── Extensions/…                           # messaging (publish) + auth kablosu

src/services/personalization/Personalization.Api/   # YENİ BC
├── Personalization.Api.csproj
├── Program.cs · Extensions/MartenExtensions.cs · Extensions/MessagingExtensions.cs
├── Domains/SearchHistory/UserSearchRecord.cs        # append-only log belgesi (Create fabrikası)
├── CatalogConsumers.cs  (ya da Process/)            # UserSearched tüketir → UserSearchRecord append
└── FLOW.md

src/aspire/AppHost/AppHost.cs              # + personalizationDb + Bff projesi + Personalization.Api

tests/Personalization.Api.Tests/           # UserSearchRecord.Create (test-first)

# React (repo-dışı ~/dev/ECommerceWeb):
src/api/catalog.ts                         # arama → /api/discovery/search (BFF); login'de bearer
```

**Structure Decision**: BFF `src/others/Bff` (Gateway gibi destek/edge, BC değil — kendi DB'si yok, domain aggregate'i yok). Personalization tam BC (`src/services/personalization`, kendi DB). Kontrat `Shared`. Gateway yalnız yeni rota (dumb edge). Catalog'a dokunulmaz.

## Complexity Tracking

| İhlal/Ekleme | Neden Gerekli | Reddedilen Basit Alternatif |
|---|---|---|
| Yeni **BFF** servisi (constitution "repoda BFF yok" notunu değiştirir) | Web+mobile ortak giriş noktası + kullanıcı-niyet (arama) sinyalinin doğal yayıncısı + ileride ChatAgent/feed evi | **Catalog `UserSearched` yayınlasın**: aktivite-sorumluluğunu saf ürün BC'sine sızdırır (İlke I kavram-karışması) + mobile için ortak giriş yok. **React doğrudan Personalization'a POST**: sync istek (kullanıcı kararıyla red) + dayanıksız. |

(Not: BFF domain DB'si/aggregate'i olmayan orkestratör olduğundan yeni "BC" sayılmaz; İlke I BC-izolasyonu ihlali değil — Complexity'de yalnız "yeni yüzey" olarak izlenir.)
