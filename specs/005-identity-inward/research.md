# Research — 005 Kimlik Makamı İçeri (kopya kararları)

Kaynak: AgentPlatform envanter keşfi (2026-11-05, Explore raporu) + brainstorm kararları.

## R1 — Kopya bütünlüğü: DB şemasına dokunma, yüzeyde kırp
- **Decision:** EF migration'ları (Initial + AddRoleScopes) AYNEN kopyalanır; kırpma yalnız yüzeyde (endpoint map'leri, sayfalar, seed config). ApiKey/UserScope tabloları şemada kalır, uçları map edilmez.
- **Rationale:** Migration yeniden-üretimi hataya açık; boş tablo zararsız. Kırpılan yüzeye istek 404 (spec edge case).
- **Alternatives:** Şemadan da kırpıp migration'ları yeniden üretmek — kazanç yok, risk var; red.

## R2 — Kırpılan yüzey listesi (kopyadan ÇIKAR)
- **Decision:** DCR (`RegisterEndpoint` + `DcrRequestValidator` + `RegistrationEndpointMetadataHandler` + DcrTemplate config) · Consent sayfası + Explicit akışı (tüm istemciler seed/Implicit) · dış-agent istemcileri (external-admin-agent, external-customer-agent, mcp-gateway-discovery) + Claude redirect URI'ları · `/ApiKeys` uçları + `apikeys.manage` policy kaydı · paymentgateway/platform app kayıtları.
- **Rationale:** Spec FR-002/FR-011; MCP söküm çizgisi. Ölü kod kopyada temiz başlasın.

## R3 — Kalan parçalar (AYNEN gelir)
- **Decision:** OpenIddict server config (code+PKCE, client_credentials, refresh; ScopeClaimArrayHandler, IgnoreResourceParameterHandler) · `/Connect` authorize/token/userinfo/logout · `/Rbac` motoru (ScopeResolver, RoleScopeQuery, ClientCeilingResolver, RoleAssignmentService/Rules, KnownScopes) · `/Pages/Account` (Login/Create/Logout/AccessDenied) · `/Pages/Admin` (Users, Roles, Roles/Scopes) · `/Options` AppRegistry + validator · `/Data` + migrations · SeedHostedService · ForwardedHeaders.
- **Rationale:** Rol→scope yönetimi + admin ekranları kapsam İÇİ (kullanıcı kararı); ScopeResolver/refresh-süzme davranışı aynen korunmalı.

## R4 — AppRegistry tek-app'e config'le iner, kod kalır
- **Decision:** AppRegistry/Validator KODU değişmez; appsettings yalnız `ecommerce` app'ini taşır (scope'lar EC BC'leri; roller customer/admin — super-admin ECommerce'te GELMEZ, platform kavramıydı).
- **Rationale:** Validator kuralları (scope kesişmezliği, phantom scope) tek app'te de değerli; kod kırpmak bakım yükü.
- **Alternatives:** Registry'yi söküp düz option — kopya sadakatini bozar, kazanç az.

## R5 — Seed istemciler (EC appsettings)
- **Decision:** `react-web` (public, code+PKCE+refresh, redirect `http://localhost:5173/*` dev; tek müşteri istemcisi) · `order-saga` + `payment-s2s` m2m istemcileri MEVCUT EC kullanımı sürdüğü için kalır (SagaTokenHandler bugün bu makamdan token alıyor).
- **Rationale:** Spec FR-002 React PKCE; S2S istemcileri kopuk kalırsa Order→Payment zinciri ölür.

## R6 — Platform.Auth → Common katlaması
- **Decision:** `IdentityOption` + `AddAuthenticationAndAuthorizationExtension` + global using'ler Common'a taşınır; namespace EC'nin beklediği `Common.*` düzenine hizalanır (bayat-paket gölge tuzağının tersi — artık tek kaynak). `Platform.Auth` PackageVersion + Common'daki referans + NuGet.config `github` feed'i ve credential bloğu kalkar (nuget.org kalır). GH_PACKAGES_TOKEN ihtiyacı biter.
- **Rationale:** Tek tüketici Common; feed PAT'ı dev/CI sürtünmesi.

## R7 — Issuer adresi değişmiyor
- **Decision:** Identity.Server EC'de de `https://localhost:5001` (launchSettings) — EC servislerindeki `IdentityOption.Address` değerleri OLDUĞU GİBİ kalır; değişen yalnız o adreste artık kimin dinlediği.
- **Rationale:** Sıfır servis-config değişikliği; HTTPS zorunlu tuzağıyla uyumlu. AgentPlatform aynı anda çalıştırılmaz (tek-proje odak).

## R8 — Teknoloji istisnası: EF Core + Razor Pages
- **Decision:** Identity.Server EF Core + Npgsql + Razor Pages ile GELİR (Marten/VSA'ya çevrilmez); sürümler Directory.Packages.props'a (CPM) eklenir.
- **Rationale:** Kopya sadakati + OpenIddict EF store'ları olgun; yeniden-mimari bu spec'in işi değil. Anayasa istisnası ADR'de gerekçelenir (II/III ilkeleri domain BC'lerine; identity destek altyapısı).

## R9 — Admin seed EC düzeniyle
- **Decision:** BootstrapAdmin config'i Aspire secret parametresiyle beslenir (`identity-admin-email/password` user-secrets); rol `admin` (super-admin yok). Secret log/yanıtta görünmez (İlke VIII).
- **Rationale:** Spec FR-004; gateway-admin-api-key deseniyle tutarlı.

## R10 — Mail: log/mock
- **Decision:** Şifre sıfırlama e-postası `ILogger` üzerinden link loglar (dev); SMTP kararı vault 03'te açık kalır.
