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
scripts/check-agent-query-schema.sh    # ES index mapping ↔ query_storefront description alan/tip drifti (086)
```

Yardımcı script'ler:

```bash
scripts/kibana-service-token.sh        # Kibana (dev) için ES service-account token → AppHost user-secrets
python3 scripts/build-flow-wall.py     # FLOW.md'lerden EventStorming duvarı (flow-wall.html, git'e girmez)
python3 scripts/build-miro-wall.py     # aynı duvarın Miro SVG'si (miro-wall.svg, git'e girmez); boarda basma Claude Miro MCP'yle
scripts/book-import/                   # dev veri hazırlığı: OpenLibrary'den çek → temizle → şekillendir (3 .py);
                                       # nihai seed Catalog Seeding/Data/books.json'da COMMIT'Lİ (091) — açılışta kendiliğinden dolar
```

- **Sistemi hep Aspire AppHost'tan başlat**, tek servis değil — servisler birbirini/DB/RabbitMQ'yu
  service discovery + conn-string enjeksiyonuyla bulur; tek API bağımsız açılmaz.
- **Marten şeması otomatik kurulur** (`ApplyAllDatabaseChangesOnStartup`) — migration komutu yok.
- **OpenAI kullanan servisler** (NotificationAgent, Storefront — embedding, 067) açılışta fail-fast:
  `dotnet user-secrets set OpenAI:ApiKey <k> --project <proj>` (+ `OpenAI:Model`, ör. gpt-4o-mini).
- **Kibana ES'e bağlanmıyorsa** service token yoktur veya ES volume'ü resetlenmiştir (Kibana 8.x
  `elastic` superuser'ını reddeder) → AppHost ayaktayken `scripts/kibana-service-token.sh`.
- **Paket sürümleri yalnız `Directory.Packages.props`'ta** (Central Package Management); `.csproj`
  `PackageReference`'ı sürümsüz listeler. Sürüm ekle/değiştir → yalnız props.

## Teknoloji

.NET 10 (`Nullable`+`ImplicitUsings` açık) · **Marten** (Postgres = document/event store, Newtonsoft,
non-public setter+ctor) · **Wolverine** (in-proc bus `IMessageBus` + RabbitMQ fanout; handler assembly
taramasıyla) · **Elasticsearch** (storefront arama — turkish analyzer + dense_vector kNN, 086) +
Kibana (dev-only görünürlük) · **HybridCache + Redis** (AOP cache L1/L2 yalnız okuma; boşaltma
handler-içi atomik publish + epoch-key, 090 — native tag KULLANILMAZ, bkz. conventions) ·
**OpenIddict + ASP.NET Identity** (IdP, bu repoda değil — bkz. BC haritası) · **YARP** gateway ·
**MCP** (her ürün servisi `/mcp`; tek dış giriş AgentPlatform fasadı) · **Microsoft Agent Framework** +
`Microsoft.Extensions.AI` (NotificationAgent) · **Scrutor** (DI) · **xUnit + Shouldly**.

## BC haritası

Her BC = kendi DB'si + şeması. Origin sütunu = BC'yi tanımlayan spec'in tam yolu (guard'lı); sonraki
feature'lar o feature'ın kendi spec'inde. Servisler `src/services/*`; destek `src/others`
(`Common`/`Shared`), `src/aspire` (`AppHost`/`ServiceDefaults`), `src/agents`.

| Servis | DB | Ne yapar | Origin spec |
|---|---|---|---|
| `catalog` | catalogDb | Zengin `Product`+`Category`+`Author`+`Publisher`+`ProductTag`+`SpecificationAttribute` (kitap künyesi: çok-yazar + tek yayınevi); admin düzenleme + fiyat geçmişi (058: append-only `ProductPriceChange`). **091: taslak/yayın/gizleme YOK** — ürün CANLI doğar, fiyat>0 var oluş invariant'ı, Gtin unique-index. **Müşteri keşif yüzeyi** (envanter otoritesi — vitrin aramasından ayrı): `get_product` · `search_products` · `get_price_history` · `list_categories` · `list_authors` · `list_publishers`. **Admin:** ürün/kategori/yazar/tag/spec tool'ları + `AdminActionLog` izi (`specs/070-admin-mcp-surface`); `admin_republish_products` = yeni downstream BC ya da sıfırlanmış read-model için `ProductChangedEvent` backfill (replay; tüm tüketiciler idempotent, `OldPrice` hep null). **Seed doluşu (091):** commit'li `Seeding/Data/books.json` → `BookSeedHostedService` açılışta satır satır (Gtin varsa atlar; ürün+`ProductAdded`(satır stok)+`ProductChangedEvent` aynı commit). Kapağı File'dan `CoverIngested` ile alır (`FileConsumers`→`SetImage`) | `specs/040-catalog-domain-extract` · `specs/091-catalog-json-seed` |
| `basket` | basketDb | Kalıcı sepet + kalem; anonim sahiplik (057) — `MergeFrom` domain'de VAR, yüzeyi YOK; stok tutmaz, süre yok (056: rezervasyonsuz), stok gerçeği checkout'ta; yüzey MCP-only + checkout gRPC | `specs/012-stock-reservation` |
| `order` | orderDb | Sipariş aggregate + yaşam döngüsü; orchestrator'dan broker Confirm/Cancel; hosted-CF ödeme yolu (`start_payment` — sepet+adres oku, Pending order, Payment S2S hosted link; 077); `PaymentSucceeded`→StartCheckout / `PaymentFailed`→Cancel tüketir; Confirm'de `OrderCompleted` fanout → Reviews + Library | `specs/028-checkout-saga` |
| `checkout` | checkoutDb | Broker-only checkout sağası (`CheckoutProcess`, ayrı servis); ödeme ÖNCEDEN alınır (077 hosted-CF) → CommitStock → Confirm → ClearBasket; StartCheckout OrderId dolu (`CheckoutId=OrderId`); CommittingStock'ta LIFO telafi + watchdog | `specs/049-checkout-orchestrator` |
| `payment` | paymentDb | Hosted-CF ödeme (077): `PaymentIntent` (kart alanı yok); PG hosted link (`PgHostedPaymentClient`, MerchantKey S2S) + HMAC callback (`CallbackSecret` ayrı) → `PaymentSucceeded`/`PaymentFailed` fanout; terk-timer `ScheduleAsync`→Expire; TxRef unique idempotent | `specs/077-hosted-cf-payment` |
| `stock` | stockDb | `ProductStock` (OnHand); ilk stok `ProductLinked`'ten doğar; checkout düşümü broker'dan (056); `get_stock` anonim; admin `admin_set_stock` (mutlak) / `admin_adjust_stock` (artır-azalt) / `admin_list_all_stock` + `AdminActionLog` izi (058, 070); negatif guard aggregate'te (`Adjust`/`SetQuantity` invariant'ı) | `specs/014-supplier-stock-authority` |
| `storefront` | storefrontDb (Marten event-log) + Elasticsearch | **086: CQRS + Event Sourcing** — Postgres read-model DEĞİL, ürün-stream event-log (gerçek-kaynak); dört kaynak event (Catalog/Stock/Reviews/Discount) stream'e append, **async projection** (`StorefrontProjection`) katlayıp **Elasticsearch** doc'u yazar/siler (satılabilirse; FR-008 projeksiyon-zamanı dışlama). Okuma yolu asistan: TEK tool `query_storefront`, ham **ES Query DSL** alır (text+fuzzy+kNN+filtre tek sorguda; `{{EMBED}}`→knn vektör); minimal rail = sabit index + size≤50 + `_source` whitelist + timeout; `AgentQueryLog` izi; embedding ES `dense_vector`. Açılışta index yoksa kurulur (`EnsureAsync`); soğuk başlangıç = dev full-reset + Catalog `admin_republish_products`; ES-seviyesi elle reindex BACKLOG. 070 playbook'un kanonik evi bu tool'un Description'ı | `specs/003-storefront-read-model` · `specs/086-storefront-elasticsearch-search` |
| `customer` | customerDb | Wallet (tokenize kart, PAN yok; kart YAZMA yüzeyi yok — yalnız okuma + payment-context) + AddressBook; izole, event YAYMAZ. Müşteri: `list_cards` · `list_addresses`/`add_address`/`update_address`/`remove_address`/`set_default_address`. **087 makine-handoff merchant onboarding:** kayıt store-başlatır S2S register (bootstrap key), credential PG→store HMAC-callback'le gelir (`CallbackSignatureValidator`, 077 aynası), kullanım gRPC; `admin_start_onboarding` · `admin_onboarding_status` · `admin_get_merchant_status` · `admin_reissue_merchant_key`. Sır hiçbir insan-yüzeyde render EDİLMEZ (Yapma listesi) | `specs/022-wallet-address-book` · `specs/087-merchant-id-visibility` |
| `reviews` | reviewsDb | Satın-alma şartlı yorum (`check_review_eligibility` → `submit_review` → `get_reviews`); yorum HEMEN görünür, moderasyon YOK (088); özet event → Storefront | `specs/044-product-reviews` |
| `discount` | discountDb | Admin kampanya indirimi (079): süzgeç (kategori/yazar/yayınevi/tek-kitap) → yüzde; kitap-başına TEK indirim, vitrine event'le iter, süre dolunca temizler; fiyat TUTMAZ (yüzde otoritesi — etkin fiyatı tüketici hesaplar); Order'a gRPC aktif-yüzde; `admin_create_campaign`/`admin_list_campaigns`/`admin_cancel_campaign`; müşteri yüzeyi YOK — indirim vitrinde `query_storefront`'tan görünür | `specs/079-discount-campaigns` |
| `library` | libraryDb | Kullanıcı-ürün ilgi kayıtları; fiyat alarmı (yaşayan abonelik, email snapshot) + `NotificationRecord` izi; `ProductChangedEvent.OldPrice` tetiği → alarm başına `PriceAlarmTriggered`; `create_price_alarm`/`get_price_alarm`/`remove_price_alarm`; `UserPurchase` (kişisel satın-alma birikimi; 086'da Storefront'tan taşındı) `OrderCompleted` tüketir (`OrderConsumers`, idempotent upsert) | `specs/060-price-alarm-mail` |
| `file` | fileDb | Kapak **kayıt defteri** (082); `FileAsset` (ImageName=ISBN tekil/değişmez unique-index + metadata) + nested `FileStorageLocation` (çoklu fiziki depo: R2/Local/…, upsert invariant). Fiziki bit `IFileStore` ardında (`S3FileStore`→R2; byte DB'de DEĞİL); URL provider-agnostik lokal çözülür (`CoverUrlResolver`, StorageFilePath=key + config-base; full URL saklanmaz). `GET /files/v1/covers/{isbn}` anonim serve; S2S `POST /internal/files` (yaz+kayıt) + `/resolve` (batch, 0 dış çağrı) + `GET .../locations`; idempotent R2 backfill (config-gated). **Kapak kablosu (083/091):** `ProductAdded` tüketir (`CatalogConsumers`; R2'de yoksa yerel staging'den yükle+kayıt) → `CoverIngested(isbn,url)` yayar → Catalog `Product.ImageUrl` doldurur; açılış kapak migration'ı kaynağı = commit'li seed `books.json` (`JsonCoverSource`; xlsx emekli). **MCP yüzeyi YOK** (tek BC) | `specs/081-cover-image-store` |
| `gateway` | — | YARP reverse proxy; tek giriş — yalnız MCP + PRM rotaları | — |
| `notification-agent` | — | Fiyat alarmı maili (DB'siz worker); `PriceAlarmTriggered`→LLM compose→Mail.Mcp `send_mail`→`NotificationSent` | `specs/060-price-alarm-mail` |
| `mail-mcp` | — | Standalone MCP server; tek tool `send_mail` (MailKit→Mailpit); yalnız NotificationAgent tüketir | `specs/060-price-alarm-mail` |
| `identity-server` | identityDb | **Bu repoda DEĞİL** — `AgentPlatform` repo'sunda uygulama-nötr platform IdP (084); ECommerce `AppRegistry` config'inde kayıtlı relying party ("app #1"). OpenIddict + ASP.NET Identity; OIDC/OAuth + RBAC; DCR + tek consent + revocation (061). Nötr auth kablosu (`IdentityOption` + `AddAuthenticationAndAuthorizationExtension`) = `Platform.Auth` NuGet paketi (yerel feed; namespace `Common.*` korunur). ECommerce IdP'ye dış servis olarak bakar (issuer URL `IdentityOption.Address`); AppHost proje-ref YOK | `../AgentPlatform` · `specs/084-platform-idp` |
| _(MCP fasadı)_ | — | **Bu repoda DEĞİL** — tek müşteri+admin MCP fasadı (DB'siz proxy) AgentPlatform'ın kendi Aspire host'unda koşar. EC servislerinin `/mcp` uçları KORUNUR, fasada sabit mutlak URL'lerle downstream olur. Dış AI istemcisi platform MCP girişine bağlanır (Anayasa İlke III: tek MCP girişi). Tarihsel tasarım `specs/073-customer-mcp-facade` + `specs/085-single-mcp-surface`; taşıma AgentPlatform 001 | `specs/073-customer-mcp-facade` · `specs/085-single-mcp-surface` |

## Yüzey modeli

- **Agent-only / BYO-agent.** Mağaza ne görsel ekran ne kendi sohbet agent'ı host eder. Müşteri
  **kendi AI istemcisiyle** (Claude Desktop vb.) AgentPlatform MCP fasadının **TEK `/mcp` ucuna**
  bağlanır (`specs/085-single-mcp-surface`: tek login, upfront); tool'lar alt BC `/mcp`'lerinden toplanır, çağrı sahibi BC'ye
  kullanıcı token'ıyla proxy'lenir. Login/OIDC doğrudan Identity (agent OAuth); web cookie-login yok.
- **Admin aynı `/mcp`'de görünür.** BC'de tool-görünürlük budaması YOK — görünürlük fasadın işi
  (`ScopeResolver`). Yetki **tek katman**: admin slice handler'ındaki `[RequiredScope]` (403 son
  savunma). Ayrı allowlist/görünürlük listesi YOK. Her admin yazma kendi BC'sinde salt-append
  `AdminActionLog`'a yazar. `CatalogAdminSurface.SCOPES` (tek örnek) yalnız auth kaydı + PRM demeti için kalır.
- **MCP uçlarının auth matrisi** (`Program.cs`'teki `MapMcp` çağrısı kanonik):
  **korumalı** (`RequireAuthorization` + RFC 9728 PRM keşfi, `Common/Extensions/McpResourceMetadataExtension`) —
  basket · order · payment · customer · reviews · library · discount;
  **anonim kalır** — catalog · stock · storefront · checkout · mail-mcp.
  UserKey (`X-User-Key`) yan yoldur, Bearer'la çakışmaz.
- **Ölü-mesaj (DLQ) operatör yüzeyi (088):** `admin_list_dead_letters` · `admin_get_dead_letter` ·
  `admin_replay_dead_letter` · `admin_discard_dead_letter` — scope `ops.deadletter`, **File hariç her
  BC'nin `/mcp`'sinde** (`Common/Utils/DeadLetters/DeadLetterAdmin.cs`; BC tarafı kablo
  `Extensions/MessagingExtensions.cs` + `Program.cs`). Admin "hata var mı / tekrar dene" akışını kendi
  AI istemcisinden yürütür — ayrı ops ekranı yok.
- **`logout`** (`AuthTools`): korumalı MCP'lerde ortak oturum-kapatma tool'u (basket/order/payment/customer).
- **Kalan REST** = S2S internal (merchant-key, adres varsayılan, 077 Payment intents/callback) + auth
  (Identity OIDC) + MCP-infra (PRM) + Catalog import yükleme ekranı. **Kalan senkron kontrat** =
  checkout gRPC (basket) + Order→Payment hosted-link S2S (077) + Discount→Order aktif-yüzde gRPC.
  Eski "her aggregate bir REST penceresi" kuralı EMEKLİ.
- **Scope zorlaması AgentPlatform'da (085 R3/R5):** DCR tavanı değişmedi, **zorlama yeri** değişti —
  `ScopeResolver` `talep ∩ istemci-tavanı ∩ (rol ∪ her-zaman-izinli)` kesişimini kurar
  (`ClientCeilingResolver`; OpenIddict scope-izin ön-validasyonu `IgnoreScopePermissions()` ile
  gevşetildi). Tavan-üstü talep RED değil **sessiz eleme** — union PRM müşteri bağlantısını kırmaz.
  Seed istemciler `external-admin-agent` + `external-customer-agent` AYNI `/mcp`'ye bağlanır (fasat tek
  union PRM ilan eder); Claude Desktop'ta iki kayıt aynı URL'e düşerse cache/kimlik ayrışması mcp-remote
  `--static-oauth-client-info` ile (her kayıt kendi `client_id`'ini sabitler; `~/.mcp-auth` hash'i
  buradan türer — sunucuda ek kod YOK).

## Ürün yazım yolu (050: first-party — mallar mağazanın)

İki giriş var (091: Excel import + yayın makinesi SÖKÜLDÜ):

1. **Açılış seed'i (091):** commit'li `Seeding/Data/books.json` → `BookSeedHostedService` satır satır
   (Gtin varsa ATLAR — admin düzenlemesi ezilmez, upsert bilinçli reddedildi); ürün CANLI doğar.
2. **Elle oluşturma (074/091):** `admin_create_product` — CANLI doğar, ISBN=Gtin çakışması reddedilir,
   ayrı yayın adımı YOK.

**Taslak/yayın/gizleme ekseni yok (091):** fiyat>0 var oluş invariant'ı (`Product.Create`/`SetPrice`);
her değişim vitrine akar; `ProductChangedEvent.IsDeleted` kontratta durur, hep `false`.
Düzenleme = MCP admin tool'ları (`admin_update_product` / dimensions / seo / tag / category / author /
spec). Yeni üründe Catalog `ProductLinked` → Stock, `ProductChangedEvent` → Storefront.
**Silme YOK** (016).

## Test

Testler **saf unit** — xUnit + Shouldly, `tests/<Servis>.Api.Tests` (12 proje, hepsi `.slnx`'te).
Host ayağa kaldırılmaz: `WebApplicationFactory` yok, Testcontainers yok, integration/E2E harness YOK
(Anayasa İlke VI bunu açıkça kurmaz der). Kapsam: aggregate davranış metotları, saga `On*` karar
metotları, value object'ler ve mock'suz test edilebilir saf birimler (ör. `CacheKeyFactory`).
Marten/RabbitMQ/ES'e giden yollar test edilmez — Aspire'da elle doğrulanır.

Yeni test: ilgili `tests/<Servis>.Api.Tests` köküne `<Konu>Tests.cs` (alt klasör yok). Prod sorgu/komut
tipine bağlanmak gerekmiyorsa test-yerli `record` tanımla (`CachingAspectTests` deseni). Saf domain
mantığı **test-first** yazılır ve `tasks.md`'de test task'ı implementasyondan ÖNCE gelir.

## Yeni şey ekleme

**Yeni admin MCP tool'u:**

1. **Ad** → `src/others/Shared/McpToolNames.cs`'teki ilgili `*AdminTools` sınıfına const.
   Ad **dış sözleşmedir** (Claude Desktop vb. dışarıdan görür) — değiştirmek dış kırılmadır.
2. **Description** → `src/others/Shared/McpToolDescriptions.cs` const'ı (standart: Yapma listesi).
3. **Slice** → `src/services/<bc>/<Bc>.Api/Domains/<Aggregate>/Features/Agents/{Commands,Queries}/Admin<Ad>.cs`:
   `[RequiredScope(...)]` **record command/query üzerinde**, `FeatureObjectResultModel<T>` dönen
   `[Transactional]` handler, aynı dosyada `[McpServerToolType]` sınıf +
   `[McpServerTool(Name = Shared.<Bc>AdminTools.<Ad>)]` metot.
4. Yazma ise `AdminActionLog` izi.

Yeni scope gerekiyorsa `AuthorizationScopes`'a ekle (+ Catalog'da `CatalogAdminSurface.SCOPES`, auth
kaydı + PRM demeti için). Başka liste YOK.

**Yeni BC:** `src/services/<bc>/<Bc>.Api` + kendi Postgres DB'si/şeması + `tests/<Bc>.Api.Tests`
(ikisi de `.slnx`'e) · AppHost'ta servis + DB kaydı · sürümler `Directory.Packages.props` ·
`Extensions/MessagingExtensions.cs` (Wolverine + DLQ tool kablosu) · entegrasyon event'lerle, senkron
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
- **NotificationAgent (060):** TEK singleton `MailAgent` (workflow da compose/send ayrımı da YOK —
  kullanıcı kararları); tek LLM çağrısı maili yazar + `send_mail` tool'unu çağırır; her hata
  `NotificationException`→retry→error queue. Mailpit ham container (SMTP 1025 / UI 8025); Mail.Mcp
  SMTP hedefini env'den alır (`SmtpOptions`).

## Yapma listesi

- **Çok-tedarikçi feed zincirini geri getirme:** Procurement + Supplier (+ Supplier.Gateway /
  IngestionAgent) 050'de söküldü — model first-party, mallar mağazanın. Ürün girişi = ürün-CRUD.
- **`IConfiguration`'dan doğrudan okuma** (Options pattern istisnaları hariç).
- **MCP'yi agent-dışı koddan imperatif çağırma** (Anayasa İlke I) — yapısal servisler-arası ihtiyaç
  REST/gRPC sözleşmesiyle karşılanır.
- **Hassas kimlik/sır değerini insan-yüzeyde render etme** — ekrana/chat'e/MCP dönüşüne ve
  log/trace'e yazma. Kural ve gerekçe **Anayasa İlke VIII**'de (+ ADR
  [`adr-mcp-control-plane-no-secret-return.md`](docs/adr/adr-mcp-control-plane-no-secret-return.md));
  bu projedeki uygulaması 087 makine-handoff onboarding'dir (bkz. `customer` satırı).
- **Tool description'ı standart-dışı yazma:** expose edilen her MCP tool description'ı
  [MCP Tool Description Standardı](../AgentPlatform/docs/mcp-tool-description-standard.md)'na uyar
  (eylem-önce, Türkçe tetikleyici ifade "'...' gibi istekler için", kısıt/PII sonda, prose prefix yok)
  ve `src/others/Shared/McpToolDescriptions.cs` const'ından referanslanır — inline `[Description("...")]`
  string bırakma.
- **Yeni saga için ayrı orchestration servisi açma** (god-service) — saga sürecin sahibi BC'de host
  edilir. (049 Checkout.Orchestrator mevcut istisnadır, emsal değil.)
- **Kimlik makamını ECommerce'e geri gömme (084):** IdP `AgentPlatform`'da. AppHost'a IdP proje-ref'i
  EKLEME, scope/client/rol'ü koda gömme (yenisi `AgentPlatform` `AppRegistry` config'ine). Nötr auth
  kablosu `Platform.Auth` paketinde — `Common`'a geri taşıma.
- **`src/otherProjects/CustomNopCommerce`'e dokunma** — `.slnx` dışı referans/deneme kodu
  (git-takipli ama çözümün parçası değil). Genel kural: çözüme dahil olmayan klasör kapsam dışıdır.
