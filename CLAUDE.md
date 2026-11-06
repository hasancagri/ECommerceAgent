# CLAUDE.md

Claude Code'a bu repo'da rehberlik eder. **Gerçek-kaynak sırası:** kod + bu dosya >
Claude memory > Obsidian vault. Spec geçmişi repo'dan çıkarıldı — damıtması Obsidian'da
(`DevNotes/ECommerceAgent/reference/specs-arsivi-genel-yapi.md`), ham hali tarih arşivinde.

Bu dosya **bugünkü durumu** anlatır. Sökülen yapıların gerekçesi
[`docs/adr/`](docs/adr/README.md)'da ("neden böyle karar verildi?": BC-per-service, saga,
cache vs read-model, sır-dönüş yasağı…) — dev-anında oradan grep'le. Geri gelmemesi gerekenler
"Yapma listesi"nde. Vault bağlayıcı değil.

**Katman ayrımı — her bilgi tek evde:**

| Soru | Ev |
|---|---|
| İhlali kabul edilemez mi? | `.specify/memory/constitution.md` (her spec/plan bunu okur) |
| Taşınabilir mimari/kod konvansiyonu mu? (DDD/VSA, kod standardı, servisler-arası desen) | @docs/conventions.md |
| Bu projeye özel operasyonel bilgi mi? (komut, BC haritası, yüzey, tuzak) | bu dosya |

## Komutlar

Repo kökünden. Çözüm: `ECommerceAgent.slnx` (`dotnet build/test` dosyayı otomatik bulur,
açıkça vermeye gerek yok). Format/lint script'i YOK.

```bash
dotnet build                                              # tüm çözüm
dotnet run --project src/aspire/AppHost/AppHost.csproj    # tüm sistem (Aspire)
dotnet test                                               # tüm testler
dotnet test tests/Basket.Api.Tests/Basket.Api.Tests.csproj          # tek proje
dotnet test --filter "FullyQualifiedName~BasketTests.AddItem"       # tek test
```

**Commit öncesi** (CI yok, guard yok — script'ler 2026-11-02'de söküldü):

```bash
dotnet build && dotnet test
```

FLOW.md anchor'ları ve BC haritası artık elle/review'da doğrulanır. Dev veri kaynağı:
`~/Desktop/ECommerce Notları/Kitapyurdu_Katalog_v2.xlsx` (21-alan Kitapyurdu kazıması, repo-dışı).

- **Sistemi hep Aspire AppHost'tan başlat**, tek servis değil — servisler birbirini/DB/RabbitMQ'yu
  service discovery + conn-string enjeksiyonuyla bulur; tek API bağımsız açılmaz.
- **Marten şeması otomatik kurulur** (`ApplyAllDatabaseChangesOnStartup`) — migration komutu yok.
- **Paket sürümleri yalnız `Directory.Packages.props`'ta** (Central Package Management); `.csproj`
  `PackageReference`'ı sürümsüz listeler. Sürüm ekle/değiştir → yalnız props.

## Teknoloji

.NET 10 (`Nullable`+`ImplicitUsings` açık) · **Marten** (Postgres = document/event store, Newtonsoft,
non-public setter+ctor) · **Wolverine** (in-proc bus `IMessageBus` + RabbitMQ fanout; handler assembly
taramasıyla) · **HybridCache + Redis** (AOP cache L1/L2 yalnız okuma; boşaltma
handler-içi atomik publish + epoch-key, 090 — native tag KULLANILMAZ, bkz. conventions) ·
**OpenIddict + ASP.NET Identity** (IdP ÇÖZÜM İÇİNDE: `src/others/Identity.Server` — 005 kopyası; tek EF Core+Razor istisnası, ADR adr-identity-inward) ·
**Scrutor** (DI) · **xUnit + Shouldly**. (096 Ollama/embedding omurgası 099 öncesi SÖKÜLDÜ —
semantik ayak ileride yeni spec'le; MCP + Microsoft Agent Framework temiz-sayfa sökümüyle
kalktı — yüzey REST first-party'ye dönüyor, bkz. Yüzey modeli.)

## BC haritası

Her BC = kendi DB'si + şeması. (Parantezli numaralar eski spec numaralarıdır — tarihçe Obsidian
damıtmasında.) Servisler `src/services/*`; destek `src/others`
(`Common`/`Shared`), `src/aspire` (`AppHost`/`ServiceDefaults`).

