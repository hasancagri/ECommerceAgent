# Tasks: Kimlik Makamı İçeri Dönüyor (005)

**Input**: specs/005-identity-inward/ (spec, plan, research R1-R10, data-model, contracts, quickstart)

**NOT — test task'ı YOK (kullanıcı kararı):** kopya işi, saf domain yazımı yok; `tests/Identity.Server.Tests` açılmaz. **Canlı doğrulamayı KULLANICI bizzat ekranlardan yapar** (Identity'nin server-rendered login/kayıt//Admin sayfaları — React gerekmez); implement ajanı yalnız build + açılış sağlığına bakar, quickstart senaryolarını koşmaz. React ekran tasarımı SONRAKİ aşama (Dilim 0).

## Phase 1: Setup (kopya + kırpma)

- [x] T001 AgentPlatform `src/Identity.Server` → `src/others/Identity.Server` kopyala (bin/obj hariç); AgentPlatform working tree'sine DOKUNMA (SC-004)
- [x] T002 Kırp (R2): `Connect/RegisterEndpoint.cs` + `DcrRequestValidator` + `RegistrationEndpointMetadataHandler` + `Pages/Account/Consent/*` + `ApiKeys/*` uçları sil; `Program.cs`'ten DCR/consent/apikeys kayıtlarını çıkar (`src/others/Identity.Server/Program.cs`)
- [x] T003 Tek-app appsettings (R4/R5): yalnız `ecommerce` app'i — EC scope'ları + roller (customer/admin; super-admin YOK) + istemciler (react-web PKCE, order-saga, payment-s2s); yabancı app/Claude redirect/DcrTemplate izleri temizle (`src/others/Identity.Server/appsettings.json`)
- [x] T004 [P] CPM: OpenIddict/EFCore/Npgsql/Identity sürümlerini `Directory.Packages.props`'a ekle; `Identity.Server.csproj` sürümsüz PackageReference'a çevir
- [x] T005 [P] `ECommerceAgent.slnx`'e `src/others/Identity.Server` kaydı
- [x] T006 [P] `Properties/launchSettings.json` doğrula/kur: `https://localhost:5001` (R7; launchSettings'siz proje Production açılır — bilinen tuzak)

## Phase 2: Foundational (Aspire kablosu + derleme)

- [x] T007 AppHost: postgres'e `identityDb` + `AddProject<Projects.Identity_Server>("identity-server").WithReference(identityDb).WaitFor(identityDb)`; eski "IdP dış repoda" yorumunu güncelle (`src/aspire/AppHost/AppHost.cs`)
- [x] T008 AppHost secret parametreleri (R9): `identity-admin-email` + `identity-admin-password` → Identity.Server `BootstrapAdmin__*` env; değer log/ekrana yazılmaz (İlke VIII) (`src/aspire/AppHost/AppHost.cs`)
- [x] T009 `dotnet build` yeşil: kırpma artıkları (silinen tiplere referans, ölü using) temizlenene dek düzelt

## Phase 3: US1 — Müşteri yerel kimlikle kayıt/giriş (P1) 🎯 MVP

- [x] T010 [US1] Kayıt yolu doğrula/sabitle: `Pages/Account/Create` HER kayıtta customer rolü atar; ekrandan/API'dan admin doğma yolu olmadığını kod üstünde doğrula (`src/others/Identity.Server/Pages/Account/Create/Index.cshtml.cs`)
- [x] T011 [US1] Seed akışı: `SeedHostedService` scope+istemci+rol seed'i tek-app config'iyle sorunsuz; react-web PKCE istemcisi doğru izinlerle doğuyor (`src/others/Identity.Server/Connect/SeedHostedService.cs`)
- [x] T012 [US1] Şifre sıfırlama mail mock'u (R10): reset bağlantısı ILogger'a; gerçek gönderim YOK (`src/others/Identity.Server/Pages/Account/` ilgili sayfa)
- [ ] T013 [US1] **KULLANICI** — Canlı: quickstart Senaryo 1 (ekranlardan kayıt→login; token zinciri)

## Phase 4: US2 — Admin seed + rol→scope yönetimi (P2)

- [x] T014 [US2] BootstrapAdmin options: secret'tan e-posta+şifre; idempotent yoksa-oluştur; secret eksikse uyarı logla, crash etme (`src/others/Identity.Server/Options/` + `Connect/SeedHostedService.cs`)
- [x] T015 [US2] `/Admin/*` sayfaları tek-app dünyada çalışır: Users/Roles/Scopes; KnownScopes kapalı listesi EC scope'larını gösterir; son-admin kilidi yerinde (`src/others/Identity.Server/Pages/Admin/`)
- [ ] T016 [US2] **KULLANICI** — Canlı: quickstart Senaryo 2 (/Admin ekranlarından rol→scope; DB reset→admin seed)

## Phase 5: US3 — Yerel issuer + Platform.Auth katlaması (P3)

- [x] T017 [US3] `IdentityOption` + `AddAuthenticationAndAuthorizationExtension` + global using'leri Common'a taşı; namespace `Common.*` hizası (`src/others/Common/Options/IdentityOption.cs`, `src/others/Common/Extensions/AuthenticationExtension.cs`)
- [x] T018 [US3] Paket temizliği: `Common.csproj`'dan Platform.Auth referansı + `Directory.Packages.props`'tan PackageVersion satırı sil
- [x] T019 [US3] `NuGet.config`: `github` feed'i + credential bloğu kalkar (nuget.org kalır); `GH_PACKAGES_TOKEN`'sız `dotnet restore` geçer
- [x] T020 [US3] Tüm çözüm build + mevcut testler yeşil; `grep -rn "Platform.Auth" --include='*.csproj'` sıfır sonuç (quickstart Senaryo 3)

## Phase 6: Polish (belge + uçtan uca)

- [x] T021 [P] ADR yaz: `docs/adr/adr-identity-inward.md` — 084 neden tersine döndü (tek-repo odak, SSO yok, platform vizyonu askıda) + EF/Razor istisnası (R8) + `docs/adr/README.md`'ye satır
- [x] T022 [P] CLAUDE.md revizyonu (FR-010): Yapma-listesi "kimliği geri gömme" maddesi güncelle (karar tersine döndü, ADR link) · BC haritası identity-server satırı "çözüm içi `src/others/Identity.Server`" · Teknoloji bölümü (OpenIddict içeri, Platform.Auth öldü, EF Core istisnası)
- [x] T023 [P] Vault işaretleri: `~/dev/DevNotes/ECommerceAgent/todos/05-uyelik-kimlik.md` yapılanları işaretle (OpenIddict kaldı kararı, Common katlaması, log/mock mail)
- [ ] T024 **KULLANICI** — Uçtan uca canlı tur: quickstart 4 senaryo (implement ajanı yalnız Senaryo 3 grep'lerini + AgentPlatform `git status` temizliğini koşar)

## Dependencies & Execution Order

- Phase 1 → 2 sıralı (T004/T005/T006 kendi içinde [P]).
- US1 (Phase 3) MVP — Phase 2 bitince başlar.
- US2, US1'den bağımsız test edilir ama aynı seed altyapısını kullanır → US1 sonrası önerilir.
- US3 her an yapılabilir (US1/US2'yi beklemez) ama build-kırılım riskini izole etmek için ayrı faz.
- Polish en son; T021/T022/T023 paralel.

## MVP Scope

US1 (T001-T013): AgentPlatform kapalıyken kayıt→login→token→korumalı istek. US2/US3 artımlı.
