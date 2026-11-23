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
**Scrutor** (DI) · **xUnit + Shouldly** · **Microsoft.Agents.AI + MCP (014 GERİ — ScoutAgent)**:
Kâşif sohbet agent'ı OpenAI gpt-4o-mini tool-calling'le BC'lerin query-only `/mcp` tool'larını seçer
(first-party agent-içi, ADR adr-mcp-inward-agent; dış/BYO MCP HÂLÂ kapalı). (096 Ollama/embedding
omurgası 099 öncesi SÖKÜLDÜ — semantik ayak ileride yeni spec'le.)

## BC haritası

Her BC = kendi DB'si + şeması. (Parantezli numaralar eski spec numaralarıdır — tarihçe Obsidian
damıtmasında.) Servisler `src/services/*`; destek `src/others`
(`Common`/`Shared`), `src/aspire` (`AppHost`/`ServiceDefaults`).

| Servis | DB | Ne yapar |
|---|---|---|
| `catalog` | catalogDb | Sade kitap künyesi (095): `Product` (kimlik Gtin/ISBN unique-index; ad/açıklama/fiyat/sayfa/çevirmen/dil/tarih; çok-yazar + tek yayınevi + cilt tipi + popülerlik sayaçları (001, dış-kaynak anlık görüntüsü)) + `Author`+`Publisher`. **İçerik-temelli keşif ekseni (021 — kategori + 009 etiket SÖKÜLDÜ):** kapalı-sözlük taksonomi `Genre`/`Moods`/`AudienceLevel`/`Themes`+`EnrichmentConfidence` (değerler Türkçe sözlük `BookTaxonomy`; yazma anında `SetEnrichment` kanonikleştirir — takma-ad eşle, karşılıksız düş, geçersiz→"belirsiz"; okuma anında facet) + eser kimliği `WorkKey` (`WorkKeyNormalizer.Derive` — aynı eserin baskıları tek anahtara; benzerlik tekilleştirmesi, iç alan). `Category` aggregate/`CategoryNaming`/ağaç GİTTİ. **Satışa-uygunluk modeli (001):** ürün fiyatsız/yazarsız doğabilir (katalogda görünür, satılamaz); `IsSalable` TEK tanım = fiyat>0 ∧ stok>0 ∧ ≥1 yazar (stok bacağı `StockCount` — `StockChanged`-fed kopya, 002; `DiscountRate` etkin-oran kopyası — `ProductDiscountChanged`-fed, 003, IsSalable'a girmez; tüketiciler `StockConsumers`/`DiscountConsumers` DLQ-destekli); fiyat sonradan `SetPrice`'la atanır/serbest değişir, >0 guard (097 "doğumda sabit" REVİZE edildi; 058 fiyat geçmişi sökük kalır). **096 embedding omurgası SÖKÜLDÜ (099 öncesi, kullanıcı kararı):** `ProductEmbedding`+Ollama hattı+reconcile süreci gitti — semantik arama ayağı ileride yeni spec'le, bilinçli kurulur. **İlk REST admin yüzeyi AÇIK (004):** Excel import uçları (`/api/admin/catalog/imports` — gateway'den, X-Admin-Api-Key çift kat) — ürün giriş yolu: çok-sayfalı xlsx (sayfa=yayınevi), İKİ-FAZ: önce dry-run (hiç yazmaz, sebepli rapor) → admin onayı (`POST .../{id}/confirm`) → INSERT-ONLY yazım (ISBN varsa dokunma); arka plan `ImportJobWorker` (durum-güdümlü polling, mesaj/kuyruk tetiği YOK); `ImportJob` aggregate'i; yüklenen dosya silinmez. **Kapak dönüşü GERİ (004, 097 tersine):** `CoverIngested` tüketilir (`FileConsumers`) → `ImageUrl` dolar. **v11 import (021):** taksonomi + WorkKey yazılır; `ImportJob.Enriched` sayacı; `ProductAdded.InStock` ikili sinyali (Stock false→0/true→20-50). **Arama facet (021):** tür/ton/seviye/konu çoklu-değer filtre `@@@` DIŞI jsonb (`= ANY`/`?|` — token çarpışması önlenir) + BM25 metin grubuna taksonomi + `OnlyInStock`; sözlük-dışı değer red (`TaxonomyFacetValidator`→REST 400). **Benzerlik (021):** `similar_books` query-only MCP tool (yalnız ScoutAgent) — konu+ton BM25 örtüşme + boost (`SimilarBooksOptions`) + hard filtre (dil/seviye grubu) + `DISTINCT ON WorkKey` + stok-önce; "ne anlatıyor" CEVAPLANMAZ (özet tool'u yok, karta yönlendir) |
| `basket` | basketDb | Kalıcı sepet + kalem; sahip = login kullanıcı (anonim sahiplik 057 + `MergeFrom` SÖKÜLDÜ — sepete atmak login ister); stok kavramı yok (dijital pivot), süre yok (056); dış yüzey ŞU AN YOK (MCP söküldü) — kalan tüketici checkout gRPC + `OrderCompleted` temizliği |
| `order` | orderDb | Sipariş aggregate + yaşam döngüsü. 094 pay-by-link akışı (giriş yüzeyi MCP'yle söküldü, REST first-party'de yeniden açılacak; gRPC istemcileri + davranış durur): sepet (Basket gRPC) ile tutar (liste fiyatı — indirim 095'te söküldü) → Pending order → Payment BC'ye S2S intent+link (`PaymentIntentClient`) → `HostedUrl`; başarısızda order commit YOK; `/checkout/done` dönüş stub'ı. **Dijital pivot: stok ayağı KALKTI** — `PaymentSucceeded`→`Order.Confirm`+`OrderCompleted` fanout (Reviews+Library+Basket) / `PaymentFailed`→Cancel (`PaymentConsumers`, idempotent durum-guard) |
| `payment` | paymentDb | `PaymentIntent` durum makinesi (kart alanı yok, TxRef unique). 094 pay-by-link: Order S2S intent+link gRPC (`PaymentIntentGrpcService`) + EC→PG `POST /payment-links` REST (`PgPaymentLinkClient`, X-Api-Key; `STORE_NOT_LIVE` fail-fast) + PG→EC HMAC webhook (`ProcessPaymentCallback`+`CallbackSignatureValidator`: fail-closed, `pgPaymentRef` eşle, anti-tamper amount guard, idempotent) → `PaymentSucceeded`/`PaymentFailed` fanout. Kart alanı HİÇ taşımaz (İlke VIII, yalnız opak `pgPaymentRef`). Merchant kimliği PG'de. Ödeme geçmişi okuma yüzeyi MCP'yle söküldü |
| `customer` | customerDb | AddressBook (≤1 varsayılan invariant'ı); izole, event YAYMAZ; Order'a varsayılan-adres gRPC'si (uykuda). Adres MCP tool'ları söküldü — dış yüzey ŞU AN YOK. **Wallet 076'da, merchant kimliği/onboarding (087) 093 PG pivotunda SÖKÜLDÜ** |
| `reviews` | reviewsDb | Satın-alma şartlı yorum (kanıt `OrderCompleted`-fed lokal read-model); yorum HEMEN görünür, moderasyon YOK (088); özet (ortalama+adet) okuma anında hesaplanır. Yazma/okuma MCP yüzeyi söküldü — aggregate davranışı durur, yüzey REST first-party'de |
| `library` | libraryDb | Kullanıcı-ürün ilgi kayıtları; **fiyat alarmı UYKUDA (097):** fiyat değişmezliğiyle tetik öldü — `ProductChangedEvent` aboneliği söküldü, `PriceAlarm` modeli + `PriceAlarmTriggered` kontratı yayıncısız durur, `NotificationRecord` izi kalır; `UserPurchase` (kişisel satın-alma birikimi) `OrderCompleted` tüketir (`OrderConsumers`, idempotent upsert) |
| `stock` | stockDb | Kurgusal envanter (002, fiziki dönüş): `StockItem` (ProductId unique-index + OnHand≥0; mutlak atama `SetQuantity`, satış düşümü `Decrease` — yetersizse red). Adedin TEK yazarı; her değişimde `StockChanged(productId, onHand)` fanout → Catalog adet kopyası. Rezervasyon YOK (056 sürer); dış yüzey + checkout bağlantısı sonraki dilimler |
| `discount` | discountDb | Kampanya modeli (003, dönüş — tarihçe 079): `Campaign` (tek ürün + oran %1-%90 + zaman penceresi + iptal; etkinlik `IsActiveAt` — zaman dışarıdan). Üründe ≤1 etkin indirim, son-gelen-kazanır (`CampaignSelection`). İndirim liste fiyatını EZMEZ — `ProductDiscountChanged(productId, rate?)` kontratı + Catalog oran kopyası hazır; YAYINCI yürürlük süreci (`CampaignWindowProcess`) + dış yüzey + checkout bağlantısı SONRAKİ dilimler |
| `file` | fileDb | Kapak **kayıt defteri** (082); `FileAsset` (ImageName=ISBN tekil/değişmez unique-index + metadata) + nested `FileStorageLocation` (çoklu fiziki depo: R2/Local/…, upsert invariant). Fiziki bit `IFileStore` ardında (`S3FileStore`→R2; byte DB'de DEĞİL); URL provider-agnostik lokal çözülür (`CoverUrlResolver`, StorageFilePath=key + config-base; full URL saklanmaz). `GET /files/v1/covers/{isbn}` anonim serve; S2S `POST /internal/files` (yaz+kayıt) + `/resolve` (batch, 0 dış çağrı) + `GET .../locations`; idempotent R2 backfill (config-gated). **Kapak kablosu (083/091):** `ProductAdded` tüketir (`CatalogConsumers`; **004:** `CoverSourceUrl` doluysa ve R2'de yoksa dış adresten İNDİRİR, R2'de varsa atlar; yoksa eski staging yolu) → `CoverIngested(isbn,url)` yayar — **Catalog tüketicisi GERİ (004):** `FileConsumers` → `Product.SetImage`; açılış kapak migration'ı KAPALI kalır (098; R2'deki kapaklar + kayıt defteri duruyor) |
| `personalization` | personalizationDb | **Kullanıcı arama sinyali deposu (012) + geçmiş yüzeyi (013):** login aramadan gelen `UserSearched`'i (Catalog arama ucu yayınlar, fanout) tüketir (`CatalogSearchConsumers`) → `UserSearchRecord` (ham log: userId/query/mode/resultCount/searchedAt; AggregateRoot DEĞİL, Marten belgesi `user_search_record`, şema `personalization`; `UserId` index). **013: İLK HTTP yüzeyi** — gateway `/api/personalization/v1/search-history` (GET tekilleştirilmiş+sayfalı · DELETE item · DELETE all), hepsi **auth-zorunlu** (anonim 401, kimlik JWT `sub`'tan, named-scope YOK — kişisel-veri erişimi); tekilleştirme/Türkçe-fold okuma anında saf `SearchHistoryProjection` (depo ham kalır). **İnvariant: yazım append-only, 013 kullanıcı KENDİ kaydını HARD siler** (içerik mutasyonu yok). JWT inline (Catalog 012 deseni, `ValidateAudience=false`). Öneri/ML/ONNX YOK (sonraki dilimler). Anonim arama kaydolmaz. |
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
arama Catalog'da **ParadeDB pg_search BM25 TEK motor** (018 birleşik arama; metin+filtre+sort+browse
tek `SearchAsync` → REST `/products/search` + MCP `search_books` aynı yol; semantik/pgvector/RRF 018'de
SÖKÜLDÜ, ileride yeni spec). React ekran HENÜZ YOK — domain + arama motoru aggregate/query'de duruyor.

- **Gateway (004, YARP `src/others/Gateway`):** React'ın tek origin'i; rotalar config'te
  (`/api/admin/catalog/**` admin-anahtar korumalı, `/api/catalog/**` + `/files/**` anonim). Kenar
  yetki Dilim-1'de `X-Admin-Api-Key` (Aspire secret parametresi `gateway-admin-api-key`; Catalog
  aynı anahtarı İKİNCİ kat doğrular — `AdminApiKeyEndpointFilter`); Dilim 2'de JWT'ye evrilir.
  ForwardedHeaders ServiceDefaults'ta tek yerde.
- **Arama kaydı Catalog'da (012 — app-BFF SÖKÜLDÜ):** React doğrudan gateway `/api/catalog/products/search`'e gider
  (orkestratör katmanı kaldırıldı). Catalog arama ucu OPSİYONEL JWT (ValidateAudience=false — 007 deseni; anonim
  serbest, `RequireAuthorization` YOK) ile login userId'yi çözer; sonucu döndükten SONRA login+dolu-sorguda
  `UserSearched` yayınlar (endpoint-seviyesi, query saf okuma kalır; fanout → Personalization). Publish ayrık +
  hata-yutar → arama sonucunu etkilemez. ChatAgent/feed ileride.
- **Yaşayan HTTP yüzey:** Catalog admin import uçları (004, gateway'den) · File kapak serve/S2S
  (`/files/v1/covers`, `/internal/files`, `/resolve`) · PG→EC `/payments/callback` HMAC webhook
  (dış-sağlayıcı callback istisnası) · Order `/checkout/done` dönüş stub'ı · **ScoutAgent sohbet
  (014, gateway `/api/chat/**` → `scout-agent`): POST `/messages` (opsiyonel bearer), GET
  `/conversations*` (login)** · auth (Identity OIDC — dış repo).
- **Agent-içi MCP (014):** ScoutAgent `/mcp` query uçlarını tüketir (MVP: Catalog `search_books`/
  `get_book`; sonraki dilim Basket/Order/Library/Customer/Reviews/Personalization). Yalnız okuma;
  kişisel uçta token taşınır. Bkz. ADR adr-mcp-inward-agent + `src/agents/ScoutAgent.Api/FLOW.md`.
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
- **Marten tr-TR noktasız ı — KÖKTEN ÇÖZÜLDÜ (010, `Directory.Build.props`):**
  `<InvariantGlobalization>true</InvariantGlobalization>` repo-geneli → `ToLower('I')` hep noktalı
  `i` → Marten tablo adları hiç ı'lı olmaz (eski `mt_doc_stockıtem` 42P07 crashloop sınıfı biter);
  bonus: sayı `ToString()` hep "." (ClosedXML "0,285"→285 parse bug'ı da biter). Artık elle
  `DocumentAlias` ZORUNLU DEĞİL (ama zararsız). Yeni belge eklerken ı'yı DÜŞÜNME — global kural halleder.
  (Tarihsel: önce elle `opts.Schema.For<T>().DocumentAlias("…")` I'sız alias gerekiyordu — unutuluyordu;
  bozuk ı'lı tablo bir kez DROP edilir, Marten noktalı adı yeniden kurar.)
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
- **Storefront BC + Elasticsearch/Kibana'yı geri getirme:** söküldü — arama Catalog'da ParadeDB
  pg_search BM25 TEK motor (018; semantik/pgvector/RRF söküldü). `query_storefront`/`search_products`/
  ES-DSL + vitrin event'leri (`ReviewSummaryChanged`/`ProductDiscountChanged`) GERİ EKLEME.
- **Catalog araması için pgvector/embedding/RRF/semantik geri ekleme (018):** birleşik BM25 tek motor;
  semantik ayak bilinçli söküldü, ancak yeni spec'le döner. `ProductEmbedding` tablosu/kNN/füzyon EKLEME.
- **MCP'yi DIŞ/BYO yüzey olarak geri getirme:** temiz-sayfa sökümüyle dış-agent MCP yüzeyi çıktı
  (DCR, consent, PRM keşfi, DLQ operatör tool'ları, fasat aboneliği) — bunlar GERİ GELMEZ. **İSTİSNA
  (014, ADR adr-mcp-inward-agent):** first-party ScoutAgent'ın tükettiği **agent-içi query-only MCP**
  GERİ GELDİ (İlke I MCP'yi meşru kanal sayar; "MCP yalnız agent tüketir"). BC'ler `/mcp` query ucu
  açar, yalnız ScoutAgent client bağlanır, YAZMA tool'u yok. Dış yüzey yine bilinçli karar + spec ister.
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
