# Feature Specification: Kimlik Makamı İçeri Dönüyor (Identity.Server kopyası)

**Feature Branch**: `005-identity-inward`

**Created**: 2026-11-05

**Status**: Draft

**Input**: User description: "AgentPlatform Identity.Server'ının KOPYASI src/others/Identity.Server olarak EC çözümüne gelir (AgentPlatform'a dokunulmaz). OpenIddict+ASP.NET Identity taşınır; rol→scope yönetimi + /Admin/* sayfaları dahil. AppRegistry tek-app'e kırpılır; DCR/consent/dış-agent istemcileri ve sosyal login gelmez. React public client (code+PKCE) seed; kayıt her zaman customer; admin seed config/secret'tan. Platform.Auth Common'a katlanır, yerel feed kalkar. ADR + CLAUDE.md revizyonu işin parçası."

> Bağlam: Kâşif kararı 1 (2026-11-04, vault todos/05) — SSO yok, dış IdP bağımlılığı kalkar,
> tek-repo odak. CLAUDE.md Yapma-listesindeki "kimliği ECommerce'e geri gömme (084)" maddesi
> BİLİNÇLİ tersine dönüyor; gerekçe ADR'ye yazılır. AgentPlatform repo'suna HİÇ dokunulmaz
> (PaymentGateway oradaki IdP'yle yaşamaya devam eder).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Müşteri yerel kimlikle kayıt olur ve giriş yapar (Priority: P1)

Ziyaretçi e-posta+şifreyle kayıt olur (her kayıt customer rolü doğurur), tek giriş ekranından
login olur; aldığı token'la EC'nin scope-korumalı yüzeylerine erişir. Dış repo/dış IdP ayakta
olmadan uçtan uca çalışır.

**Why this priority**: Kimlik her kişisel özelliğin (sepet, kitaplık, alarm) ön şartı; yerel
makam çalışmadan Dilim 2+ hiçbir ekran canlanamaz.

**Independent Test**: AgentPlatform kapalıyken EC AppHost kalkar; kayıt → login → token →
scope-korumalı bir uca 200, token'sız 401.

**Acceptance Scenarios**:

1. **Given** çalışan EC sistemi (AgentPlatform YOK), **When** yeni e-posta+şifreyle kayıt, **Then** hesap doğar ve rolü customer'dır (ekrandan admin doğmaz).
2. **Given** kayıtlı kullanıcı, **When** tek giriş ekranından login, **Then** OIDC code+PKCE akışıyla token alır; token'daki scope'lar rolünden türemiştir.
3. **Given** geçerli customer token'ı, **When** admin scope'u isteyen uca istek, **Then** 403.

---

### User Story 2 - Admin seed'le doğar, rol→scope'u içeriden yönetir (Priority: P2)

Admin hesabı açılışta config/secret'tan seed'lenir (DB reset'e dayanıklı; ekrandan admin
doğmaz). Admin, çözüm-içi server-rendered yönetim sayfalarından rol→scope eşlemesini görür
ve değiştirir.

**Why this priority**: Yönetim yüzeyi olmadan scope düzeni işletilmez; ama müşteri akışı (US1)
olmadan da değer üretmez.

**Independent Test**: DB sıfırlanır → açılışta admin yoksa seed oluşur → admin login →
/Admin sayfalarında rol→scope map'i düzenlenir → değişiklik yeni token'lara yansır.

**Acceptance Scenarios**:

1. **Given** boş identityDb, **When** sistem açılır, **Then** config/secret'taki admin hesabı oluşur; ikinci açılış kopya yaratmaz (idempotent).
2. **Given** admin login'i, **When** bir role scope eklenir/çıkarılır, **Then** o rolün sonraki token'ları yeni scope demetini taşır.
3. **Given** customer token'ı, **When** /Admin sayfalarına istek, **Then** erişim reddedilir.

---

### User Story 3 - EC servisleri yerel issuer'a döner, Platform.Auth Common'a katlanır (Priority: P3)

Geliştirici gözüyle: tüm EC servisleri token'ı çözüm-içi Identity.Server'dan doğrular
(IdentityOption.Address yerel adres); auth kablosu Platform.Auth NuGet'inden çıkıp Common'a
taşınır, yerel NuGet feed'i ve bayat-cache tuzağı tarihe karışır.

**Why this priority**: Temizlik/bağımsızlık bacağı — US1/US2 yerel issuer'la zaten çalışır;
paket katlaması ayrı doğrulanabilir son adım.

**Independent Test**: Çözümde Platform.Auth referansı ve NuGet.config yerel feed girdisi
KALMAZ; `dotnet build && dotnet test` yeşil; scope-korumalı uçlar aynen çalışır.

**Acceptance Scenarios**:

1. **Given** katlama sonrası çözüm, **When** tüm projeler derlenir, **Then** Platform.Auth paket referansı sıfırdır ve auth kablosu Common'dan gelir (namespace değişmez).
2. **Given** çalışan sistem, **When** herhangi bir servise yerel-issuer token'ıyla istek, **Then** doğrulama çözüm-içi issuer'la yapılır (dış URL config'te kalmaz).

---

### Edge Cases

- Şifre sıfırlama: 3 adımlı akış (e-posta → bağlantı → yeni şifre) çalışır ama e-posta İLK
  dilimde gerçek gönderilmez — log/mock'a düşer (mail altyapı kararı vault 03'te açık).
- Aynı e-postayla ikinci kayıt denemesi reddedilir (nazik hata).
- Admin seed secret'ı eksikse: sistem açılır ama admin doğmaz; uyarı loglanır (crash yok).
- Token issuer/HTTPS uyumsuzluğu: Identity HTTPS zorunlu (bilinen tuzak — SameSite=None);
  servislerdeki Address issuer'la birebir eşleşmek zorunda.
- AgentPlatform'dan kopyalanan kodda çok-app izleri (AppRegistry kayıtları, DCR uçları,
  consent ekranları): tek-app'e kırpılır; kırpılan yüzeye gelen istek 404 olur.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Kimlik servisi EC çözümünün PARÇASI olur (`src/others/Identity.Server`); kaynak = AgentPlatform Identity.Server KOPYASI. AgentPlatform repo'sunda hiçbir değişiklik yapılmaz.
