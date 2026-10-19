<!-- Sync Impact Report — v1.11.1 → v1.12.0 (2026-10-16, MINOR)
     Removed: "E2E Testing (Playwright)" bölümü → yerine "Otomatik Regression Kapsamı".
     Bölüm v1.8.0'da eklenmişti ve tarif ettiği her şey WebApp'e aitti (vitrin gezinme, sepet/
     checkout ekranları, /Admin/Roles, data-testid, web-first assertions). WebApp 066'da söküldü,
     sistem agent-only oldu; harness hiç kurulmadı (tests/E2E yok, repoda Playwright referansı
     yok). Bölüm aynı dosyadaki "Kalite kapıları" maddesiyle (host/entegrasyon harness'ı yoktur)
     ve İlke VI ile çelişiyordu. İlke değil bölüm kaldırıldı → MINOR.
     Added: İlke VIII — Sır ve Kimlik Değeri İnsan-Yüzeyde Görünmez (NON-NEGOTIABLE).
     087 + adr-mcp-control-plane-no-secret-return'de yaşayan invariant anayasaya alındı; şimdiye
     kadar yalnız CLAUDE.md "Yapma listesi"nde duruyordu, yani spec/plan üretiminde okunmuyordu.
     Modified: İlke V capability-link örneği 078 (söküldü) → 083 ImportSession (yaşayan tek örnek).
     Modified: İlke I telemetri istisnası UYKUDA işaretlendi — repoda UI/BFF yok, 042 de söküldü.
     Modified: İlke I'den WebApp örneği, İlke II'den var olmayan `Brand` aggregate'i çıkarıldı.
     Templates: plan/spec/tasks ✅ değişiklik gerekmez.
     Runtime docs: CLAUDE.md ✅ aynı turda hizalandı (yüzey/test/tool adları koda karşı doğrulandı). -->

# ECommerceAgent Constitution

Bu anayasa, projenin pazarlık edilemez mimari ilkelerini tanımlar. Her spec, plan
ve implementation bu ilkelere uymak zorundadır. Ayrıntılı, çalışma-anı (runtime)
rehberi için `CLAUDE.md`'ye bakılır; çatışma halinde bu anayasa üstündür.

## Core Principles

### I. Bounded Context İzolasyonu (NON-NEGOTIABLE)

Her mikroservis bir Bounded Context'tir ve sınırı fiziksel/serttir. Her context'in
kendi Postgres veritabanı, kendi Marten şeması ve kendi domain modeli vardır;
**ortak (paylaşılan) bir domain modeli yoktur**.

- Aynı kavram farklı context'te farklı modeldir (ör. "Ürün" Catalog'da zengin bir
  aggregate, Basket'te sade bir BasketItem entity'si, Storefront'ta bir read-model
  satırıdır). Bir context'in modeli diğerine sızdırılamaz.
- Bir servis başka bir servisin veritabanına, tablosuna, DbContext'ine veya
  aggregate'ine **doğrudan erişemez**.
