# Feature Specification: React Login (OIDC) + Google ile Giriş

**Feature Branch**: `006-react-login`

**Created**: 2026-11-06

**Status**: Draft

**Input**: User description: "React login dilimi: ECommerceWeb React uygulaması Identity.Server'a OIDC authorization code + PKCE ile bağlanır (seed'li react-web istemcisi) — giriş butonu, callback, token saklama/yenileme, logout, korumalı route iskeleti. Identity login sayfasına 'Google ile devam et' eklenir — TAM İŞLEVLİ Google ile giriş (kullanıcı son kararı, 2026-11-06). Kayıt mevcut Identity sayfalarıyla. Dilim 0'ın ilk gerçek dilimi."

**Kademe**: Küçük — yeni aggregate/tablo/event/endpoint kontratı yok (mevcut OIDC uçları + seed'li istemci; Google dış-sağlayıcı bağlantısı ASP.NET Identity'nin mevcut external-login şemasını kullanır, domain'e dokunmaz). Yalnız `spec.md` + `tasks.md`.

**Kapsam notu**: İş iki repo'ya yayılır — `~/dev/ECommerceWeb` (React istemci) + bu repo (`src/others/Identity.Server` login sayfası). Spec/tasks bu repoda yaşar.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Siteye giriş yapma (Priority: P1)

Ziyaretçi React sitesinde "Giriş" butonuna basar; kimlik sayfasına yönlendirilir, e-posta/şifresiyle giriş yapar, siteye geri döner ve artık adıyla karşılanır. Henüz hesabı yoksa aynı yüzeydeki kayıt bağlantısından hesap açar (kayıt ekranları mevcut kimlik sayfalarıdır, yeni ekran yok).

**Why this priority**: Login olmadan hiçbir kullanıcıya-bağlı özellik (sepet, kitaplık, kişiselleştirme) açılamaz — Dilim 0'ın kapısı.

**Independent Test**: Sistem ayakta iken React sitesinden "Giriş" → kimlik sayfası → giriş → siteye dönüş; ekranda kullanıcının girişli olduğu görülür.

**Acceptance Scenarios**:

1. **Given** girişsiz ziyaretçi, **When** "Giriş"e basar ve kimlik sayfasında doğru e-posta/şifre verir, **Then** siteye geri döner ve girişli durumda görünür (kullanıcı adı/çıkış görünür).
2. **Given** girişsiz ziyaretçi, **When** kimlik sayfasında yanlış şifre girer, **Then** kimlik sayfasında hata görür, site girişsiz kalır.
3. **Given** kimlik sayfasına gelmiş ziyaretçi, **When** kayıt bağlantısıyla hesap açar, **Then** customer olarak giriş yapıp siteye dönebilir.

---

### User Story 2 - Oturumun sürmesi ve çıkış (Priority: P2)

Girişli kullanıcı sayfayı yeniler ya da ertesi saat geri gelir — yeniden giriş istenmez; oturum sessizce tazelenir. "Çıkış" dediğinde hem sitede hem kimlik tarafında oturum kapanır.

**Why this priority**: Her sayfa yenilemede login isteyen site kullanılamaz; çıkışsız oturum güvenlik açığıdır.

**Acceptance Scenarios**:

1. **Given** girişli kullanıcı, **When** sayfayı yeniler, **Then** girişli kalır (yeniden kimlik sayfası görmez).
2. **Given** girişli kullanıcı, **When** erişim süresi dolar, **Then** oturum kullanıcıya hissettirmeden tazelenir; tazeleme de başarısızsa kullanıcı girişsiz duruma düşer ve tekrar giriş yapabilir.
3. **Given** girişli kullanıcı, **When** "Çıkış"a basar, **Then** site girişsiz görünür ve yeniden "Giriş" denediğinde şifre tekrar sorulur (kimlik oturumu da kapanmıştır).

---

### User Story 3 - Korumalı alan iskeleti (Priority: P3)

Girişsiz ziyaretçi, giriş gerektiren bir sayfaya (ör. "Hesabım") gitmeye çalışır; giriş akışına yönlendirilir ve giriş sonrası gitmek istediği sayfaya döner.

**Why this priority**: Sonraki dilimlerin (sepet, kitaplık) kapı mekanizması — bir kez kurulur, hepsi kullanır.

**Acceptance Scenarios**:

1. **Given** girişsiz ziyaretçi, **When** korumalı sayfaya gider, **Then** giriş akışına yönlendirilir ve girişten sonra o sayfaya döner.
2. **Given** girişli kullanıcı, **When** korumalı sayfaya gider, **Then** doğrudan görür.

---

### User Story 4 - Google ile giriş (Priority: P2)

Ziyaretçi kimlik giriş sayfasında "Google ile devam et"e basar; Google hesabını seçer/onaylar; siteye girişli döner. İlk Google girişinde hesap otomatik açılır (customer); aynı e-postayla şifreli hesap zaten varsa iki giriş yolu aynı hesapta birleşir.

**Why this priority**: Kayıt sürtünmesini ciddi düşürür; ama P1 (temel login) olmadan test bile edilemez — onun üstüne gelir.

**Independent Test**: Hiç hesabı olmayan bir Google hesabıyla giriş → sitede girişli + customer olarak; ikinci girişte aynı hesaba döner.

**Acceptance Scenarios**:

1. **Given** sistemde hesabı olmayan ziyaretçi, **When** "Google ile devam et" ile Google hesabını onaylar, **Then** customer rolüyle hesap açılır ve siteye girişli döner (şifre belirlemesi istenmez).
2. **Given** Google ile daha önce girmiş kullanıcı, **When** tekrar Google ile girer, **Then** aynı hesaba girişli döner (ikinci hesap AÇILMAZ).
3. **Given** aynı e-postayla şifreli hesabı olan kullanıcı, **When** Google ile girer, **Then** mevcut hesabına bağlanır (çift hesap oluşmaz) ve sonrasında iki yöntemle de girebilir.
4. **Given** Google onay ekranında vazgeçen ziyaretçi, **When** iptal edip döner, **Then** kimlik giriş sayfasına anlaşılır mesajla düşer, girişsiz kalır.

---

### Edge Cases

- Kullanıcı kimlik sayfasından giriş yapmadan tarayıcı "geri" ile siteye dönerse: site girişsiz, tutarlı durumda kalır (yarım akış hata bırakmaz).
- Giriş dönüşü (callback) sayfası doğrudan/yeniden ziyaret edilirse: hata yerine ana sayfaya düşer.
- Kimlik sunucusu kapalıyken "Giriş" denenirse: site çökmez, anlaşılır hata gösterilir.
- Aynı anda iki sekmede oturum: birinde çıkış yapılınca diğeri en geç bir sonraki tazelemede girişsiz duruma düşer (anlık senkron beklenmez).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: React sitesi girişsiz gezinmeye tam izin verir (anonim vitrin — İlke V); giriş yalnız kullanıcıya bağlı alanlar için istenir.
- **FR-002**: "Giriş" akışı kullanıcıyı merkezi kimlik sayfasına götürür ve başarılı girişte siteye geri getirir; şifre React uygulamasına asla girmez (yönlendirme tabanlı akış).
- **FR-003**: Site, girişli kullanıcının kimliğini (ad/e-posta) gösterebilir ve oturum durumunu tüm sayfalarda tutarlı yansıtır.
- **FR-004**: Oturum sayfa yenilemeye dayanır ve süresi dolduğunda kullanıcıya hissettirmeden tazelenir; tazeleme başarısızsa girişsiz duruma zarifçe düşülür.
- **FR-005**: "Çıkış" hem site oturumunu hem merkezi kimlik oturumunu sonlandırır.
- **FR-006**: Korumalı sayfa mekanizması: girişsiz erişim giriş akışına yönlenir, giriş sonrası hedef sayfaya dönülür.
- **FR-007**: Kimlik giriş sayfasında "Google ile devam et" seçeneği sunulur; Google'dan dönen doğrulanmış kimlikle kullanıcı girişli olur. Google'ın reddettiği/yarım kalan akış mevcut giriş sayfasına zarifçe döner.
- **FR-008**: Kayıt, mevcut kimlik sayfalarıyla yapılır; yeni kayıt ekranı yazılmaz; kayıt olan customer rolüyle doğrudan giriş yapabilir (mevcut davranış korunur).
- **FR-009**: İlk Google girişi otomatik hesap açar (customer rolü, sunucu atar); aynı doğrulanmış e-postayla mevcut hesap varsa Google girişi o hesaba bağlanır — hiçbir durumda aynı e-postayla ikinci hesap oluşmaz.
- **FR-010**: Google bağlantı sırları (istemci kimliği/sırrı) config dosyalarına/git'e girmez; güvenli parametre mekanizmasıyla taşınır ve hiçbir ekranda/logda görünmez (İlke VIII).

### Key Entities

Yeni veri/entity yok — mevcut kimlik hesapları ve seed'li `react-web` istemcisi kullanılır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ziyaretçi, kayıt dahil ilk girişini 2 dakikadan kısa sürede tamamlayabilir; mevcut hesapla giriş 30 saniyeden kısadır.
- **SC-002**: Girişli kullanıcı sayfa yenileme ve oturum tazeleme sırasında %100 oranında girişli kalır (kimlik sunucusu ayaktayken); yeniden şifre sorulmaz.
- **SC-003**: Çıkış sonrası korumalı alana erişim denemelerinin %100'ü giriş akışına yönlenir.
- **SC-004**: Hiç hesabı olmayan kullanıcı Google ile 1 dakikadan kısa sürede girişli hale gelir (form doldurmadan); aynı e-postayla asla ikinci hesap oluşmaz.

## Assumptions

- Identity.Server (005) Aspire'da ayakta ve `react-web` istemcisi seed'li; issuer/redirect ayarları React sitesinin adresiyle uyumlu hale getirilecek (gerekirse seed istemcinin redirect listesi güncellenir — yeni istemci açılmaz).
- ECommerceWeb repo'sundaki 25 ekran mock'u zemin alınır; bu dilim yalnız login/oturum katmanını gerçek yapar, diğer ekranlar mock kalır.
- Token'lar tarayıcıda güvenli varsayılan pratiklerle saklanır; sır/credential hiçbir log'a veya ekrana yazılmaz (İlke VIII).
- Kullanıcı ekran testleri 005'ten devralınan açık kalemlerle (T013/T016/T024) birlikte bu dilimin canlı doğrulamasında koşulur.
- API'lere token'la istek atma (scope'lu çağrılar) bu dilimde yalnız iskelet düzeyinde; gerçek korumalı API tüketimi sonraki dilimlerde.
- **Kullanıcı ön koşulu:** Google Cloud Console'da OAuth istemcisi (ClientId+Secret) kullanıcı tarafından açılır; yönlendirme adresi kimlik sunucusunun adresini gösterir. Secret user-secrets ile verilir — yoksa Google butonu giriş sayfasında GÖRÜNMEZ (sistem onsuz tam çalışır, zarif eksilme).
- E-posta eşleştirmeli hesap birleştirme yalnız Google'ın DOĞRULANMIŞ e-postasıyla yapılır (doğrulanmamış e-postayla birleştirme = hesap ele geçirme riski; endüstri standardı).
