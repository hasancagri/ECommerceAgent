# Data Model — 005 Identity.Server (identityDb, EF Core)

Şema AgentPlatform kopyasıyla BİREBİR gelir (R1 — migration'lar aynen; kırpma yüzeyde).

## Tablolar (EF Core + OpenIddict store'ları)

- **ApplicationUser** (ASP.NET Identity): e-posta tekil; kayıt yolu HER ZAMAN `customer` rolü atar. Admin yalnız BootstrapAdmin seed'inden.
- **IdentityRole + RoleScope**: rol→scope eşlemesi; `(RoleId, Scope)` unique. Seed rolleri: `customer`, `admin` (super-admin GELMEZ — R4). Seed additive-merge: eksik scope eklenir, elle eklenen SİLİNMEZ.
- **UserScope**: `(UserId, Scope)` unique — şemada kalır, yüzeyi yok (R1).
- **ApiKey**: şemada kalır, uçları map edilmez (R1/R2).
- **OpenIddict tabloları**: Applications (seed istemciler), Authorizations, Scopes, Tokens.

## Seed verisi (appsettings `AppRegistry`, tek app: `ecommerce`)

- **Scope'lar**: EC BC scope'ları (catalog.*, basket.*, order.*, payment.*, stock.write, customer.*, reviews.write, library.*, discount.*, ops.deadletter) + kimlik scope'ları (openid, profile, email, roles, offline_access) + `identity.roles.manage`. `apikeys.manage` ve yabancı app scope'ları GELMEZ.
- **İstemciler** (R5): `react-web` (public, code+PKCE+refresh, redirect http://localhost:5173/*) · `order-saga` (m2m) · `payment-s2s` (m2m). Hepsi Implicit consent (ekran yok).
- **Roller**: `customer` = kişisel yüzey demeti; `admin` = customer + catalog/stock/discount yazma + ops.deadletter + identity.roles.manage.
- **BootstrapAdmin**: e-posta+şifre Aspire secret'tan; idempotent; yoksa-oluştur; `admin` rolü.

## Durum geçişleri / davranış kuralları

- **Token verme**: `Granted = requested ∩ (identityScopes ∪ (clientCeiling ∩ roleBundle))` (ScopeResolver — aynen korunur). Tavan-üstü talep hata değil, sessiz elenir.
- **Refresh**: GÜNCEL rol demetiyle yeniden süzülür — rol düşerse yetki daralır.
- **Son admin kilidi**: son admin'in rolü düşürülemez (RoleAssignmentRules INV-4).
- **Seed rolleri silinemez**; atanmış kullanıcısı olan rol silinemez.
