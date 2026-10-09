---
status: Kabul
---

> ⚠️ 2026-09-29: Identity.Server AgentPlatform'a taşındı (084/commit 7bfcdae). DCR/ScopeResolver/consent kodu orada. Bu repoda relying-party; yalnız `McpResourceMetadataExtension` (Common) yerel kalır.

# ADR: Dış Agent MCP Erişimi — OAuth 2.1 (061)

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md`. As-built kod:
> `src/others/Identity.Server/Connect/{ExternalAgentDefaults,DcrRequestValidator,RegisterEndpoint,
> RegistrationEndpointMetadataHandler,IgnoreResourceParameterHandler}.cs`, `AuthorizeEndpoint.cs`,
> `Pages/Account/Consent/`, `src/others/Common/Extensions/McpResourceMetadataExtension.cs`,
> `src/services/{basket,order,customer,payment}/*/Program.cs`, gateway `appsettings.Development.json`.

## Karar (tek cümle)

Kullanıcının **kendi AI agent'ı** (Claude Code/Desktop) mağaza MCP uçlarına **OAuth 2.1** ile
bağlanır: bir kerelik tarayıcı login+consent, sonrası refresh token'la tamamen ekransız. Yeni
doğrulama katmanı YOK — token mevcut JwtBearer zincirinden geçer. UserKey yolu dokunulmadan yan
yolda kalır.

## Dert (kullanıcının niyeti)

Dış agent, tarayıcıdan **bir kez** kimlik onayı versin; sonra kullanıcı hiçbir mağaza ekranı
açmadan **yalnız yazışarak** uçtan uca alışveriş yapsın (ara → sepete at → sipariş ver → görüntüle).
UserKey modelinden farkı: burada **istemci Claude'dur** ve
OAuth dansını Claude/mcp-remote otomatik yürütür; kullanıcı sadece login+onay yapar.

## Neden UserKey yetmedi

UserKey opak anahtarı **elle** paylaşmayı gerektirir (n8n / 3. taraf
senaryosu). Claude Code/Desktop ise **DCR'siz auth server'ı REDDEDER** (kendi client'ını kaydeder,
elle clientId verme yolu yok). İki model bir arada yaşar: UserKey = makineye elle anahtar; OAuth =
kullanıcının kendi agent'ı tarayıcı seremonisiyle.

## Sekiz karar (R1–R8)

- **R1 — Keşif: RFC 9728.** Korumalı MCP açan her servis `/.well-known/oauth-protected-resource`
  dokümanı sunar (`resource`, `authorization_servers`, `scopes_supported`); kimliksiz `/mcp` isteği
  `401 + WWW-Authenticate: Bearer resource_metadata="...", scope="..."` döner. Tek yerde
  (`Common/McpResourceMetadataExtension`), servis tek satırla açar.
- **R2 — İstemci kaydı: RFC 7591 DCR ZORUNLU.** `POST /connect/register` (anonim) OpenIddict public
  client üretir (PKCE, auth_code+refresh). Discovery'ye `registration_endpoint` event handler'la
  eklenir. Claude DCR'siz sunucuyu reddettiği için şart.
- **R3 — Kimlik: mevcut JwtBearer + seçici zorlama.** basket/order/customer/payment `MapMcp`'ye
  `.RequireAuthorization()` alır; storefront/catalog/stock MCP **anonim kalır** (İlke V: anonim
  gezinme meşru; ChatAgent PUBLIC personası storefront'u tokensiz kullanıyor — regresyon önlendi).
- **R4 — Scope demeti (kapalı).** `storefront.read`, `basket.read/write`, `order.read/write`,
  `customer.read`, `payment.read` + `openid profile email offline_access`. Yönetim scope'ları
  (catalog.write, stock.write, identity.roles.manage) demete GİREMEZ; `client_credentials` verilmez.
  Kaynak: `ExternalAgentDefaults`.
- **R5 — Audience + resource (RFC 8707).** Audience mevcut scope→resource eşlemesinden gelir
  (`Config.ScopeResources`). Claude'un gönderdiği `resource` parametresi **YOK SAYILIR**
  (`IgnoreResourceParameterHandler`) — aksi halde OpenIddict `invalid_target` verir (aşağıda fix 2).
- **R6 — Consent: tek sayfa, Explicit.** DCR client'ları `ConsentType=Explicit`; tek consent
  sayfası (scope listesi + Onayla/Reddet). Seed client'lar (ecommerce.bff…) `Implicit` kalır —
  davranış değişmez.
- **R7 — Token yaşam döngüsü.** Refresh token (`offline_access`) → sessiz yenileme; OpenIddict
  revocation endpoint açıldı (`/connect/revocation`). Access ömrü mevcut default.
- **R8 — Topoloji: gateway + localhost.** ⚠️ **GÜNCELLENDİ (073/085):** per-BC path'ler
  (`/mcp/basket`…) söküldü (Gateway MCP route'ları, commit d5d8965) — artık **tek `/mcp` fasadı**
  (AgentPlatform). Claude tek adrese bağlanır; fasad tool'ları alt BC'lerden toplar, ada göre proxy'ler.
  R1-R7 kararları geçerli; yalnız bu R8 + aşağıdaki per-BC path örnekleri tarihseldir.

## Uçtan uca akış (runtime)

> ⚠️ Aşağıdaki akış per-BC path (`/mcp/basket`) ile yazıldı — 073/085'te **tek `/mcp` fasadına**
> birleşti (mekanizma aynı: 401+PRM → DCR → login+consent → Bearer; yalnız URL tek uç). Tarihsel okunur.

1. Claude Desktop → **mcp-remote** (npx köprüsü) → `GET /mcp/basket` → **401** +
   `WWW-Authenticate: Bearer resource_metadata="http://localhost:<gw>/.well-known/oauth-protected-resource/mcp/basket", scope="basket.read basket.write"`.
2. mcp-remote → `GET /.well-known/oauth-protected-resource/mcp/basket` → `resource` +
   `authorization_servers:[https://localhost:5001]` + `scopes_supported`.
3. mcp-remote → `GET https://localhost:5001/.well-known/oauth-authorization-server` →
   `registration_endpoint` + `token_endpoint_auth_methods_supported` (**none** dahil — fix 1).
4. mcp-remote → `POST /connect/register` (DCR) → `client_id` (public, PKCE, Explicit consent).
5. mcp-remote tarayıcı açar → `GET /connect/authorize` (code + PKCE + `resource` param) → cookie
   yoksa **login** (yeni kullanıcı: Register → `customer` rolü) → **Explicit** client olduğu için
   **consent sayfası** → Onayla → kalıcı `OpenIddictAuthorization` (Valid/Permanent) yazılır → `code`.
6. mcp-remote → `POST /connect/token` (code + code_verifier) → **access + refresh** token
   (`offline_access`). Access = düz JWT; `scope` çoklu-değer (`ScopeClaimArrayHandler`), `aud` =
   scope→resource eşlemesinden servis audience'ı.
7. MCP çağrısı `Authorization: Bearer <at>` → servis JwtBearer doğrular (issuer + audience + scope
   policy). Storefront tokensiz de çalışır (anonim).
8. Access dolunca → mcp-remote `refresh_token` grant ile **sessiz** yeni access (login YOK).
9. `POST /connect/revocation` (veya Claude disconnect) → sonraki çağrı 401 → yeniden seremoni.

## Consent kararı nerede doğar (kritik güvenlik noktası)

Onay **SUNUCUDA** doğar — istemci URL parametresiyle onay üretemez. `AuthorizeEndpoint`: client
`Explicit` ise `IOpenIddictAuthorizationManager.FindAsync(subject, appId, Valid, Permanent, scopes)`
ile **kalıcı onay** aranır. Yoksa → `/Account/Consent/Index`'e yönlendirir. Consent Onayla → orada
`CreateAsync` ile authorization yazılır; ikinci bağlantıda aynı scope onaylıysa **ekran atlanır**
(SC-003). Reddet → authorize'a `consent=denied` → `Results.Forbid(access_denied)`; **hiçbir kayıt
oluşmaz** (SC-005). Onay scope'ları authorize'daki granted kümeyle aynı formülden hesaplanır
(`ScopeResolver.Resolve` = requested ∩ (rol demeti ∪ kimlik scope'ları)) — yoksa FindAsync eşleşmez,
consent döngüye girer.

## Canlıda çıkan iki fix (curl'ün yakalamadığı, gerçek istemci davranışı)

1. **discovery'ye `none` eklendi** (`RegistrationEndpointMetadataHandler`,
   `TokenEndpointAuthenticationMethods.Add(None)`). mcp-remote,
   `token_endpoint_auth_methods_supported`'da `none` görmezse kendini **secret'lı** client olarak
   kaydetmeye çalışır (`client_secret_basic`); public-only validator `invalid_client_metadata` ile
   reddediyordu. Hata mcp-remote'ta `[object Response]` diye yutuluyordu.
2. **RFC 8707 `resource` parametresi yok sayıldı** (`IgnoreResourceParameterHandler`,
   authorize + token validate). Claude MCP URL'ini `resource` olarak gönderiyor; OpenIddict bunu
   scope→resource eşlemesinin (mantıksal audience adları: `basket.api`…) alt kümesi sanıp
   `invalid_target` (ID2190) veriyordu. Audience zaten scope eşlemesinden üretiliyor → parametre
   temizlenir, token yine yalnız ilgili servis audience'ını taşır.

## Token istemci tarafında nasıl saklanır (mcp-remote)

JWT'yi Claude Desktop DEĞİL, köprü **mcp-remote** saklar. Konum: `~/.mcp-auth/mcp-remote-v1/`,
düz dosya izni `0600` (macOS Keychain değil — OS kullanıcı izolasyonu). Dosyalar:
`<hash>_tokens.json` (access JWT + scope + expires_at + refresh), `<hash>_client_info.json`
(DCR `client_id`), `<hash>_code_verifier_*.txt` (PKCE). `<hash>` = server URL + config → her servis
ayrı dosya seti, token'lar karışmaz. Cache silinirse (`~/.mcp-auth`) yeniden seremoni.
> Karşıtlık: **ChatAgent** JWT'yi diske YAZMAZ — BFF token'ı RAM'de tutup çağrı anında enjekte eder
> (`TokenInjectingHandler`).

## İlkelere uyum

- **İlke I (BC izolasyonu):** DB paylaşımı yok; değişiklik auth/transport katmanında. MCP yine
  yalnız agent tüketir (Claude = agent istemcisi).
- **İlke V (scope yetki):** GÜÇLENDİ. DCR demeti kapalı registry'den; rol downstream'e sızmaz;
  DCR client'ları `client_credentials` alamaz; anonim gezinme yüzeyleri anonim kalır.
- **İlke VI (Domain-TDD):** DAR — saf birim yalnız `DcrRequestValidator` (redirect kalıpları +
  grant/auth-method süzgeci + scope kesişimi); geri kalan wiring/endpoint test-sonra + canlı.
- **İlke VII (FLOW.md):** TETİKLENMEDİ — domain süreci (command-event-policy) değişmedi, auth
  transport katmanı.

## Kapsam dışı (bilinçli)

Public HTTPS tünel + claude.ai/Desktop connector (public host); ürün görseli serving; DCR ucunda
rate-limit. Hepsi sonraki dilim.

## Durum

PR #85 (base master). Claude Desktop E2E canlı PASS (login→consent→"Authorization successful";
3 servis consent kaydı, token üretildi, korumalı MCP çağrısı çalışıyor). Headless curl zinciri +
`dotnet build`/`dotnet test` + guard'lar yeşil.
