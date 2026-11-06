# Contract — OIDC yüzeyi (çözüm-içi Identity.Server)

Issuer: `https://localhost:5001` (dev; EC servislerindeki IdentityOption.Address DEĞİŞMEZ — R7).

## Uçlar (kopyada KALAN)

- `GET/POST /connect/authorize` — code+PKCE; `prompt=create` → kayıt sayfası. Login cookie → rol→scope süzme (ScopeResolver).
- `POST /connect/token` — code+PKCE / client_credentials / refresh. Refresh güncel rol demetiyle yeniden süzer.
- `GET /connect/userinfo` — Bearer ile claim'ler.
- `GET/POST /connect/logout` — oturum sonu.
- `GET /.well-known/openid-configuration` — discovery; `registration_endpoint` YOK (DCR kırpıldı).

## Kırpılan yüzey (istek → 404)

`POST /connect/register` (DCR) · consent ekranı · `/api-keys/*` uçları.

## İstemciler

| ClientId | Tip | Akış | Kullanıcı |
|---|---|---|---|
| react-web | public (PKCE) | code + refresh | React SPA (Dilim 0+; şimdilik tarayıcı/cURL doğrulaması) |
| order-saga | confidential m2m | client_credentials | Order→Payment S2S (SagaTokenHandler — mevcut) |
| payment-s2s | confidential m2m | client_credentials | Payment S2S (mevcut) |

## Token sözleşmesi

- `scope` claim ARRAY (ScopeClaimArrayHandler — `ScopeClaimArrayHandler`/TokenType URN tuzağı aynen geçerli).
- `aud` scope→resource haritasından (AppRegistry).
- Kayıtla doğan kullanıcı token'ı customer demeti taşır; admin demeti yalnız seed admin'de.
