# Implementation Plan: Kimlik Makamı İçeri Dönüyor (Identity.Server kopyası)

**Branch**: `005-identity-inward` | **Date**: 2026-11-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-identity-inward/spec.md`

## Summary

AgentPlatform `Identity.Server`'ı `src/others/Identity.Server` olarak KOPYALANIR (kaynak repoya sıfır dokunuş): OpenIddict (code+PKCE + m2m + refresh) + ASP.NET Identity + RBAC motoru + `/Admin/*` Razor sayfaları. Yüzeyde kırpılır: DCR/consent/dış-agent istemcileri/ApiKey uçları/yabancı app kayıtları (research R2). `Platform.Auth` Common'a katlanır, GitHub Packages feed'i NuGet.config'ten kalkar. Issuer `https://localhost:5001` sabit — EC servis config'i değişmez (R7). ADR + CLAUDE.md revizyonu işin parçası.

## Technical Context

**Language/Version**: .NET 10 (çözüm geneli; Nullable+ImplicitUsings)

**Primary Dependencies**: OpenIddict (server+EF stores), ASP.NET Identity, EF Core + Npgsql (YALNIZ bu proje — Marten değil, R8), Razor Pages; sürümler Directory.Packages.props'a (CPM)

**Storage**: PostgreSQL `identityDb` (Aspire'daki mevcut postgres resource'una yeni database; EF migrations açılışta uygulanır — kopyadaki düzen neyse o korunur)

**Testing**: xUnit + Shouldly, `tests/Identity.Server.Tests` — yalnız saf birimler: `ScopeResolver`, `RoleAssignmentRules`, `AppRegistryValidator` (İlke VI; host/EF harness YOK)

**Target Platform**: Aspire AppHost altında servis; HTTPS zorunlu (`https://localhost:5001` launchSettings)

**Project Type**: Web servis (OIDC makamı + server-rendered yönetim UI) — destek/altyapı, BC değil

**Constraints**: AgentPlatform repo'suna SIFIR dokunuş (SC-004) · issuer adresi mevcut EC config'leriyle birebir (R7) · kayıt→customer, admin yalnız seed (FR-003/004) · sır log/ekran/yanıta yazılmaz (İlke VIII)

**Scale/Scope**: tek-app registry (ecommerce), 2 rol (customer/admin), 3 seed istemci (react-web, order-saga, payment-s2s), ~56 dosyalık kopya + kırpma

## Constitution Check

- **İlke I (BC izolasyonu):** GEÇER — Identity kendi DB'si (identityDb); hiçbir servis bu DB'ye dokunmaz, iletişim token/OIDC üzerinden. Identity BC değil destek makamı; BC haritasına "çözüm içi destek" satırı (FR-010).
- **İlke II/III (zengin aggregate, VSA/CQRS/Marten):** İSTİSNA — kopya EF Core + Razor Pages'tır, VSA'ya çevrilmez. Gerekçe: kopya sadakati + OpenIddict store olgunluğu; domain BC'si olmadığından ilkelerin hedef alanı dışında. ADR'de kayda geçer (R8). 084 döneminde de aynı mimariydi (dış repoda).
- **İlke IV (Result):** İSTİSNA aynı kapsamda — Razor/OpenIddict akışları kendi hata modelini kullanır; EC servis koduna Result dışı desen SIZMAZ.
- **İlke V (scope yetki):** GEÇER ve güçlenir — `[RequiredScope]`/`AuthorizationScopes` değişmez; rol→scope DB + admin sayfaları içeri gelir. `/Admin/*` cookie-auth istisnası anayasada zaten tanımlı.
- **İlke VI (test):** GEÇER — yalnız saf birim testleri (ScopeResolver, RoleAssignmentRules, AppRegistryValidator); WebApplicationFactory/Testcontainers YOK.
- **İlke VII (FLOW.md):** Identity domain süreci taşıyan BC değil; kısa süreç notu `src/others/Identity.Server/FLOW.md` yerine ADR+CLAUDE.md'de (spec assumption). Gate ihlali yok.
- **İlke VIII (sır):** GEÇER — bootstrap admin şifresi Aspire secret parametresi; log/yanıt/ekranda görünmez.

**Post-design re-check:** R1-R10 kararları yukarıdaki istisnaların kapsamını genişletmiyor; ihlal yok.

## Project Structure

### Documentation (this feature)

```text
specs/005-identity-inward/
├── spec.md
├── plan.md              # bu dosya
├── research.md          # R1-R10 kararları
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── oidc-surface.md  # uç + istemci + scope envanteri
│   └── admin-pages.md   # /Admin yüzeyi davranış sözleşmesi
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/others/Identity.Server/          # AgentPlatform kopyası (kırpılmış)
├── Program.cs                       # OpenIddict+EF+RBAC kurulumu; DCR/ApiKey/consent kayıtları ÇIKARILMIŞ
├── Config.cs · appsettings.json     # tek-app AppRegistry (ecommerce scope/rol/istemcileri)
├── Options/                         # AppRegistry + Validator + BootstrapAdmin
├── Connect/                         # Authorize/Token/UserInfo/Logout + SeedHostedService + handler'lar (Register/DCR YOK)
├── Rbac/                            # ScopeResolver, RoleScopeQuery, ClientCeilingResolver, RoleAssignment*
├── Pages/Account/                   # Login, Create(→customer), Logout, AccessDenied (Consent YOK)
├── Pages/Admin/                     # Users, Roles, Roles/Scopes (cookie + AdminRole policy)
├── Data/ + Migrations/              # AYNEN (R1 — ApiKey/UserScope tabloları şemada kalır)
└── Properties/launchSettings.json   # https://localhost:5001 (R7; launchSettings TUZAĞI: dosyasız proje Production açılır)

src/others/Common/                   # Platform.Auth katlaması (R6)
├── Options/IdentityOption.cs        # taşınan POCO
└── Extensions/AuthenticationExtension.cs  # AddAuthenticationAndAuthorizationExtension

tests/Identity.Server.Tests/         # ScopeResolverTests, RoleAssignmentRulesTests, AppRegistryValidatorTests
```

Değişen mevcut dosyalar: `ECommerceAgent.slnx` (+2 proje) · `Directory.Packages.props` (−Platform.Auth, +OpenIddict/EFCore/Npgsql) · `NuGet.config` (github feed + credential bloğu kalkar) · `src/aspire/AppHost/AppHost.cs` (identityDb + identity-server + admin secret parametreleri; IdP yorumu güncellenir) · `Common.csproj` (−Platform.Auth ref) · `docs/adr/adr-identity-inward.md` (YENİ) · `CLAUDE.md` (Yapma-listesi, BC haritası, Teknoloji).

## Uygulama fazları (tasks için iskelet)

1. **Kopya + kırpma:** proje dosyaları gelir; DCR/consent/ApiKey uçları/yabancı app'ler çıkar (R2); tek-app appsettings (R4/R5); CPM sürümleri props'a; slnx kaydı.
2. **Aspire kablosu:** identityDb + AddProject + secret parametreleri (R9); HTTPS/launchSettings (bilinen tuzak).
3. **Platform.Auth katlaması:** kod Common'a, namespace hizası, paket/feed temizliği (R6); tüm çözüm derlenir.
4. **Testler (test-first saf birimler):** ScopeResolver kesişim matrisi, RoleAssignmentRules son-admin kilidi, AppRegistryValidator kuralları.
5. **Belge:** ADR + CLAUDE.md revizyonu (FR-010) + vault 05 işaretleri.
6. **Canlı doğrulama (quickstart):** AgentPlatform kapalıyken kayıt→login→token→scope'lu istek; admin seed + /Admin akışı; reset-mail log'u.