- Context'ler arası iletişim **integration event'leri** (RabbitMQ fanout), **MCP** ve —
  anlık evet/hayır gerektiren senkron kararlar ile sürecin sahibi BC'de koşan orkestre
  edilmiş saga'ların adım/telafi komutları için — **tipli senkron RPC (gRPC/HTTP)**
  ile olur. Senkron RPC yalnız bilinçli bir kontrat üzerinden yapılır ve DB izolasyonunu
  bozmaz (bir servis diğerinin DB/tablo/aggregate'ine yine doğrudan erişemez). Paylaşılabilen
  tek şey `Shared.IntegrationEvents` / `Shared/Protos` gibi bilinçli sözleşmelerdir.
  Örnekler: stok rezervasyonu (012) Basket/Order→Stock gRPC (request/response zorunlu; async
  event anlık karar veremez); checkout saga adımları (028) Order→Stock/Basket gRPC (hedefli
  komut + telafi, süreç bilgisi yalnız saga'da).
- **Orkestre saga adım/telafi kanalı — iki meşru biçim (v1.11.0):** Sürecin sahibi saga, hedefli
  adım/telafi komutlarını (a) **tipli senkron RPC (gRPC/HTTP)** ile ya da (b) **hedefli asenkron
  broker komut/yanıtı** (korelasyon kimliği + idempotency anahtarı taşıyan komut → yanıt-event) ile
  yürütebilir. (b) fanout DEĞİLDİR (tek hedef, koreografi değil orkestrasyon); temporal decoupling /
  at-least-once dayanıklılık gerektiğinde ya da saga ayrı bir orchestration servisinde host
  edildiğinde seçilir. İki biçim de bilinçli sözleşme (`Shared/Protos` ya da
  `Shared.IntegrationEvents`/`Shared/*Messages`) üzerinden yapılır ve DB izolasyonunu bozmaz.
  Örnekler: 028 Order-içi saga → gRPC; 049 ayrı Checkout.Orchestrator → broker komut/yanıt
  (hedefli, korelasyonlu, idempotent). **Şu an UYKUDA (v1.12.2):** 049 Checkout.Orchestrator 092'de
  söküldü (checkout choreography'ye döndü — fanout integration event, İlke I'in birincil kanalı); repoda
  başka orkestre-saga kalmadı. Kanal KALDIRILMADI (telemetri v1.12.0 + capability-link v1.12.1 "örneksiz
  uyku" emsali) — çok-adımlı/dallanan/telafili bir süreç doğduğunda aynı koşullarla yeniden geçerli.
- **MCP'yi yalnız agent'lar tüketir** (v1.8.1): MCP tool'ları LLM tool-seçim yüzeyidir;
  agent olmayan kod (servisler, worker'lar) imperatif `CallToolAsync` ile MCP süremez.
  Yapısal (LLM'siz) servisler-arası ihtiyaç REST/gRPC sözleşmesiyle karşılanır.
- **Telemetri kanalı istisnası** (v1.9.0, 042): kayıp-toleranslı, domain-gerçeği OLMAYAN
  davranış telemetrisi, UI/BFF katmanından (bounded context DEĞİLDİR) TEK-tüketicili bir
  BC'ye **versiyonlu, şema-kontratlı log dosyasıyla** beslenebilir (kontrat feature spec'inde
  yaşar, örn. `specs/042/contracts/behavior-log-line.md`). Koşullar: üretici UI/BFF'dir
  (BC-arası kullanılamaz), tüketici TEKtir, veri kaybı kabul edilebilirdir. İkinci tüketici
  doğduğu an kanal integration event'e TERFİ ETMEK ZORUNDADIR. DB izolasyonu aynen geçerli:
  dosyayı yalnız sahibi BC okur ve kendi veritabanına indirir.
  **Şu an UYKUDA (v1.12.0):** repoda UI/BFF katmanı yok (066 WebApp söküldü) ve örnekteki 042
  Personalization BC'si de söküldü — istisna kaldırılmadı, bir UI/BFF doğduğu an yeniden geçerlidir.

### II. Zengin Aggregate, İçeride Korunan Invariant'lar

Domain modeli anemik olamaz. Her servis tek bir Bounded Context'tir; bir BC, domain'i
gerektiriyorsa **birden fazla zengin aggregate root** içerebilir (ör. Catalog: `Product`,
`Category`, `Author`, `Publisher`). Her aggregate root `AggregateRoot`'tan türer ve kendi tutarlılık
sınırıdır; dış dünya durumu yalnızca kök üzerinden değiştirir.

- Yeni bir aggregate ancak kendi kimliği, kendi invariant'ları ve kendi yaşam döngüsü
  varsa açılır; **anemik (davranışsız) aggregate yasaktır** — davranışsız kavram value
  object ya da entity olarak mevcut bir aggregate'in içinde modellenir.
- Aynı BC içindeki aggregate'ler birbirine nesne referansıyla değil **Id ile** referans
  verir (ör. `Product.BrandId`); bir aggregate diğerinin iç durumunu değiştiremez.

- Koleksiyonlar private tutulur, yalnızca okunur expose edilir (`_items` →
  `IReadOnlyList<T> Items`); mutasyon sadece davranış metotlarından geçer.
- İş kuralı/invariant handler'da değil **aggregate metodunda** korunur. Yeni kural
  eklerken önce aggregate metoduna bakılır.
- Yapı taşları doğru seçilir: kimlik+denetim gereken aggregate-dışı sınıf
  `BaseModel`'den türer; sade entity base almaz; value object'ler `record` +
  private ctor + statik `Create` fabrikasıyla yazılır; enum yerine `Enumeration`.

### III. Vertical Slice + CQRS, Repository Yok

Kod teknik katmana göre değil domain feature'ına göre düzenlenir. Bir feature = bir
`static class`; command/query `record`, `Response`, `Handler` ve endpoint-extension
kendi içine gömülüdür.

- **CQRS ayrımı zorunlu:** durumu değiştiren işlemler `Features/Commands/` altında
  (`IDocumentSession` ile yazar, handler `[Transactional]`), yalnızca veri
  döndürenler `Features/Queries/` altında. İkisi tek slice'ta birleştirilmez.
- **Repository yoktur.** Handler'lar kalıcılık için doğrudan Marten
  `IDocumentSession`'ı, başka bir slice'ı çağırmak için `IMessageBus` alır.
- Endpoint'ler Minimal API'dir; `*EndpointExtension` üzerinden map'lenir, kullanıcı
  `CurrentUser.Load(...)` ile çözülür, `.RequireAuthorization(...)` ile korunur.
- MCP tool'ları ince sarmalayıcıdır: aynı Wolverine command/query'sini `IMessageBus`
  ile yeniden çağırır, iş mantığı eklemez.

### IV. Result Pattern (exception değil)

Beklenen hatalar (bulunamadı, doğrulama, iş kuralı ihlali) exception ile değil bir
**Result nesnesiyle** taşınır. Handler'lar, aggregate metotları ve endpoint'ler her
zaman `Common.Results` altındaki bir Result tipi döner
(`FeatureResultModel`, `FeatureObjectResultModel<T>`, `FeatureListResultModel<T>`,
`FeaturePagedResultModel<T>`, `ResultDomain`).

- Sonuçlar statik fabrikalarla üretilir (`Ok`, `Error`, `NotFound`); `new` ile değil.
- Hata `MessageItem` ile taşınır ve `Code` **serbest metin değil bir resource
  sabitidir**; yeni mesaj eklerken önce resource sabiti tanımlanır.
- Exception yalnızca gerçekten beklenmeyen durumlar içindir; onları
  `GlobalExceptionHandler` yakalar.

### V. Scope-Öncelikli Yetkilendirme + Rol = Scope Demeti

Kimlik merkezi IdP `Identity.Server` tarafından verilir (OpenIddict + ASP.NET Identity).
Servisler JWT bearer doğrular ve **yalnız scope** bazında yetkilendirir
(`AuthorizationScopes.*`). Basım merkezidir, zorlama dağıtıktır; merkezi yetki-karar
servisi (PDP) açılmaz.

- **Rol = token verme anındaki scope demetidir.** Kullanıcının rolleri, token
  basılırken rol→scope map'inden scope'lara **açılır** ve access_token'ın `scope`
  claim'ine yazılır. Downstream servisler rolü **GÖRMEZ**: rol claim'iyle yetki kararı
  verilmez, `[Authorize(Roles=...)]` kullanılmaz. Rol yalnız IdP'de yaşar; BC izolasyonu
  korunur (servis rol taksonomisini bilmez, yalnız scope).
- **Scope kümesi KOD-sahipli, kapalı bir registry'dir** (`KnownScopes`). Scope'ları
  servisler kodda tanımlar; hiçbir ekran/DB yeni scope string'i **üretemez**. Rol→scope
  eşlemesi bu kapalı listeden **seçilir** (serbest metin yasak) — uyumsuzluk imkansız.
- **Rol ve rol→scope map DB'de yaşar; admin ekrandan yönetir.** Rol CRUD, rol→scope
  işaretleme ve rol→kullanıcı ataması yetkili admin tarafından yapılır. Register olan
  otomatik `customer` rolü alır (sunucu atar, seçilemez) ve **doğrudan login olabilir**
  (aktivasyon-mail zorunlu değil). Seed: `admin` + `customer` rolleri, rol→scope map ve
  login olabilen **bootstrap admin** kullanıcı (secret config'ten, kodda değil).
- **Back-office dahil her yüzey scope ile korunur.** Yönetim uçları (kullanıcı/rol
  yönetimi vb.) özel bir scope ister (ör. `identity.roles.manage`); bu scope admin
  rolünün demetindedir. Downstream servislerde rol-policy kullanılmaz.
- **İstisna — IdP'nin kendi admin UI'ı:** Bu scope kuralı **downstream API yüzeyleri**
  içindir. `Identity.Server`'ın KENDİ server-rendered yönetim sayfaları (rol/scope/kullanıcı
  yönetimi) rol otoritesinin iç yüzeyidir ve interaktif **cookie** ile auth olur — cookie'de
  scope claim'i bulunmaz, dolayısıyla cookie kullanıcısının `admin` rolüyle korunur. Bu, "rol
  ile yetki" değil, rol otoritesinin kendini koruması olduğundan İlke'yi ihlal etmez;
  `identity.roles.manage` scope'u programatik/API erişim için eşdeğer guard olarak tanımlı kalır.
- **Makine kimlikleri RBAC dışıdır.** Agent/saga gibi insan-olmayan çağıranlar
  `client_credentials` + **statik** scope ile kimliklenir (ClientId/Secret); rol, mail,
  kullanıcı kaydı taşımaz (ör. `ingestion-agent`, `order-saga`).
- Scope zorlaması endpoint'lerde `.RequireAuthorization(...)` ile, Wolverine mesaj
  handler'larında ise `[RequiredScope]` + `ScopeAuthorizationMiddleware` ile uygulanır.
- Kimlik doğrulama şeması JWT bearer ile sınırlı değildir: dış entegrasyonlar için
  JWT-olmayan custom authentication şemaları (ör. opak UserKey) meşrudur — koşul,
  zorlamanın scope olmasıdır.
- **İstisna — süreli tek-kullanımlık capability-link (v1.11.1; v1.12.1'den beri UYKUDA):**
  Scope-korumalı bir yüzeyin ürettiği, sunucu-durumlu (DB'de geri çekilebilir), kısa süreli ve TEK
  kullanımlık token-link, insan-tarayıcı akışı gerektiren DAR (tek amaçlı) hosted ekranı koruyabilir;
  scope zorlaması link-üretim anında uygulanmış sayılır (yetki devri). Koşullar: gizli değer ekrandan
  okunamaz (yazma-only), token URL dışında taşınmaz, yüzey tek amaçlıdır. **Şu an UYKUDA (v1.12.1):**
  son yaşayan örnek (083 Excel import yükleme ekranı, `ImportSession`) 091'de söküldü (katalog doluşu
  commit'li JSON seed'e döndü) — istisna kaldırılmadı (telemetri istisnası emsali), insan-tarayıcı
  gerektiren yeni bir tek-amaçlı ekran doğduğu an aynı koşullarla yeniden geçerlidir.
- Anonim gezinme meşrudur: kimlik istemeyen okuma yüzeyleri (vitrin vb.) login'siz
  erişilebilir kalır; login yalnız kullanıcıya bağlı işlemler için istenir.
- `Identity.Server` HTTPS üzerinden çalışmak zorundadır; tüm servislerin `Authority`
  değeri issuer ile eşleşir.

### VI. Domain-TDD (saf domain mantığı test-first)

Saf domain mantığı **test-first (red-green-refactor)** yazılır. Kapsam: aggregate
davranış metotları, saga `On*` karar metotları, value object'ler ve mock'suz test
edilebilir diğer domain birimleri.

- Önce başarısız test (xUnit + Shouldly), sonra geçirecek en küçük kod, sonra refactor.
- `tasks.md`'de bu birimlerin test task'ları ilgili implementasyon task'ından ÖNCE gelir.
- Kapsam dışı: handler, endpoint, UI, altyapı/wiring — mevcut düzen sürer (test-sonra
  veya canlı doğrulama). Bu ilke için entegrasyon/host harness'ı KURULMAZ.
- Gerekçe: domain katmanı saf ve bağımlılıksızdır; TDD burada ucuz ve doğaldır,
  harness gerektiren katmanlarda pahalıdır. İlke II'nin "iş kuralı aggregate'te"
  kuralı bu ilkeyle test edilebilirliğini kanıtlar.

### VII. Domain Süreci Legibility (EventStorming-belgelenmiş süreç)

Her Bounded Context'in domain süreci **okunabilir olmak zorundadır**: BC'nin iş akışı
(hangi adım, hangi sırayla, hangi komut/olay/policy) **EventStorming altitude'unda**,
ubiquitous dille (teknoloji değil, iş/ürün anlatısı olarak) belgelenir ve **domain süreci
değiştikçe** güncel tutulur.

- Kapsam **domain sürecidir** — iş adımlarının sırası ve doğurdukları olaylar; class-by-class
  çağrı dökümü DEĞİL. Teknoloji yalnız koda-atlama köprüsü olarak kenarda kalır.
- Güncelleme tetiği **dardır**: yeni/silinen command-event-policy ya da adım sırası değişimi.
  Mekanik refactor/rename süreci değiştirmez → belgeyi tetiklemez (o drifti guard yakalar).
- Domain süreci belgesi **olmayan ya da bayat** (koddaki güncel süreçle çelişen) bir BC kabul
  edilemez. Bir feature domain sürecini değiştiriyorsa belge **aynı PR'da** güncellenir.
- Bu bir **süreç disiplinidir** (İlke VI Domain-TDD gibi), yapı invariant'ı değil. Anayasa yalnız
  *ne + neden* der; belgenin **dosya adı, formatı ve senkron-guard'ı** "nasıl" olarak
  `conventions.md`/`CLAUDE.md`'de tanımlanır (İlke VI'nın "test-first" deyip "xUnit"i CLAUDE.md'ye
  bırakması gibi).
- Gerekçe: DDD'nin çekirdeği domain legibility'dir. Koda uzun süre uzak kalan sahibin bile süreci
  geri yükleyebilmesi, domain'in teknolojinin ardında kaybolmaması gerekir. Domain-altitude olduğu
  için bu belge class-yapısından çok daha az bayatlar — yalnız iş süreci değişince değişir.

### VIII. Sır ve Kimlik Değeri İnsan-Yüzeyde Görünmez (NON-NEGOTIABLE)

Makine sırrı ve kimlik değeri (MerchantId/Key, API anahtarı, finansal veri, PII) hiçbir
insan-okunur yüzeye ve hiçbir kalıcı ize yazılmaz: ekran, chat, MCP tool dönüşü, log, trace.
LLM yalnız **sır-olmayan opak tutamaç** görür (durum, referans, maskeli son-dört).

- Sırrın yaşam döngüsü makineler arasındadır: kayıt S2S (bootstrap key), teslim sağlayıcı→store
  HMAC-imzalı callback, kullanım gRPC/S2S. İnsan hiçbir adımda değeri görmez.
- Bir yüzeyin sırrı "göstermesi gerekiyorsa" tasarım yanlıştır — yüzey değil **akış** değişir
  (087 emsali: elle credential giriş ekranı söküldü, yerine makine-handoff geldi).
- Yazma-only capability-link (İlke V istisnası) bu ilkeyi ihlal etmez: değer ekrandan okunamaz.
- Gerekçe: agent-only bir sistemde her tool dönüşü bir LLM bağlamına, oradan model sağlayıcısının
  loglarına girebilir. Sır **dönmeyen** bir kontrol düzlemi, sırrı "dikkatli döndüren" bir kontrol
  düzleminden kategorik olarak güvenlidir — dikkat bir invariant değildir.
  ADR: `docs/adr/adr-mcp-control-plane-no-secret-return.md`.

## Teknoloji ve Mimari Kısıtları

- **.NET 10**, C#, her yerde `Nullable` + `ImplicitUsings` açık.
- **Marten**, Postgres'i EF Core ile değil bir **document/event store** olarak kullanır.
- **Wolverine** hem süreç-içi command/query bus'ı hem RabbitMQ integration mesajlaşmasıdır;
  handler'lar assembly taramasıyla keşfedilir. Exchange/queue adları `RabbitMqConstants`
  içinde merkezidir.
- Sistem **her zaman Aspire AppHost üzerinden** çalıştırılır; tek servis bağımsız
  çalıştırılmaz (bağımlılıklarını service discovery ile bulur).
- **Central Package Management** açık: paket sürümleri `Directory.Packages.props`'a
  eklenir, tek tek `.csproj`'lara değil.
- **DI kaydı Scrutor ile otomatiktir:** `ITransientDependency` / `IScopedDependency` /
  `ISingletonDependency` marker'larından biri implemente edilir; `Program.cs`'te elle
  kayıt yapılmaz.
- Using'ler her projenin tek `GlobalUsings.cs`'ine eklenir.
- Agent / agent framework tipleri **Singleton**'dır; kullanıcıya özel davranış
  agent'ı scope'lamakla değil, kullanıcının token'ını çağrı anında enjekte ederek
  sağlanır.

## Geliştirme Akışı (Spec-Driven Development)

Önemsiz olmayan her feature spec-kit akışını izler:

1. `/speckit-constitution` — ilkeleri kur/güncelle (bu dosya)
2. `/speckit-specify` — feature spec'i (NE ve NEDEN)
3. `/speckit-clarify` (opsiyonel) — belirsizlikleri gider
4. `/speckit-plan` — implementation planı (NASIL)
5. `/speckit-tasks` — sıralı görevler
6. `/speckit-implement` — uygulama

### Artefakt Ölçekleme (feature büyüklüğüne göre)

Akış her feature için aynı **törenle** dayatılmaz; üretilen artefakt seti işin
büyüklüğüne göre ölçeklenir. Amaç, "boş-doğru" (mevcut durumu tekrar eden, yeni bilgi
katmayan) dosya üretmemektir. Üç kademe:

- **Trivial** — davranış değiştirmeyen ya da yeni iş kuralı getirmeyen değişiklik
  (bug fix, refactor, metin/konfig). Spec-kit gerekmez; doğrudan implement + commit.
- **Küçük** — *tümü* sağlanıyorsa: tek aggregate içinde kalır, yeni tablo/şema yok,
  yeni endpoint kontratı yok, servisler-arası (integration event) etki yok, kayda
  değer belirsizlik yok. Yalnızca **`spec.md` + `tasks.md`** üretilir;
  `plan/research/data-model/contracts/quickstart` **üretilmez**.
- **Tam** — yukarıdaki maddelerden biri bile bozuluyorsa (yeni aggregate/tablo,
  servisler-arası event, yeni endpoint kontratı, veya belirsizlik) tam akış işletilir.

Kademe seçimi spec'in başında bir satırla gerekçelendirilir. Şüphedeyse bir üst
kademe seçilir. `001-product-sale-readiness` retrospektif olarak "Küçük"tü.

Kalite kapıları:

- Testler xUnit + Shouldly ile yazılır; domain birim testleri saftır (host/entegrasyon
  harness'ı yoktur). Saf domain mantığı İlke VI gereği **test-first** yazılır.
- API sürümleme URL-segment tabanlıdır (`v1`); dokümanlar Scalar ile sunulur.
- Fiziksel klasörler solution klasörleriyle birebir örtüşür.

## Otomatik Regression Kapsamı

Bugün yalnız **saf unit test** vardır (xUnit + Shouldly; host/entegrasyon harness'ı yok — İlke VI).
Otomatik E2E kapsamı YOKTUR ve bu bilinçli bir durumdur, borç değil.

- v1.8.0'da eklenen "E2E Testing (Playwright)" bölümü **v1.12.0'da kaldırıldı.** Tarif ettiği akışlar
  (anonim vitrin gezinme, sepet/checkout ekranları, `/Admin/Roles` yönetimi, `data-testid` selector'ları,
  web-first assertion'lar) WebApp'e aitti; WebApp 066'da söküldü ve sistem agent-only oldu. Harness hiç
  kurulmadı (`tests/E2E` yok, repoda Playwright referansı yok) — bölüm, var olmayan bir yüzey için var
  olmayan bir harness'ı dayatıyordu ve aynı dosyadaki "Kalite kapıları" maddesiyle çelişiyordu.
- Agent-only yüzey için otomatik regression istenirse **yüzey MCP'dir, tarayıcı değil**: Aspire tam-stack
  üstünde tool-çağrısı seviyesinde doğrulama. Bu bir tasarım kararıdır — kendi spec'i ve bu dosyada bir
  amendment ile gelir; varsayılan olarak dayatılmaz.
- O gelene kadar kritik akışların doğrulanması **elle**dir: Aspire'ı kaldır, dış MCP istemcisinden
  (Claude Desktop vb.) akışı yürüt. Domain mantığı zaten unit testlerdedir ve İlke VI gereği test-first.

## Governance

- Bu anayasa diğer tüm pratiklerin üstündedir. Bir spec/plan/PR anayasayla
  çelişiyorsa, ya anayasaya uydurulur ya da bu dosyada gerekçelendirilmiş bir
  değişiklikle (amendment) güncellenir.
- Ayrıntılı, gündelik geliştirme rehberi `CLAUDE.md`'dir; anayasa "ne pazarlık
  edilemez", CLAUDE.md "nasıl uygulanır" sorusunu yanıtlar. İkisi çelişirse anayasa
  kazanır ve CLAUDE.md hizalanır.
- Değişiklikler (amendment) commit mesajında ve versiyon artışıyla belgelenir:
  ilke ekleme/kaldırma MAJOR, yeni ilke/bölüm ekleme MINOR, açıklama/düzeltme PATCH.

**Version**: 1.12.2 | **Ratified**: 2026-07-12 | **Last Amended**: 2026-10-19

<!-- v1.12.2 (2026-10-19, PATCH): İlke I orkestre saga adım/telafi kanalının (v1.11.0) canlı örneği
     049 Checkout.Orchestrator UYKUDA işaretlendi — 092-checkout-choreography ile söküldü (checkout
     choreography'ye döndü: fanout StockCommitRequested/StockCommitted/StockCommitFailed, atomik batch
     commit, watchdog yok). Kanal KALDIRILMADI (telemetri v1.12.0 + capability-link v1.12.1 "örneksiz uyku"
     emsali): choreography zaten İlke I'in birincil kanalı; çok-adım/dallanan/telafili süreç doğunca kanal
     yine meşru. İlke içeriği değişmedi → PATCH. ADR: adr-checkout-choreography-migration. -->

<!-- v1.12.1 (2026-10-19, PATCH): İlke V capability-link istisnası UYKUDA işaretlendi — son yaşayan
     örnek (083 Excel import ekranı, ImportSession) 091-catalog-json-seed ile söküldü (Excel import +
     ürün yayın makinesi gitti; katalog doluşu commit'li books.json seed'i). İstisna kaldırılmadı
     (İlke I telemetri istisnasının v1.12.0 emsali): koşullar değişmedi, örneksiz kaldı. İlke içeriği
     değişmedi → PATCH. -->


<!-- v1.12.0 (2026-10-16, MINOR): "E2E Testing (Playwright)" bölümü kaldırıldı → "Otomatik
     Regression Kapsamı". Bölümün tarif ettiği yüzey (WebApp ekranları) 066'da söküldü, harness hiç
     kurulmadı, madde Kalite kapıları + İlke VI ile çelişiyordu. Agent-only sistemde regression'ın
     yüzeyi MCP'dir, tarayıcı değil — istenirse kendi spec'i + amendment ile gelir. Ayrıca İlke VIII
     eklendi (sır/kimlik değeri insan-yüzeyde görünmez; 087 + ADR'den anayasaya terfi, çünkü
     CLAUDE.md'de kalınca spec/plan üretiminde okunmuyordu). İlke V capability-link örneği 078'den
     083 ImportSession'a taşındı (078 ekranı 087'de söküldü, mekanizma import'ta yaşıyor); İlke I
     telemetri istisnası UYKUDA işaretlendi (UI/BFF yok, 042 söküldü); İlke I'den WebApp örneği ve
     İlke II'den var olmayan Brand aggregate'i çıkarıldı. Gerekçe: dosya, var olmayan yüzeyler için
     normatif kural dayatıyordu — anayasa her spec/plan'da okunduğu için en pahalı bayatlama burası. -->

<!-- v1.11.1 (2026-09-18, PATCH): İlke V'e açıklayıcı istisna — süreli + TEK kullanımlık
     capability-link. Scope-korumalı bir yüzeyin ürettiği, sunucu-durumlu (geri çekilebilir), kısa
     ömürlü token-link, insan-tarayıcı akışı gerektiren tek-amaçlı hosted ekranı koruyabilir; scope
     zorlaması link-ÜRETİM anında uygulanmış sayılır (yetki devri). Koşullar: tek amaçlı yüzey,
     gizli değer ekrandan OKUNAMAZ (yazma-only), token URL dışında taşınmaz. İlke ihlali değil
     açıklayıcı istisna → PATCH (v1.7.1 IdP-cookie emsali). Gerekçe: 078-hosted-onboarding-form —
     MerchantKey LLM sohbetine giremez, store'da web-login yüzeyi yok (066). NOT (v1.12.0): o ekran
     087'de söküldü; istisnanın yaşayan örneği artık 083 Excel import'tur. -->

<!-- v1.11.0 (2026-08-25, MINOR): İlke I orkestre saga adım/telafi kanalı genişletildi — sürecin
     sahibi saga, hedefli komutları tipli senkron RPC (gRPC/HTTP) YA DA hedefli asenkron broker
     komut/yanıtı (korelasyon + idempotency; fanout değil) ile yürütebilir. (b) temporal decoupling/
     at-least-once ya da ayrı orchestration servisi host'unda seçilir; bilinçli sözleşmeyle, DB
     izolasyonu korunur. Örnek: 028 gRPC, 049 broker. gRPC'yi kaldırmaz (ek meşru kanal) → MINOR.
     Gerekçe: 049-checkout-orchestrator ayrı Checkout.Orchestrator + broker-only komut/yanıt. -->

<!-- v1.10.0 (2026-08-23, MINOR): İlke VII eklendi — Domain Süreci Legibility. Her BC'nin domain
     süreci EventStorming altitude'unda, ubiquitous dille belgelenir ve süreç değiştikçe (yeni/silinen
     command-event-policy, sıra değişimi) güncel tutulur; belgesi olmayan/bayat BC kabul edilemez.
     Süreç disiplinidir (İlke VI emsali); anayasa ne+neden der, dosya adı/format/guard "nasıl" olarak
     conventions.md/CLAUDE.md'ye bırakılır. Gerekçe: koddan uzak kalan sahibin süreci geri yükleyebilmesi,
     domain'in teknoloji ardında kaybolmaması (DDD çekirdeği). Procurement FLOW.md pilotu yazıldı. -->

<!-- v1.9.0 (2026-08-21, MINOR): İlke I'e telemetri kanalı istisnası — kayıp-toleranslı davranış
     telemetrisi UI/BFF'den tek-tüketicili BC'ye versiyonlu log dosyasıyla beslenebilir; ikinci
     tüketici doğarsa integration event'e terfi zorunlu; DB izolasyonu sürer. Gerekçe: 042
     Personalization (WebApp davranış JSONL'i → Python BC); event töreni telemetri için ağır. -->

<!-- v1.8.1 (2026-08-11, PATCH): İlke I'e netleştirme — MCP'yi yalnız agent'lar tüketir; agent
     olmayan koddan imperatif CallToolAsync yasak (WebApp GatewayRegistrationClient bu kuralla
     silindi, 033). Yapısal ihtiyaç REST/gRPC. -->

<!-- v1.8.0 (2026-08-07, MINOR): Yeni bölüm — "E2E Testing (Playwright)". Kullanıcıya-dönük kritik
     akışlar (anonim vitrin, sepet/checkout-saga, RBAC admin, kritik hatalar) Microsoft.Playwright +
     xUnit ile Aspire.Hosting.Testing tam-stack üstünde regression olarak doğrulanır; business logic
     unit'te kalır (İlke VI). E2E TDD döngüsü dışı, feature-sonrası. web-first assertions zorunlu,
     Thread.Sleep yasak, data-testid öncelikli. Gerekçe: unit var, E2E yok — kritik yollar için
     otomatik regression güvencesi. Harness tests/E2E'de ilk ihtiyaçla kurulur (henüz yok). -->

<!-- v1.7.1 (2026-08-06, PATCH): İlke V'e açıklayıcı istisna eklendi — "back-office scope-gated"
     kuralı downstream API yüzeyleri içindir; Identity.Server'ın KENDİ server-rendered admin UI'ı
     cookie ile auth olur (cookie'de scope yok) ve cookie kullanıcısının admin rolüyle korunur.
     Bu rol-otoritesinin kendini koruması olduğundan İlke'yi ihlal etmez; identity.roles.manage
     programatik guard olarak kalır. Gerekçe: 030-rbac /speckit-analyze K1 gerginliğini kapatır. -->

<!-- v1.7.0 (2026-08-06, MINOR): İlke V keskinleştirildi — rol mekanizması netleşti. Rol token'a
     "claim biner" değil, token verme anında rol→scope map'inden SCOPE DEMETİNE açılır; downstream
     servisler rolü GÖRMEZ (rol claim ile yetki yok, [Authorize(Roles=...)] yok). Scope kümesi
     KOD-sahipli kapalı registry (KnownScopes); rol + rol→scope map DB'de, admin ekrandan yönetir,
     serbest scope string yazamaz (listeden seçer). Back-office dahil her yüzey scope ile korunur.
     Makine kimlikleri client_credentials + statik scope, RBAC dışı. Register aktivasyon-mail
     zorunlu değil (customer direkt login). Seed: admin+customer + rol→scope + bootstrap admin.
     Gerekçe: 2026-08-06 RBAC tasarım oturumu — granülarite endişesi (rol=scope demeti), ekrandan
     rol yönetimi + uyumsuzluk riski (kapalı scope registry), makine=client_credentials ayrımı. -->

<!-- v1.6.0 (2026-08-06, MINOR): İlke V yeniden yazıldı — IdP teknoloji-nötr (hedef OpenIddict +
     ASP.NET Identity; Duende lisans gerekçesiyle terk ediliyor) ve rol modeli geri geldi:
     register otomatik `customer` rolü (sunucu atar), yönetim yüzeyleri rol-policy (sabit adlı),
     servis akışlarında zorlama scope kalır, seed admin+customer + bootstrap admin, endpoint-bazlı
     DB yetki yasak, rol×permission katmanına açık. Gerekçe: 2026-08-06 tasarım oturumu —
     OpenIddict migrasyonu + kullanıcı/rol yönetim ekranları + e-posta aktivasyonlu register. -->

<!-- v1.5.0 (2026-08-05, MINOR): İlke VI (Domain-TDD) eklendi — saf domain mantığı test-first;
     tasks.md'de domain test task'ları implementasyondan önce. Handler/endpoint/UI kapsam dışı.
     Gerekçe: mülakat/TDD tartışması sonrası kullanıcı kararı; domain katmanı zaten mock'suz
     test edildiğinden TDD maliyeti düşük, entegrasyon harness'ı gerektirmiyor. -->

<!-- v1.4.0 (2026-08-05, MINOR): İlke I senkron RPC sanksiyonu orkestre edilmiş saga adım/telafi
     komutlarını kapsayacak şekilde genişletildi. Gerekçe: 028-checkout-saga — kullanıcı kararıyla
     tam orchestration; telafi (RevertCommit) ve pivot-sonrası temizlik (ClearBasket) hedefli komut
     ister, fanout event bunu modelleyemez. DB izolasyonu ve Shared/Protos kontrat kuralı aynen. -->

<!-- v1.3.1 (2026-07-28, PATCH): İlke I örneği güncellendi — Discount BC'si 018 ile
     kaldırıldığından örnek "Ürün" (Catalog aggregate / Basket entity / Storefront read-model
     satırı) olarak değiştirildi; ilke içeriği değişmedi. -->

<!-- v1.3.0 (2026-07-27, MINOR): İlke II gevşetildi — "her serviste tek aggregate root"
     yerine "BC gerektiği kadar zengin aggregate içerebilir; anemik aggregate yasak;
     aggregate'ler arası referans Id ile". Gerekçe: 016-category-brand — Catalog BC'ye
     Product yanına kimlikli Category ve Brand aggregate'leri (get-or-create, immutable ad)
     ekleniyor; VO modeli teklik invariant'ını taşıyamadığı için reddedildi. -->

<!-- v1.2.0 (2026-07-24, MINOR): İlke I'e tipli senkron RPC (gRPC/HTTP) sanksiyonlu
     servisler-arası kanal olarak eklendi (DB izolasyonu korunur). Gerekçe: stok
     rezervasyonu (spec 012) anlık evet/hayır kararı gerektirir; async event/MCP yetmez. -->