| Servis | DB | Ne yapar |
|---|---|---|
| `catalog` | catalogDb | Sade kitap künyesi (095): `Product` (kimlik Gtin/ISBN unique-index; ad/açıklama/fiyat/sayfa/çevirmen/dil/tarih; çok-yazar + tek yayınevi + cilt tipi + popülerlik sayaçları (001, dış-kaynak anlık görüntüsü); **TEK leaf `CategoryId` + editoryal `RelatedCategoryIds`** — üst kategori üründe DENORMALIZE DEĞİL, yayın anında ağaçtan çözülür `CategoryNaming.ResolveChain`) + `Category` (kapalı 2 seviyeli ağaç: ebeveyn doğumda sabit, Create-only) + `Author`+`Publisher`. **Satışa-uygunluk modeli (001):** ürün fiyatsız/yazarsız doğabilir (katalogda görünür, satılamaz); `IsSalable` TEK tanım = fiyat>0 ∧ stok>0 ∧ ≥1 yazar (stok bacağı `StockCount` — `StockChanged`-fed kopya, 002; `DiscountRate` etkin-oran kopyası — `ProductDiscountChanged`-fed, 003, IsSalable'a girmez; tüketiciler `StockConsumers`/`DiscountConsumers` DLQ-destekli); fiyat sonradan `SetPrice`'la atanır/serbest değişir, >0 guard (097 "doğumda sabit" REVİZE edildi; 058 fiyat geçmişi sökük kalır). **096 embedding omurgası SÖKÜLDÜ (099 öncesi, kullanıcı kararı):** `ProductEmbedding`+Ollama hattı+reconcile süreci gitti — semantik arama ayağı ileride yeni spec'le, bilinçli kurulur. **İlk REST admin yüzeyi AÇIK (004):** Excel import uçları (`/api/admin/catalog/imports` — gateway'den, X-Admin-Api-Key çift kat) — ürün giriş yolu: çok-sayfalı xlsx (sayfa=yayınevi), İKİ-FAZ: önce dry-run (hiç yazmaz, sebepli rapor) → admin onayı (`POST .../{id}/confirm`) → INSERT-ONLY yazım (ISBN varsa dokunma); arka plan `ImportJobWorker` (durum-güdümlü polling, mesaj/kuyruk tetiği YOK); `ImportJob` aggregate'i; yüklenen dosya silinmez. **Kapak dönüşü GERİ (004, 097 tersine):** `CoverIngested` tüketilir (`FileConsumers`) → `ImageUrl` dolar |
| `basket` | basketDb | Kalıcı sepet + kalem; sahip = login kullanıcı (anonim sahiplik 057 + `MergeFrom` SÖKÜLDÜ — sepete atmak login ister); stok kavramı yok (dijital pivot), süre yok (056); dış yüzey ŞU AN YOK (MCP söküldü) — kalan tüketici checkout gRPC + `OrderCompleted` temizliği |
| `order` | orderDb | Sipariş aggregate + yaşam döngüsü. 094 pay-by-link akışı (giriş yüzeyi MCP'yle söküldü, REST first-party'de yeniden açılacak; gRPC istemcileri + davranış durur): sepet (Basket gRPC) ile tutar (liste fiyatı — indirim 095'te söküldü) → Pending order → Payment BC'ye S2S intent+link (`PaymentIntentClient`) → `HostedUrl`; başarısızda order commit YOK; `/checkout/done` dönüş stub'ı. **Dijital pivot: stok ayağı KALKTI** — `PaymentSucceeded`→`Order.Confirm`+`OrderCompleted` fanout (Reviews+Library+Basket) / `PaymentFailed`→Cancel (`PaymentConsumers`, idempotent durum-guard) |
| `payment` | paymentDb | `PaymentIntent` durum makinesi (kart alanı yok, TxRef unique). 094 pay-by-link: Order S2S intent+link gRPC (`PaymentIntentGrpcService`) + EC→PG `POST /payment-links` REST (`PgPaymentLinkClient`, X-Api-Key; `STORE_NOT_LIVE` fail-fast) + PG→EC HMAC webhook (`ProcessPaymentCallback`+`CallbackSignatureValidator`: fail-closed, `pgPaymentRef` eşle, anti-tamper amount guard, idempotent) → `PaymentSucceeded`/`PaymentFailed` fanout. Kart alanı HİÇ taşımaz (İlke VIII, yalnız opak `pgPaymentRef`). Merchant kimliği PG'de. Ödeme geçmişi okuma yüzeyi MCP'yle söküldü |
| `customer` | customerDb | AddressBook (≤1 varsayılan invariant'ı); izole, event YAYMAZ; Order'a varsayılan-adres gRPC'si (uykuda). Adres MCP tool'ları söküldü — dış yüzey ŞU AN YOK. **Wallet 076'da, merchant kimliği/onboarding (087) 093 PG pivotunda SÖKÜLDÜ** |
| `reviews` | reviewsDb | Satın-alma şartlı yorum (kanıt `OrderCompleted`-fed lokal read-model); yorum HEMEN görünür, moderasyon YOK (088); özet (ortalama+adet) okuma anında hesaplanır. Yazma/okuma MCP yüzeyi söküldü — aggregate davranışı durur, yüzey REST first-party'de |
| `library` | libraryDb | Kullanıcı-ürün ilgi kayıtları; **fiyat alarmı UYKUDA (097):** fiyat değişmezliğiyle tetik öldü — `ProductChangedEvent` aboneliği söküldü, `PriceAlarm` modeli + `PriceAlarmTriggered` kontratı yayıncısız durur, `NotificationRecord` izi kalır; `UserPurchase` (kişisel satın-alma birikimi) `OrderCompleted` tüketir (`OrderConsumers`, idempotent upsert) |
| `stock` | stockDb | Kurgusal envanter (002, fiziki dönüş): `StockItem` (ProductId unique-index + OnHand≥0; mutlak atama `SetQuantity`, satış düşümü `Decrease` — yetersizse red). Adedin TEK yazarı; her değişimde `StockChanged(productId, onHand)` fanout → Catalog adet kopyası. Rezervasyon YOK (056 sürer); dış yüzey + checkout bağlantısı sonraki dilimler |
| `discount` | discountDb | Kampanya modeli (003, dönüş — tarihçe 079): `Campaign` (tek ürün + oran %1-%90 + zaman penceresi + iptal; etkinlik `IsActiveAt` — zaman dışarıdan). Üründe ≤1 etkin indirim, son-gelen-kazanır (`CampaignSelection`). İndirim liste fiyatını EZMEZ — `ProductDiscountChanged(productId, rate?)` kontratı + Catalog oran kopyası hazır; YAYINCI yürürlük süreci (`CampaignWindowProcess`) + dış yüzey + checkout bağlantısı SONRAKİ dilimler |
| `file` | fileDb | Kapak **kayıt defteri** (082); `FileAsset` (ImageName=ISBN tekil/değişmez unique-index + metadata) + nested `FileStorageLocation` (çoklu fiziki depo: R2/Local/…, upsert invariant). Fiziki bit `IFileStore` ardında (`S3FileStore`→R2; byte DB'de DEĞİL); URL provider-agnostik lokal çözülür (`CoverUrlResolver`, StorageFilePath=key + config-base; full URL saklanmaz). `GET /files/v1/covers/{isbn}` anonim serve; S2S `POST /internal/files` (yaz+kayıt) + `/resolve` (batch, 0 dış çağrı) + `GET .../locations`; idempotent R2 backfill (config-gated). **Kapak kablosu (083/091):** `ProductAdded` tüketir (`CatalogConsumers`; **004:** `CoverSourceUrl` doluysa ve R2'de yoksa dış adresten İNDİRİR, R2'de varsa atlar; yoksa eski staging yolu) → `CoverIngested(isbn,url)` yayar — **Catalog tüketicisi GERİ (004):** `FileConsumers` → `Product.SetImage`; açılış kapak migration'ı KAPALI kalır (098; R2'deki kapaklar + kayıt defteri duruyor) |
| `identity-server` | identityDb | **ÇÖZÜM İÇİNDE (005 — 084 tersine, ADR adr-identity-inward):** `src/others/Identity.Server` = AgentPlatform kopyası (kaynağa dokunulmadı; PG oradakiyle yaşar). OpenIddict (code+PKCE + m2m + refresh; refresh güncel rol demetiyle süzer) + ASP.NET Identity + rol→scope RBAC (`ScopeResolver` kesişimi) + server-rendered `/Admin/*` (cookie, İlke V istisnası). Kayıt HEP customer; admin yalnız config/secret seed'i (`identity-admin-email/password`, idempotent). Kırpıldı: DCR/consent/dış-agent istemcileri/ApiKey uçları/super-admin. Seed istemciler: `react-web` (PKCE; post-logout `localhost:5173`), `order-saga`+`payment-s2s` (m2m). Issuer `https://localhost:5001` (servis config'leri DEĞİŞMEDİ). **Google ile giriş (006):** koşullu — `Authentication:Google` ClientId/Secret user-secrets'ta YOKSA buton render edilmez, sistem Google'sız tam çalışır; ilk girişte şifresiz customer, aynı DOĞRULANMIŞ e-postayla mevcut hesaba bağlanır (çift hesap imkansız), doğrulanmamışsa reddedip şifreye yönlendirir (`Pages/Account/ExternalLogin`). **Tek EF Core+Npgsql+Razor istisnası** (Marten değil); reset mail'i log/mock. Auth kablosu `Common/Auths`'ta (`Platform.Auth` paketi + GitHub feed ÖLDÜ) |

(Söküldü — 2026-10-30 temiz-sayfa: `stock` [dijital/PDF pivotu — envanter kavramı kalktı],
`storefront`+ES [arama Catalog pg-hibrit'e katlanacak], `notification-agent`+`mail-mcp` [MCP sökümüyle],
EC'deki TÜM `/mcp` uçları + AgentPlatform fasat aboneliği [yüzey REST first-party'ye dönüyor].
2026-10-31 (095 branch'i): `discount` [kampanya/indirim modeli kullanıcı kararıyla öldü — checkout
tutarı = liste fiyatı; `discount.*` scope'ları, proto ve `discount.events` kuyruğu da gitti; tarihçe
079]. Gerekçeler ADR + Obsidian spec damıtmasında durur.)

## Yüzey modeli

**Temiz-sayfa geçişi (2026-10-30):** BYO-agent/MCP duruşu TERK EDİLDİ — EC'deki tüm `/mcp` uçları,
tool sabitleri (`McpToolNames/Descriptions`), PRM keşfi ve DLQ admin tool'ları söküldü. Hedef yüzey:
**first-party React ekran + REST API'ler + local LLM danışman** (kişiselleştirilmiş keşif sitesi);
arama Catalog'da pg-hibrit (pg_search BM25 + pgvector + RRF). İkisi de HENÜZ YOK — bugün dış
müşteri/admin yüzeyi kapalı, domain davranışları aggregate'lerde duruyor.

- **Gateway (004, YARP `src/others/Gateway`):** React'ın tek origin'i; rotalar config'te
  (`/api/admin/catalog/**` admin-anahtar korumalı, `/api/catalog/**` + `/files/**` anonim). Kenar
  yetki Dilim-1'de `X-Admin-Api-Key` (Aspire secret parametresi `gateway-admin-api-key`; Catalog
  aynı anahtarı İKİNCİ kat doğrular — `AdminApiKeyEndpointFilter`); Dilim 2'de JWT'ye evrilir.
  ForwardedHeaders ServiceDefaults'ta tek yerde.
- **Yaşayan HTTP yüzey:** Catalog admin import uçları (004, gateway'den) · File kapak serve/S2S
  (`/files/v1/covers`, `/internal/files`, `/resolve`) · PG→EC `/payments/callback` HMAC webhook
  (dış-sağlayıcı callback istisnası) · Order `/checkout/done` dönüş stub'ı · auth (Identity OIDC — dış repo).
- **Yaşayan senkron kontratlar (S2S):** Order→Basket `GetBasketItems` gRPC · Order→Payment
  `CreateIntentAndLink` gRPC · EC→PG `POST /payment-links` REST (X-Api-Key) ·
  Order→Customer varsayılan-adres gRPC (uykuda).
- **Yetki iskeleti korunur:** `Platform.Auth` kablosu + `[RequiredScope]` + `AuthorizationScopes`
  kapalı registry yerinde — REST first-party açılınca aynı scope'larla korunacak. IdP ve rol→scope
  yönetimi AgentPlatform'da (084/085 düzeni değişmedi).
- **Ölü-mesaj (DLQ):** Wolverine retry→error-queue davranışı sürer; operatör TOOL yüzeyi söküldü —
  inceleme/replay şimdilik elle (RabbitMQ UI / DB), REST admin yüzeyiyle geri gelebilir.

## Ürün yazım yolu (050: first-party — mallar mağazanın)

**Excel import AÇIK (004 — 098 sökümünün yerine gelen yol):** admin `Kitapyurdu_Katalog_v2.xlsx`
yükler (çok-sayfalı; SAYFA=YAYINEVİ, 27 sütun) → arka plan işi INSERT-ONLY işler (ISBN varsa
dokunma), kapak zinciri `ProductAdded.CoverSourceUrl`'den akar. Okunmayan sütunlar: stok/indirim/
okuma süresi (stok elle/import'la YAZILMAZ — mal kabul artırır, satış düşürür; kullanıcı kuralı).
Elle oluşturma/düzenleme yüzeyi hâlâ YOK — sonraki dilimler.

**Taslak/yayın/gizleme ekseni yok (091):** fiyat>0 var oluş invariant'ı (`Product.Create`; fiyat
doğumda sabit — 097, SetPrice yok); `ProductChangedEvent.IsDeleted` kontratta durur, hep `false`.
`ProductChangedEvent` yayılır ama tüketicisi ŞU AN yok (097). **Silme YOK** (016).

## Test

Testler **saf unit** — xUnit + Shouldly, `tests/<Servis>.Api.Tests` (10 proje, hepsi `.slnx`'te).
Host ayağa kaldırılmaz: `WebApplicationFactory` yok, Testcontainers yok, integration/E2E harness YOK
(Anayasa İlke VI bunu açıkça kurmaz der). Kapsam: aggregate davranış metotları, saga `On*` karar
metotları, value object'ler ve mock'suz test edilebilir saf birimler (ör. `CacheKeyFactory`).
Marten/RabbitMQ'ya giden yollar test edilmez — Aspire'da elle doğrulanır.

Yeni test: ilgili `tests/<Servis>.Api.Tests` köküne `<Konu>Tests.cs` (alt klasör yok). Prod sorgu/komut
tipine bağlanmak gerekmiyorsa test-yerli `record` tanımla (`CachingAspectTests` deseni). Saf domain
mantığı **test-first** yazılır ve `tasks.md`'de test task'ı implementasyondan ÖNCE gelir.

## Yeni şey ekleme

**Yeni yüzey (REST first-party):** desen 004'le kuruldu — örnek: `Domains/ImportJobs` (slice +
`ImportJobEndpointExtension` + gateway rotası + admin-anahtar filtresi). Yeni REST feature aynı
deseni izler; yetki Dilim 2'de `[RequiredScope]`/JWT'ye bağlanacak (conventions.md VSA kuralları aynen).

**Yeni BC:** `src/services/<bc>/<Bc>.Api` + kendi Postgres DB'si/şeması + `tests/<Bc>.Api.Tests`
(ikisi de `.slnx`'e) · AppHost'ta servis + DB kaydı · sürümler `Directory.Packages.props` ·
`Extensions/MessagingExtensions.cs` (Wolverine kablosu) · entegrasyon event'lerle, senkron
çağrı EKLEME (meşru istisnalar yukarıda sayılı) · FLOW.md domain süreci (İlke VII) · BC haritasına satır ekle.

## Projeye özel yetki + tuzaklar

- **RBAC (scope; İlke V):** `AddAuthenticationAndAuthorizationExtension(config, ...scopes)`;
  `KnownScopes` kapalı registry, rol→scope map DB'de ve AgentPlatform IdP'nin kendi
  server-rendered `/Admin/*` ekranlarından yönetilir (cookie auth — Anayasa İlke V istisnası). Register → `customer` rolü. `[RequiredScope]` Wolverine mesaj
  handler'larına da uygulanır. Identity **HTTPS zorunlu**.
- **TUZAK (Marten tr-TR noktasız ı — TEKRARLAYAN):** Adında büyük `I`/`İ` geçen HER yeni Marten
  belgesi (`ImportJob`, `Invoice`, `İade`…) tr-TR ToLower'da `mt_doc_ımportjob` gibi ı'lı tablo adı
  üretir → ilk açılış çalışır, SONRAKİ açılışta 42P07 "already exists" crashloop (Weasel ı'lı adı
  eşleyemez; servis kalkmaz, ona WaitFor'lu her şey — gateway dahil — zincirleme doğmaz).
  **Kural: `opts.Schema.For<T>().DocumentAlias("...")` — elle, lowercase, I'sız — yeni belge
  eklerken İLK iş.** Yakalanırsa: bozuk ı'lı tablo DROP + alias ekle (örnek: `excel_import_job`, 004).
- **TUZAK (`ScopeClaimArrayHandler`):** `context.TokenType` bir URN'dir
  (`TokenTypeIdentifiers.AccessToken`), hint DEĞİL — hint'le kıyaslarsan handler no-op olur ve yetki
  sessizce düşer (403). **Scope-korumalı bir uçta beklenmedik 403 görürsen ilk bakılacak yer burası.**
- **DCR istemcileri (061):** public+PKCE, `ConsentType=Explicit`, kapalı scope demeti
  `ExternalAgentDefaults` (yönetim scope'ları giremez, client_credentials verilmez); seed istemciler
  Implicit kalır (consent görmez). Redirect yalnız loopback + Claude callback (`DcrRequestValidator`).
- **Capability-link deseni (Anayasa İlke V istisnası — UYKUDA, 091):** scope-korumalı bir tool'un
  ürettiği kısa-ömürlü TEK KULLANIMLIK token-link, insan-tarayıcı gerektiren tek-amaçlı ekranı
  koruyabilir. Son yaşayan örnek (083 Excel import ekranı) 091'de söküldü — istisna kaldırılmadı,
  ihtiyaç doğunca aynı koşullarla geçerli: gizli değer ekrandan OKUNAMAZ (yazma-only).
## Yapma listesi

- **Çok-tedarikçi feed zincirini geri getirme:** Procurement + Supplier (+ Supplier.Gateway /
  IngestionAgent) 050'de söküldü — model first-party, mallar mağazanın. Ürün girişi = ürün-CRUD.
- **Storefront BC + Elasticsearch/Kibana'yı geri getirme:** söküldü — arama Catalog pg-hibrit'e
  (pg_search BM25 + pgvector + RRF) katlanıyor. `query_storefront`/`search_products`/ES-DSL + vitrin
  event'leri (`ReviewSummaryChanged`/`ProductDiscountChanged`) GERİ EKLEME; keşif pg-hibrit'le gelir.
- **MCP yüzeyini parça parça geri getirme:** temiz-sayfa sökümüyle EC'den MCP tamamen çıktı
  (tool sabitleri, PRM, DLQ tool'ları, fasat aboneliği). Yüzey kararı REST first-party — tek tük
  tool geri ekleme yok; MCP dönerse bilinçli mimari karar + spec'le döner (tarihsel kurallar
  073/085 spec'lerinde ve AgentPlatform tool-description standardında durur).
- **`IConfiguration`'dan doğrudan okuma** (Options pattern istisnaları hariç).
- **Hassas kimlik/sır değerini insan-yüzeyde render etme** — ekrana/chat'e/API dönüşüne ve
  log/trace'e yazma. Kural ve gerekçe **Anayasa İlke VIII**'de (+ ADR
  [`adr-mcp-control-plane-no-secret-return.md`](docs/adr/adr-mcp-control-plane-no-secret-return.md));
  tarihsel örnek 087 makine-handoff onboarding'di (093'te söküldü; kural yaşıyor, örneksiz).
- **Yeni saga için ayrı orchestration servisi açma** (god-service) — saga sürecin sahibi BC'de host
  edilir. (049 Checkout.Orchestrator istisnası 092'de SÖKÜLDÜ — checkout artık koreografi; repoda
  orkestre saga örneği KALMADI. Gerçekten çok-adım/dallanan süreç gerekirse önce koreografiyi değerlendir.)
- **AgentPlatform IdP'sine geri dönme (005 — 084 maddesi TERSİNE DÖNDÜ):** kimlik makamı artık çözüm
  içinde (`src/others/Identity.Server`, ADR [`adr-identity-inward.md`](docs/adr/adr-identity-inward.md));
  AgentPlatform'daki kopyaya dokunma, SSO/dış-IdP bağımlılığını spec'siz geri getirme. DCR/consent/
  dış-agent istemci yüzeyini de geri ekleme (MCP söküm çizgisi).
- Genel kural: çözüme (`.slnx`) dahil olmayan klasör kapsam dışıdır. (Son örnek `CustomNopCommerce`
  referans kodu — 2026-10-31'de silindi; tarihçe git'te.)
