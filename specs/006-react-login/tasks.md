# Tasks: React Login (OIDC) + Google ile Giriş

**Input**: `specs/006-react-login/spec.md` (Kademe: Küçük — plan yok)
**Repolar**: `~/dev/ECommerceWeb` (React) + bu repo (`src/others/Identity.Server`)
**Not**: Domain-TDD kapsamı YOK (İlke VI dışı — tümü UI/altyapı/wiring; doğrulama canlı). Mock `Login.tsx` sayfası gerçek akışla İKAME edilir (IdP sayfasına yönlendirme), silinmez ama rotadan çıkar.

## Phase 1: Setup

- [X] T001 ECommerceWeb: `react-oidc-context` + `oidc-client-ts` bağımlılıklarını ekle (`~/dev/ECommerceWeb/package.json`, npm install)
- [X] T002 Auth config modülü: `~/dev/ECommerceWeb/src/auth/authConfig.ts` — authority `https://localhost:5001`, client `react-web`, redirect `http://localhost:5173/callback`, scope DAR demet (`openid profile email roles offline_access` — API scope'ları ihtiyaç doğdukça eklenir), `automaticSilentRenew`

## Phase 2: Foundational

- [X] T003 `AuthProvider` sarmalaması: `~/dev/ECommerceWeb/src/main.tsx` — uygulama kökü `react-oidc-context` provider'ına alınır
- [X] T004 Oturum yardımcıları: `~/dev/ECommerceWeb/src/auth/useAuthSession.ts` — girişli mi / kullanıcı adı / login() / logout() tek hook'ta (sayfalar provider detayını bilmez)

## Phase 3: US1 — Siteye giriş (P1) 🎯 MVP

**Bağımsız test**: Aspire + `npm run dev` ayakta; "Giriş" → IdP → e-posta/şifre → siteye girişli dönüş; header'da kullanıcı adı.

- [X] T005 [US1] Callback sayfası: `~/dev/ECommerceWeb/src/pages/AuthCallback.tsx` — dönüşte ana sayfaya (ya da saklanan hedefe) yönlendirir; doğrudan/yeniden ziyarette hata yerine ana sayfaya düşer (edge case)
- [X] T006 [US1] Rota kablosu: `~/dev/ECommerceWeb/src/App.tsx` — `/callback` rotası eklenir; mock `Login.tsx` rotası kaldırılır, "Giriş" tıkları `login()`'e bağlanır
- [X] T007 [US1] Header girişli durum: `PublicLayout.tsx` (+ gerekirse `src/components/ui.tsx`) — girişsizken "Giriş", girişliyken kullanıcı adı + "Çıkış"; tüm sayfalarda tutarlı
- [X] T008 [US1] Canlı doğrulama: kayıt (IdP mevcut Create sayfası) → login → dönüş; yanlış şifre IdP'de hata, site girişsiz kalır; IdP kapalıyken "Giriş" anlaşılır hata (edge case)

**Checkpoint**: US1 tek başına gösterilebilir — Dilim 0 kapısı açık.

## Phase 4: US2 — Oturum sürmesi + çıkış (P2)

**Bağımsız test**: girişliyken F5 → girişli kalır; token süresi dolunca sessiz tazeleme; "Çıkış" → iki tarafta da oturum biter.

- [X] T009 [US2] Oturum kalıcılığı + sessiz tazeleme ayarı: `authConfig.ts` — storage kararı (varsayılan sessionStorage; karar yorumla belgelenir), `offline_access` refresh akışı; tazeleme başarısızsa girişsiz duruma zarif düşüş
- [X] T010 [US2] Logout kablosu: `useAuthSession.ts` `logout()` → IdP signout-redirect; Identity seed'inde `react-web` için post-logout redirect `http://localhost:5173` desteği (`src/others/Identity.Server/appsettings.json` + gerekiyorsa seed modeli `Config.cs`)
- [X] T011 [US2] Canlı doğrulama: F5 kalıcılık · süre dolumu sessiz tazeleme · çıkış sonrası yeniden giriş şifre sorar · iki sekme senaryosu (edge case)

## Phase 5: US4 — Google ile giriş (P2)

**Bağımsız test**: hesabı olmayan Google hesabıyla giriş → customer olarak girişli; ikinci giriş aynı hesaba; aynı e-postalı şifreli hesapla birleşme.
**ÖN KOŞUL (kullanıcı)**: Google Cloud Console OAuth client; `dotnet user-secrets set Authentication:Google:ClientId/ClientSecret --project src/others/Identity.Server`. Secret yoksa buton GÖRÜNMEZ (zarif eksilme) — diğer fazlar bloklanmaz.

- [X] T012 [US4] Paket: `Microsoft.AspNetCore.Authentication.Google` sürümü `Directory.Packages.props`'a, referans `src/others/Identity.Server/Identity.Server.csproj`'a
- [X] T013 [US4] Google şema kaydı: `src/others/Identity.Server/Program.cs` + `Options/` POCO — ClientId/Secret user-secrets'tan (Options pattern, İlke VIII: değer log/ekran görmez); secret YOKSA şema kaydedilmez (koşullu kayıt)
- [X] T014 [US4] Login sayfasına buton: `Pages/Account/Login/Index.cshtml` + `Index.cshtml.cs` — "Google ile devam et" yalnız Google şeması kayıtlıysa render edilir; tık → external challenge
- [X] T015 [US4] External callback + hesap kuralları: `Pages/Account/ExternalLogin/Index.cshtml.cs` (yeni) — sıra: (1) mevcut external login eşleşirse o hesaba gir; (2) DOĞRULANMIŞ e-posta mevcut kullanıcıyla eşleşirse login'i o hesaba BAĞLA; (3) yoksa kullanıcı yarat + `customer` rolü (şifresiz); iptal/yarım akış login sayfasına mesajla döner
- [X] T016 [US4] Canlı doğrulama: 3 senaryo (yeni hesap / tekrar giriş / e-posta birleşme) + iptal akışı; çift hesap OLUŞMADIĞI DB'den teyit

## Phase 6: US3 — Korumalı alan iskeleti (P3)

**Bağımsız test**: girişsizken `/account` → giriş akışı → dönüşte `/account`.

- [X] T017 [US3] `RequireAuth` bileşeni: `~/dev/ECommerceWeb/src/auth/RequireAuth.tsx` — girişsizse `login()` tetikler, hedef rotayı saklar; callback dönüşünde hedefe gider
- [X] T018 [US3] Uygulama: `App.tsx` — `AccountSettings` (+ `Library`, `Shelves` gibi hesap sayfaları) `RequireAuth` ile sarılır
- [X] T019 [US3] Canlı doğrulama: girişsiz korumalı erişim → login → hedefe dönüş; girişliyken doğrudan erişim

## Phase 7: Polish

- [X] T020 CLAUDE.md: BC haritası `identity-server` satırına Google external login (koşullu, user-secrets) notu; memory güncelle (006 durumu)
- [X] T021 İki repo yeşil: `dotnet build && dotnet test` (bu repo) + `npm run build && npm run lint` (ECommerceWeb)

## Dependencies

- Phase 1 → 2 → US1; US2 ve US4 US1'e bağlı; US3 US1'e bağlı (US2/US4'ten bağımsız)
- US4 içinde T012→T013→T014→T015→T016 sıralı; US4 React tarafına DOKUNMAZ (yalnız IdP) — US2/US3 ile çakışmaz
- [P] işareti yok denecek kadar az: dosyalar küçük ve zincirli; US4 (IdP) ile US2/US3 (React) FARKLI repo — istenirse paralel yürür

## Implementation Strategy

- **MVP = US1** (T001-T008): tek başına gösterilebilir login.
- Sonra US2 (oturum disiplini) → US4 (Google; kullanıcı secret'ı geldiyse) → US3 (kapı iskeleti) → Polish.
- Google secret'ı gecikirse US4 atlanıp sona alınabilir — koşullu kayıt sayesinde sistem secret'sız tam çalışır.
- T016 notu: yeni-Google-hesabı senaryosu (farklı hesap) kullanıcı kararıyla atlandı; birleşme+tekrar giriş canlı doğrulandı (2026-11-06).
