# CLAUDE.md

Claude Code'a bu repo'da rehberlik eder. **Gerçek-kaynak sırası:** kod + bu dosya >
Claude memory > Obsidian vault. Feature detayı BC haritasındaki `specs/*` yollarında.

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

**Commit öncesi** (CI yok, guard'lar elle koşar):

```bash
dotnet build && dotnet test
scripts/check-claude-spec-links.sh     # BC haritasındaki specs/* yolları gerçekten var mı
scripts/check-flow-links.sh            # FLOW.md domain-süreç anchor'ları (Anayasa İlke VII)
```

Yardımcı script'ler:

```bash
python3 scripts/build-flow-wall.py     # FLOW.md'lerden EventStorming duvarı (flow-wall.html, git'e girmez)
python3 scripts/build-miro-wall.py     # aynı duvarın Miro SVG'si (miro-wall.svg, git'e girmez); boarda basma Claude Miro MCP'yle
scripts/book-import/                   # dev veri hazırlığı: Kitapyurdu xlsx → kitapyurdu_to_books_json.py →
                                       # canonicalize_categories.py (+ commit'li category_overrides.json);
                                       # nihai seed Catalog Seeding/Data/books.json'da COMMIT'Lİ (091) — açılışta kendiliğinden dolar
```

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
**OpenIddict + ASP.NET Identity** (IdP, bu repoda değil — bkz. BC haritası) ·
**Scrutor** (DI) · **xUnit + Shouldly**. (MCP + Microsoft Agent Framework temiz-sayfa sökümüyle
kalktı — yüzey REST first-party'ye dönüyor, bkz. Yüzey modeli.)

## BC haritası

Her BC = kendi DB'si + şeması. Origin sütunu = BC'yi tanımlayan spec'in tam yolu (guard'lı); sonraki
feature'lar o feature'ın kendi spec'inde. Servisler `src/services/*`; destek `src/others`
(`Common`/`Shared`), `src/aspire` (`AppHost`/`ServiceDefaults`).

| Servis | DB | Ne yapar | Origin spec |
|---|---|---|---|
| `catalog` | catalogDb | Sade kitap künyesi (095): `Product` (kimlik Gtin/ISBN unique-index; ad/açıklama/fiyat/sayfa/çevirmen/dil/tarih; çok-yazar + tek yayınevi; **TEK leaf `CategoryId` + editoryal `RelatedCategoryIds`** — üst kategori üründe DENORMALIZE DEĞİL, yayın anında ağaçtan çözülür `CategoryNaming.ResolveChain`) + `Category` (kapalı 2 seviyeli ağaç: yalnız seed kurar, ebeveyn doğumda sabit, Create-only) + `Author`+`Publisher` + fiyat geçmişi (058 append-only). **Sku/Dimensions/Seo/ProductType/vitrin bayrakları/ProductTag SÖKÜLDÜ (095)**; 091 sürer: ürün CANLI doğar, fiyat>0 invariant. **Dış yüzey ŞU AN YOK** — REST first-party + pg-hibrit arama (096) gelecek. **Seed (091/095 iki-fazlı):** `Seeding/Data/books.json` (11438) → Faz A `SeedCategoryTree` (deterministik ağaç; ad-çakışmasında fail-fast) → Faz B satır satır (Gtin varsa atlar; ürün+`ProductAdded`+fat `ProductChangedEvent` — `Categories`+`CategoryIds`=leaf+ata — aynı commit). Kapak File'dan `CoverIngested` ile (`FileConsumers`→`SetImage`) | `specs/040-catalog-domain-extract` · `specs/091-catalog-json-seed` · `specs/095-catalog-book-domain` |
| `basket` | basketDb | Kalıcı sepet + kalem; anonim sahiplik (057) — `MergeFrom` domain'de VAR, yüzeyi YOK; stok kavramı yok (dijital pivot), süre yok (056); dış yüzey ŞU AN YOK (MCP söküldü) — kalan tüketici checkout gRPC + `OrderCompleted` temizliği | `specs/012-stock-reservation` |
| `order` | orderDb | Sipariş aggregate + yaşam döngüsü. 094 pay-by-link akışı (giriş yüzeyi MCP'yle söküldü, REST first-party'de yeniden açılacak; gRPC istemcileri + davranış durur): sepet (Basket gRPC) ile tutar (liste fiyatı — indirim 095'te söküldü) → Pending order → Payment BC'ye S2S intent+link (`PaymentIntentClient`) → `HostedUrl`; başarısızda order commit YOK; `/checkout/done` dönüş stub'ı. **Dijital pivot: stok ayağı KALKTI** — `PaymentSucceeded`→`Order.Confirm`+`OrderCompleted` fanout (Reviews+Library+Basket) / `PaymentFailed`→Cancel (`PaymentConsumers`, idempotent durum-guard) | `specs/028-checkout-saga` · `specs/094-ec-pay-by-link-rewire` |
| `payment` | paymentDb | `PaymentIntent` durum makinesi (kart alanı yok, TxRef unique). 094 pay-by-link: Order S2S intent+link gRPC (`PaymentIntentGrpcService`) + EC→PG `POST /payment-links` REST (`PgPaymentLinkClient`, X-Api-Key; `STORE_NOT_LIVE` fail-fast) + PG→EC HMAC webhook (`ProcessPaymentCallback`+`CallbackSignatureValidator`: fail-closed, `pgPaymentRef` eşle, anti-tamper amount guard, idempotent) → `PaymentSucceeded`/`PaymentFailed` fanout. Kart alanı HİÇ taşımaz (İlke VIII, yalnız opak `pgPaymentRef`). Merchant kimliği PG'de. Ödeme geçmişi okuma yüzeyi MCP'yle söküldü | `specs/077-hosted-cf-payment` · `specs/093-pg-pivot-teardown` · `specs/094-ec-pay-by-link-rewire` |
| `customer` | customerDb | AddressBook (≤1 varsayılan invariant'ı); izole, event YAYMAZ; Order'a varsayılan-adres gRPC'si (uykuda). Adres MCP tool'ları söküldü — dış yüzey ŞU AN YOK. **Wallet 076'da, merchant kimliği/onboarding (087) 093 PG pivotunda SÖKÜLDÜ** | `specs/022-wallet-address-book` · `specs/093-pg-pivot-teardown` |
| `reviews` | reviewsDb | Satın-alma şartlı yorum (kanıt `OrderCompleted`-fed lokal read-model); yorum HEMEN görünür, moderasyon YOK (088); özet (ortalama+adet) okuma anında hesaplanır. Yazma/okuma MCP yüzeyi söküldü — aggregate davranışı durur, yüzey REST first-party'de | `specs/044-product-reviews` |
| `library` | libraryDb | Kullanıcı-ürün ilgi kayıtları; fiyat alarmı (yaşayan abonelik, email snapshot) + `NotificationRecord` izi; `ProductChangedEvent.OldPrice` tetiği → alarm başına `PriceAlarmTriggered` (tüketicisi NotificationAgent SÖKÜLDÜ — event şimdilik boşa yayılır, mail hattı ölü); alarm MCP tool'ları söküldü; `UserPurchase` (kişisel satın-alma birikimi) `OrderCompleted` tüketir (`OrderConsumers`, idempotent upsert) | `specs/060-price-alarm-mail` |
| `file` | fileDb | Kapak **kayıt defteri** (082); `FileAsset` (ImageName=ISBN tekil/değişmez unique-index + metadata) + nested `FileStorageLocation` (çoklu fiziki depo: R2/Local/…, upsert invariant). Fiziki bit `IFileStore` ardında (`S3FileStore`→R2; byte DB'de DEĞİL); URL provider-agnostik lokal çözülür (`CoverUrlResolver`, StorageFilePath=key + config-base; full URL saklanmaz). `GET /files/v1/covers/{isbn}` anonim serve; S2S `POST /internal/files` (yaz+kayıt) + `/resolve` (batch, 0 dış çağrı) + `GET .../locations`; idempotent R2 backfill (config-gated). **Kapak kablosu (083/091):** `ProductAdded` tüketir (`CatalogConsumers`; R2'de yoksa yerel staging'den yükle+kayıt) → `CoverIngested(isbn,url)` yayar → Catalog `Product.ImageUrl` doldurur; açılış kapak migration'ı kaynağı = commit'li seed `books.json` (`JsonCoverSource`; xlsx emekli) | `specs/081-cover-image-store` |
| `identity-server` | identityDb | **Bu repoda DEĞİL** — `AgentPlatform` repo'sunda uygulama-nötr platform IdP (084); ECommerce `AppRegistry` config'inde kayıtlı relying party ("app #1"). OpenIddict + ASP.NET Identity; OIDC/OAuth + RBAC; DCR + tek consent + revocation (061). Nötr auth kablosu (`IdentityOption` + `AddAuthenticationAndAuthorizationExtension`) = `Platform.Auth` NuGet paketi (yerel feed; namespace `Common.*` korunur). ECommerce IdP'ye dış servis olarak bakar (issuer URL `IdentityOption.Address`); AppHost proje-ref YOK | `../AgentPlatform` · `specs/084-platform-idp` |

(Söküldü — 2026-10-30 temiz-sayfa: `stock` [dijital/PDF pivotu — envanter kavramı kalktı],
`storefront`+ES [arama Catalog pg-hibrit'e katlanacak], `notification-agent`+`mail-mcp` [MCP sökümüyle],
EC'deki TÜM `/mcp` uçları + AgentPlatform fasat aboneliği [yüzey REST first-party'ye dönüyor].
2026-10-31 (095 branch'i): `discount` [kampanya/indirim modeli kullanıcı kararıyla öldü — checkout
tutarı = liste fiyatı; `discount.*` scope'ları, proto ve `discount.events` kuyruğu da gitti; tarihçe
`specs/079-discount-campaigns`]. Gerekçeler ADR + `specs/*` tarihinde durur.)

## Yüzey modeli

**Temiz-sayfa geçişi (2026-10-30):** BYO-agent/MCP duruşu TERK EDİLDİ — EC'deki tüm `/mcp` uçları,
tool sabitleri (`McpToolNames/Descriptions`), PRM keşfi ve DLQ admin tool'ları söküldü. Hedef yüzey:
**first-party React ekran + REST API'ler + local LLM danışman** (kişiselleştirilmiş keşif sitesi);
arama Catalog'da pg-hibrit (pg_search BM25 + pgvector + RRF). İkisi de HENÜZ YOK — bugün dış
müşteri/admin yüzeyi kapalı, domain davranışları aggregate'lerde duruyor.

- **Yaşayan HTTP yüzey:** File kapak serve/S2S (`/files/v1/covers`, `/internal/files`, `/resolve`) ·
  PG→EC `/payments/callback` HMAC webhook (dış-sağlayıcı callback istisnası) · Order `/checkout/done`
  dönüş stub'ı · auth (Identity OIDC — dış repo).
- **Yaşayan senkron kontratlar (S2S):** Order→Basket `GetBasketItems` gRPC · Order→Payment
  `CreateIntentAndLink` gRPC · EC→PG `POST /payment-links` REST (X-Api-Key) ·
  Order→Customer varsayılan-adres gRPC (uykuda).
- **Yetki iskeleti korunur:** `Platform.Auth` kablosu + `[RequiredScope]` + `AuthorizationScopes`
  kapalı registry yerinde — REST first-party açılınca aynı scope'larla korunacak. IdP ve rol→scope
  yönetimi AgentPlatform'da (084/085 düzeni değişmedi).
- **Ölü-mesaj (DLQ):** Wolverine retry→error-queue davranışı sürer; operatör TOOL yüzeyi söküldü —
  inceleme/replay şimdilik elle (RabbitMQ UI / DB), REST admin yüzeyiyle geri gelebilir.

## Ürün yazım yolu (050: first-party — mallar mağazanın)

Tek giriş kaldı (091: Excel import + yayın makinesi, temiz-sayfa: admin MCP tool'ları SÖKÜLDÜ):

1. **Açılış seed'i (091):** commit'li `Seeding/Data/books.json` (Kitapyurdu 11438) →
   `BookSeedHostedService` satır satır (Gtin varsa ATLAR — admin düzenlemesi ezilmez, upsert bilinçli
   reddedildi); ürün CANLI doğar.
2. Elle oluşturma/düzenleme: yüzey ŞU AN YOK — REST first-party admin'le yeniden açılacak
   (aggregate davranışları duruyor).

**Taslak/yayın/gizleme ekseni yok (091):** fiyat>0 var oluş invariant'ı (`Product.Create`/`SetPrice`);
`ProductChangedEvent.IsDeleted` kontratta durur, hep `false`. Yeni üründe Catalog `ProductChangedEvent`
yayar → Library (fiyat alarmı) tüketir. **Silme YOK** (016).

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

**Yeni yüzey (REST first-party):** desen henüz kurulmadı — ilk REST feature'ı spec-kit'ten geçer ve
deseni kurar (slice + endpoint-extension + `[RequiredScope]`; conventions.md VSA kuralları aynen geçerli).

**Yeni BC:** `src/services/<bc>/<Bc>.Api` + kendi Postgres DB'si/şeması + `tests/<Bc>.Api.Tests`
(ikisi de `.slnx`'e) · AppHost'ta servis + DB kaydı · sürümler `Directory.Packages.props` ·
`Extensions/MessagingExtensions.cs` (Wolverine kablosu) · entegrasyon event'lerle, senkron
çağrı EKLEME (meşru istisnalar yukarıda sayılı) · FLOW.md domain süreci (İlke VII) · Origin spec'i BC
haritasına ekle (`check-claude-spec-links.sh` doğrular).

## Projeye özel yetki + tuzaklar

- **RBAC (scope; İlke V):** `AddAuthenticationAndAuthorizationExtension(config, ...scopes)`;
  `KnownScopes` kapalı registry, rol→scope map DB'de ve AgentPlatform IdP'nin kendi
  server-rendered `/Admin/*` ekranlarından yönetilir (cookie auth — Anayasa İlke V istisnası). Register → `customer` rolü. `[RequiredScope]` Wolverine mesaj
  handler'larına da uygulanır. Identity **HTTPS zorunlu**.
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
- **Kimlik makamını ECommerce'e geri gömme (084):** IdP `AgentPlatform`'da. AppHost'a IdP proje-ref'i
  EKLEME, scope/client/rol'ü koda gömme (yenisi `AgentPlatform` `AppRegistry` config'ine). Nötr auth
  kablosu `Platform.Auth` paketinde — `Common`'a geri taşıma.
- **`src/otherProjects/CustomNopCommerce`'e dokunma** — `.slnx` dışı referans/deneme kodu
  (git-takipli ama çözümün parçası değil). Genel kural: çözüme dahil olmayan klasör kapsam dışıdır.
