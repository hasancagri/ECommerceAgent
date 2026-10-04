# Implementation Plan: Platform IdP Terfisi (Dilim A)

**Branch**: `084-platform-idp` | **Date**: 2026-09-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/084-platform-idp/spec.md`

## Summary

`Identity.Server`'ı ECommerce'ten çıkar → **yeni `AgentPlatform` repo'suna taşı** (fiziksel split A'da);
uygulama-nötr **platform kimlik makamı** yap. İki eksen:
1. **Nötrleme** — ECommerce-sabit scope/client/role kaydını **uygulama-kayıt sözleşmesine** (config güdümlü) taşı; ECommerce "app #1" olarak o sözleşmeyle kayıtlanır; ikinci uygulama (`pg`) yalnız config'le tanıtılır, ad-uzayı çakışması reddedilir.
2. **Fiziksel split** — `AgentPlatform` repo'su kur, IdP'yi taşı, `Common`'ın nötr kısmını **paylaşılan NuGet paketine** ayır (ECommerce + AgentPlatform ikisi de tüketir), Aspire AppHost'u proje-ref yerine dış-servis referansına çevir.

Kod domain'e bağımsız (csproj temiz) → asıl sürtünme domain değil, **Common bağımlılığı + AppHost kablosu**.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: OpenIddict 7.x (server + EF stores), ASP.NET Core Identity, EF Core (identityDb Postgres), Razor Pages (admin UI)
**Storage**: Postgres `identityDb` (Identity user/role + RoleScope junction + OpenIddict tabloları) — AgentPlatform'a taşınır, şema değişmez
**Testing**: xUnit + Shouldly
**Target Platform**: Linux/container; kendi Aspire AppHost'u (AgentPlatform)
**Project Type**: Bağımsız platform servisi (yeni repo)
**Constraints**: HTTPS zorunlu; yeni loopback-only sert bağımlılık YOK; Options pattern; CPM (paket sürümü props'ta)
**Scale/Scope**: 1 servis taşıma + Common paket ayrımı + 2 repo AppHost kablosu; canlı kullanıcı/prod verisi YOK

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar.*

- **İLKE I (BC izolasyonu):** IdP destek servisi, domain BC değil; kendi `identityDb`'si taşınır, başka BC DB'sine dokunmaz. Nötrleme başka BC bağı EKLEMEZ. ✅
- **İLKE III/II (VSA/aggregate):** IdP OAuth/Identity altyapısı; Domains/aggregate/Features deseni domain BC'lere ait, destek servisi muaf. Yeni aggregate/event yok. ✅
- **İLKE V (scope yetki):** Dilimin kalbi — rol=scope demeti, downstream scope görür (rol değil) KORUNUR; `KnownScopes` tek-kaynak kalır (artık app-registry üzerinden). ✅
- **Config = Options:** yeni app-kayıt config'i Options ile bağlanır (`AddOptions<T>().BindConfiguration().ValidateOnStart()`); `IConfiguration` doğrudan okuma yok. Mevcut `Program.cs` `ApiKeyAuth:Authority` okuması = service-discovery/bootstrap istisnası, genişletilmez. ✅
- **Paket/CPM:** yeni paylaşılan paketin sürümü `Directory.Packages.props`'ta pinlenir; `.csproj` sürümsüz ref. ✅
- **İLKE VII (FLOW.md):** IdP'nin domain süreci yok; tetik domain-süreç değişimi — bu dilim tetiklemez. ✅

**Sonuç:** İhlal yok. Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/084-platform-idp/
├── plan.md
├── research.md          # app-registry, scope ad-uzayı, Common paket ayrımı, AppHost dış-ref, issuer/loopback
├── data-model.md        # RegisteredApp + ScopeNamespace + Role/RoleScope (mevcut)
├── quickstart.md        # AgentPlatform standalone boot + login + ikinci-app config doğrulaması
├── contracts/
│   └── app-registration.md   # uygulama-kayıt config sözleşmesi
└── tasks.md             # /speckit-tasks (bu komutta DEĞİL)
```

### Source Code (iki repo)

```text
# YENİ repo: AgentPlatform/
AgentPlatform/
├── src/Identity.Server/            # ← src/others/Identity.Server buradan taşınır
│   ├── Config.cs                   # ⟳ ECommerce-sabit scope/audience → AppRegistry'den
│   ├── Rbac/KnownScopes.cs         # ⟳ tek-kaynak = kayıtlı app ad-uzaylarının birleşimi
│   ├── Connect/SeedHostedService.cs# ⟳ seed döngüsü app başına registry üzerinden
│   └── Options/AppRegistryOptions.cs # ✚ kayıtlı uygulamalar + scope ad-uzayı + client demetleri (Options)
├── src/AppHost/                    # ✚ AgentPlatform'un kendi Aspire host'u (idp + identityDb)
└── (fasat Dilim C'de buraya eklenir)

# Paylaşılan paket (yeni)
Platform.Auth (NuGet)               # ← Common'ın NÖTR kısmı: IdentityOption + AuthenticationExtension;
                                    #   ECommerce + AgentPlatform tüketir. (ScopeClaimArrayHandler DEĞİL —
                                    #   sunucu-handler'ı IdP'de kalır, AgentPlatform'a taşınır)

# Mevcut repo: ECommerceAgent/
src/others/Common/
└── Utils/Constants/AuthorizationScopes.cs  # ⟳ KALIR — ECommerce'in KENDİ scope ad-uzayı (app #1)
src/aspire/AppHost/AppHost.cs               # ⟳ identity-server proje-ref → dış-servis ref (AgentPlatform URL)
```

**Structure Decision:** Fiziksel split A'da (kullanıcı kararı). `AgentPlatform` yeni git repo + kendi
AppHost'u. `Common` ikiye ayrılır: **nötr auth kablosu → `Platform.Auth` paketi** (iki repo tüketir),
**ECommerce-özel scope sabitleri → ECommerce'te kalır**. ECommerce AppHost IdP'ye artık dış-servis
(URL/service-discovery) olarak bakar. Fasat (Dilim C) sonra aynı `AgentPlatform` repo'suna eklenir.

## Complexity Tracking

İhlal yok — boş.