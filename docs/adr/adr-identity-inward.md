---
status: Kabul
---

# ADR: Kimlik Makamı Geri İçeri — 084 Tersine Döndü (005)

> **Gerekçe katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + spec `005-identity-inward`.
> İlgili: [adr-external-agent-oauth-061](adr-external-agent-oauth-061.md) (dış-agent OAuth dünyası —
> artık EC kapsamı dışında).

**Durum:** Kabul · **Feature:** 005-identity-inward · **Tersine çevirdiği karar:** 084 (uygulama-nötr platform IdP, AgentPlatform repo'su)

---

## 1. Karar — tek cümle

**Kimlik makamı ECommerce çözümünün İÇİNDE yaşar:** AgentPlatform `Identity.Server`'ının KOPYASI
`src/others/Identity.Server` olarak gelir (OpenIddict + ASP.NET Identity + rol→scope RBAC +
server-rendered `/Admin/*`); AgentPlatform repo'suna dokunulmaz, iki kimlik makamı bağımsız evrilir
(PaymentGateway oradakiyle yaşamaya devam eder).

## 2. Neden 084 tersine döndü

084'ün gerekçesi çok-uygulamalı platform vizyonuydu (EC = "app #1", SSO, DCR'li dış agent'lar).
Araya giren gerçekler:

- **Temiz-sayfa pivotu (2026-10-30):** BYO-agent/MCP yüzeyi terk edildi — DCR/consent/dış-istemci
  makinesinin EC'de tüketicisi kalmadı.
- **Kâşif kararı 1 (2026-11-04):** SSO YOK, tek-repo odak — dış IdP'ye OIDC redirect bağımlılığı
  (iki repo ayağa kaldırma, cross-repo drift) günlük geliştirmeyi yavaşlatıyordu.
- **Platform vizyonu askıda:** çok-uygulama AppRegistry'nin EC'deki tek kaydı zaten "ecommerce" idi.

## 3. Kopyanın sınırları

- **Yüzeyde kırpıldı:** DCR (`/connect/register`) + consent + dış-agent istemcileri + ApiKey uçları +
  paymentgateway/platform app kayıtları + super-admin rolü. DB şemasına DOKUNULMADI (migration'lar
  aynen; boş ApiKey/UserScope tabloları zararsız — yeniden üretim riskine değmez).
- **Aynen korundu:** OpenIddict akışları (code+PKCE, m2m, refresh), ScopeResolver kesişimi
  (`requested ∩ (identity ∪ (ceiling ∩ roleBundle))`), refresh'te güncel-rol süzmesi, additive-merge
  RBAC seed, son-admin kilidi, ForwardedHeaders.
- **Issuer değişmedi:** `https://localhost:5001` — EC servislerinin `IdentityOption.Address` config'i
  birebir aynı kaldı; değişen yalnız o adreste kimin dinlediği.

## 4. Teknoloji istisnası (İlke II/III/IV dışı)

Identity.Server **EF Core + Npgsql + Razor Pages** ile gelir — Marten/VSA/Result dünyasına
ÇEVRİLMEDİ. Gerekçe: kopya sadakati (çalışan, canlıda doğrulanmış makine) + OpenIddict'in EF
store'ları birinci-sınıf. Identity domain BC'si değil destek makamıdır; anayasa ilkelerinin hedef
alanı dışında. 084 döneminde de aynı mimariydi — değişen yalnız repo sınırı.

## 5. Platform.Auth'un ölümü

Nötr auth kablosu (IdentityOption + AddAuthenticationAndAuthorizationExtension) paket olmasının tek
sebebiydi: IdP dış repodaydı. İçeri dönünce paket anlamsızlaştı → kod `Common/Auths`'a katlandı
(namespace `Common.Auths`), GitHub Packages feed'i + `GH_PACKAGES_TOKEN` gereksinimi + bayat-paket
gölge tuzağı tarihe karıştı.

## 6. Ne zaman yeniden tersine döner

Platform vizyonu canlanır (ikinci gerçek uygulama + SSO ihtiyacı) ya da EC istemcileri AgentPlatform
ekosistemiyle birleşirse: kopya tek makama konsolide edilir. O gün bu ADR Superseded olur; bugünkü
düzen o güne dek bağlayıcıdır.
