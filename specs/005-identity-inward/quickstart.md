# Quickstart — 005 canlı doğrulama

Ön koşul: AgentPlatform repo'su/IdP'si KAPALI (SC-001 bunu ölçer). User-secrets:
`Parameters:identity-admin-email` + `Parameters:identity-admin-password` set (AppHost).

```bash
dotnet build && dotnet test                                  # tümü yeşil
dotnet run --project src/aspire/AppHost/AppHost.csproj       # identity-server dahil kalkar
```

## Senaryo 1 — Kayıt → login → token → korumalı istek (US1)

1. Tarayıcı: `https://localhost:5001/connect/authorize?client_id=react-web&...&prompt=create` → kayıt ekranı → yeni hesap (customer doğar).
2. Code+PKCE değişimi → access token; `scope` claim'i customer demeti (array).
3. Scope-korumalı bir uca token'la istek → 200; token'sız → 401; admin-scope'lu uca customer token → 403.

## Senaryo 2 — Admin seed + rol→scope yönetimi (US2)

1. identityDb'yi DÜŞÜR → AppHost yeniden → açılışta admin seed loglanır (şifre LOGLANMAZ).
2. Admin login → `/Admin/Users` kullanıcılar + rol atama; `/Admin/Roles/Scopes` scope düzenleme.
3. Bir role scope ekle → o rolle YENİ login/refresh token'ında yeni scope görünür.
4. customer hesabıyla `/Admin/*` → red.

## Senaryo 3 — Katlama temizliği (US3)

```bash
grep -rn "Platform.Auth" --include='*.csproj' .   # sıfır sonuç
grep -n "github" NuGet.config                      # feed yok
unset GH_PACKAGES_TOKEN && dotnet restore          # token'sız restore geçer
```

## Senaryo 4 — Kırpılan yüzey + reset mail mock

- `POST https://localhost:5001/connect/register` → 404 (DCR yok); discovery'de `registration_endpoint` alanı yok.
- Şifremi-unuttum akışı → reset bağlantısı Identity.Server log'unda görünür (e-posta gitmez).

Beklenen toplam: SC-001..SC-005 karşılanır; AgentPlatform working tree'si temiz (`git -C ~/dev/AgentPlatform status` değişiklik göstermez).