- **FR-002**: OpenIddict+ASP.NET Identity makinesi korunur: OIDC authorization code + PKCE; React için public client seed'lenir. DCR, consent akışı, dış-agent istemcileri, sosyal login GELMEZ.
- **FR-003**: Kayıt her zaman customer rolü doğurur; ekrandan/API'dan admin doğamaz. Tek giriş ekranı (ayrı admin login yok).
- **FR-004**: Admin hesabı açılışta config/secret'tan idempotent seed'lenir (yoksa oluştur); DB reset sonrası kendini yeniden kurar. Secret değeri log/ekran/yanıtta görünmez (İlke VIII).
- **FR-005**: Rol→scope eşlemesi DB'de yaşar ve çözüm-içi server-rendered /Admin sayfalarından yönetilir (cookie auth — Anayasa İlke V istisnası aynen taşınır); sayfalar admin-dışına kapalı.
- **FR-006**: `[RequiredScope]` + `AuthorizationScopes` düzeni DEĞİŞMEZ (İlke V yaşar); yalnız token basan makam çözüm-içine döner — tüm servislerin issuer adresi yerel Identity.Server olur.
- **FR-007**: Platform.Auth NuGet'i ölür: kablo kodu Common'a katlanır (namespace `Common.*` korunur), yerel feed kaydı NuGet.config'ten kalkar, hiçbir projede paket referansı kalmaz.
- **FR-008**: Identity.Server Aspire AppHost'a kaydolur: kendi Postgres DB'si (identityDb) + HTTPS zorunlu; ona bağımlı servisler issuer hazır olmadan istek atarsa davranış bugünkü dış-IdP düzeniyle aynı kalır.
- **FR-009**: Şifre sıfırlama akışı kurulur; e-posta gönderimi bu dilimde log/mock (gerçek SMTP sonraki karar). Reset token'ları tek kullanımlık ve süreli.
- **FR-010**: ADR yazılır ("kimlik makamı geri içeri — 084 neden tersine döndü") + CLAUDE.md revize edilir: Yapma-listesi maddesi, BC haritası identity-server satırı, Teknoloji bölümü.
- **FR-011**: Çok-uygulama AppRegistry tek-app düzenine iner: yalnız EC istemcileri seed'lenir; başka app kaydı/config yüzeyi kalmaz.

### Key Entities

- **Kullanıcı hesabı**: e-posta (tekil) + şifre + rol(ler); kayıtla customer doğar.
- **Rol→scope eşlemesi**: DB'de; admin yüzeyinden düzenlenir; token içeriğini belirler.
- **OIDC istemcisi (seed)**: React public client (code+PKCE); admin sayfaları cookie ile (istemci değil).
- **Admin seed kaydı**: config/secret kaynaklı; idempotent oluşturma.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: EC sistemi AgentPlatform repo'su/IdP'si hiç ayakta olmadan uçtan uca kalkar; kayıt→giriş→korumalı istek akışı tek oturuşta tamamlanır.
- **SC-002**: DB tam sıfırlama sonrası tek açılışta admin yeniden kullanılabilir (elle müdahale sıfır).
- **SC-003**: Çözümde Platform.Auth referansı ve yerel NuGet feed girdisi sıfır; build+tüm testler yeşil.
- **SC-004**: AgentPlatform repo'sunda sıfır commit/sıfır dosya değişikliği.
- **SC-005**: Rol→scope değişikliği admin ekranından yapılıp yeni token'da gözlenir (yeniden açılış gerekmez; mevcut token'ların eski scope taşıması kabul).

## Assumptions

- Mail gönderimi bu dilimde kurulmaz: reset e-postası log/mock (vault 03'teki mail kararı açık kalır).
- React istemcisi henüz yok (Dilim 0 sonrası): PKCE istemcisi seed'lenir, akış dev araçlarıyla (tarayıcı/cURL) doğrulanır.
- PaymentGateway etkilenmez: AgentPlatform IdP'si onun için yaşamaya devam eder; iki kimlik makamı bağımsız evrilir.
- Mevcut EC kullanıcı verisi yok sayılır (dış IdP'deki hesaplar taşınmaz — dev ortamı, kayıp kabul).
- Identity.Server BC haritasında "destek/altyapı" satırıdır (iş alanı BC'si değil); FLOW.md zorunluluğu domain süreci taşıyan BC'lere aittir — kimlik için kısa süreç notu yeterli (plan kararı).
