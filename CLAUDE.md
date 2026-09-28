# CLAUDE.md

Claude Code'a bu repo'da rehberlik eder. **Gerçek-kaynak sırası:** kod + bu dosya >
Claude memory > Obsidian vault. Feature detayı BC haritasındaki `specs/*` yollarında.

**Mimari + kod konvansiyonları (taşınabilir katman): @docs/conventions.md** — DDD/VSA kuralları,
kod standartları, servisler-arası desenler orada. Bu dosya yalnız BU projeye özel bilgidir.

## Komutlar

Repo kökünden. Çözüm: `ECommerceWithAgentFramework.slnx` (`dotnet build/test` dosyayı
otomatik bulur, açıkça vermeye gerek yok). Format/lint script'i YOK.

```bash
dotnet build                                              # tüm çözüm
dotnet run --project src/aspire/AppHost/AppHost.csproj    # tüm sistem (Aspire)
dotnet test                                               # tüm testler
dotnet test tests/Basket.Api.Tests/Basket.Api.Tests.csproj          # tek proje
dotnet test --filter "FullyQualifiedName~BasketTests.AddItem"       # tek test
scripts/check-claude-spec-links.sh                        # BC haritası spec yolları guard'ı
scripts/check-flow-links.sh                               # FLOW.md domain-süreç anchor guard'ı (İLKE VII)
```

- **Sistemi hep Aspire AppHost'tan başlat**, tek servis değil — servisler birbirini/DB/RabbitMQ'yu
  service discovery + conn-string enjeksiyonuyla bulur; tek API bağımsız açılmaz.
- **Marten şeması otomatik kurulur** (`ApplyAllDatabaseChangesOnStartup`) — migration komutu yok.
- **OpenAI kullanan servisler** (ModerationAgent, NotificationAgent, Storefront —
  embedding, 067) açılışta fail-fast:
  `dotnet user-secrets set OpenAI:ApiKey <k> --project <proj>` (+ `OpenAI:Model`, ör. gpt-4o-mini).
- **Paket sürümleri yalnız `Directory.Packages.props`'ta** (Central Package Management); `.csproj`
  `PackageReference`'ı sürümsüz listeler. Sürüm ekle/değiştir → yalnız props.

## Teknoloji

.NET 10 (`Nullable`+`ImplicitUsings` açık) · **Marten** (Postgres = document/event store, Newtonsoft,
non-public setter+ctor) · **Wolverine** (in-proc bus `IMessageBus` + RabbitMQ fanout; handler assembly
taramasıyla) · **OpenIddict + ASP.NET Identity** (IdP) · **YARP** gateway · **MCP** (her API `/mcp`;
müşteri yüzeyi `mcp-gateway` fasadı — dış AI istemcisi tüketir) · **Microsoft Agent Framework** +
`Microsoft.Extensions.AI` (ModerationAgent, NotificationAgent) · **Scrutor** (DI) · **xUnit + Shouldly**.

## BC haritası

Her BC = kendi DB'si + şeması. Origin sütunu = BC'yi tanımlayan spec'in tam yolu (guard'lı); sonraki
feature'lar o feature'ın kendi spec'inde. Servisler `src/services/*`; destek `src/others`
(`Common`/`Shared`/`Identity.Server`), `src/aspire` (`AppHost`/`ServiceDefaults`), `src/agents`, `src/ui`.

| Servis | DB | Ne yapar | Origin spec |
|---|---|---|---|
| `catalog` | catalogDb | Zengin `Product`+`Category`+`Author`+`Publisher`+`ProductTag`+`SpecificationAttribute` (kitap künyesi: çok-yazar + tek yayınevi); admin düzenleme + yayın anahtarı + fiyat geçmişi (058, append-only `ProductPriceChange`); admin yüzeyi TEK `/mcp`'de scope-budamalı (085 — `/mcp-admin` söküldü; 070: admin tool'lar + `AdminActionLog` izi); **Excel katalog import (083):** token-linkli xlsx yükleme ekranı → `ImportRow` staging → `ImportProcessor` TASLAK ürün (`ProductAdded`, exactly-once) + `publish_imported` toplu yayın + `get_import_status`; kapağı File.Api'den `CoverIngested` tüketir (`FileConsumers`→`SetImage`); eski books.json seeder söküldü | `specs/040-catalog-domain-extract` |
| `basket` | basketDb | Kalıcı sepet + kalem; anonim sahiplik (057; login-merge yüzeyi söküldü, `MergeFrom` domain'de durur); stok tutmaz/süre yok (056), stok gerçeği checkout'ta; yüzey MCP-only + checkout gRPC | `specs/012-stock-reservation` |
| `order` | orderDb | Sipariş aggregate + yaşam döngüsü; orchestrator'dan broker Confirm/Cancel; hosted-CF ödeme yolu (`start_payment` — sepet+adres oku, Pending order, Payment S2S hosted link; 077); `PaymentSucceeded`→StartCheckout / `PaymentFailed`→Cancel tüketir; Confirm'de `OrderCompleted` fanout (Reviews + Storefront) | `specs/028-checkout-saga` |
| `checkout` | checkoutDb | Broker-only checkout sağası (`CheckoutProcess`, ayrı servis); 077: ödeme öncedendir (hosted-CF) → CommitStock→Confirm→ClearBasket (Charge adımı SÖKÜLDÜ); StartCheckout OrderId dolu (`CheckoutId=OrderId`); CommittingStock'ta LIFO telafi + watchdog | `specs/049-checkout-orchestrator` |
| `payment` | paymentDb | Hosted-CF ödeme (077): `PaymentIntent` (kart alanı yok); PG hosted link (`PgHostedPaymentClient`, MerchantKey S2S) + HMAC callback (`CallbackSecret` ayrı) → `PaymentSucceeded`/`PaymentFailed` fanout; terk-timer `ScheduleAsync`→Expire; TxRef unique idempotent | `specs/077-hosted-cf-payment` |
| `stock` | stockDb | `ProductStock` (OnHand); ilk stok `ProductLinked`'ten; checkout düşümü broker'dan (056); admin artır/azalt + mutlak set (058); admin yüzeyi TEK `/mcp`'de scope-budamalı (085 — `/mcp-admin` söküldü; 070: set/adjust tool + iz; `Adjust` domain guard'lı) | `specs/014-supplier-stock-authority` |
| `storefront` | storefrontDb | Push-only read-model (`StorefrontView`); müşteri REST okuma yüzeyi (liste/facet/aile/harf-dizin/feed) SÖKÜLDÜ — okuma yolu asistan; `UserPurchase` birikimi sürer; asistan yüzeyi TEK tool `query_storefront` (069: salt-okur `storefront_sellable` view + `AgentSqlGuard` bekçi + kısıtlı DB rolü + `{{EMBED}}` anlamsal + `AgentQueryLog` izi; 070: sorgu rehberi/playbook KANONİK evi tool Description'ı, ChatAgent kopyası donduruldu; parametrik arama + `find_similar_books` SÖKÜLDÜ) | `specs/003-storefront-read-model` |
| `customer` | customerDb | Wallet (tokenize kart, PAN yok; kart YAZMA yüzeyi yok — yalnız okuma + payment-context) + AddressBook; izole, event yok; merchant-admin yüzeyi TEK korumalı `/mcp`'de scope-budamalı (085 — `/mcp-admin` söküldü; 078: PII'siz hosted onboarding — 070 imperatif MCP sapması SÖKÜLDÜ, PG'ye tipli S2S REST; SAPMA-2 = anonim token-yetkili credential ekranı `/merchant-credentials/{token}`, key sohbete girmez) | `specs/022-wallet-address-book` |
| `reviews` | reviewsDb | Satın-alma şartlı yorum; AI moderasyon AYRI worker'da (broker); özet event → Storefront | `specs/044-product-reviews` |
| `library` | libraryDb | Kullanıcı-ürün ilgi kayıtları; ilk dilim fiyat alarmı (yaşayan abonelik, email snapshot) + `NotificationRecord` izi; `ProductChangedEvent.OldPrice` tetiği → alarm başına `PriceAlarmTriggered` | `specs/060-price-alarm-mail` |
| `gateway` | — | YARP reverse proxy; tek giriş | — |
| `identity-server` | identityDb | **084: ECommerce'ten ÇIKTI — `AgentPlatform` repo'sunda platform IdP** (uygulama-nötr; ECommerce "app #1" olarak `AppRegistry` config'inde kayıtlı relying party). OpenIddict + ASP.NET Identity; OIDC/OAuth + RBAC; DCR + tek consent + revocation (061). Nötr auth kablosu (`IdentityOption`+`AddAuthenticationAndAuthorizationExtension`) = `Platform.Auth` NuGet paketi (yerel feed, namespace Common.* korunur). ECommerce IdP'ye dış-servis (issuer URL `IdentityOption.Address`) olarak bakar; AppHost proje-ref YOK | `../AgentPlatform` · `specs/084-platform-idp` |
| `reviews-moderation-agent` | — | Reviews moderasyonu (DB'siz worker); `ReviewModerationRequested`→LLM→`ReviewModerated` | `specs/046-reviews-moderation-agent` |
| `notification-agent` | — | Fiyat alarmı maili (DB'siz worker); `PriceAlarmTriggered`→LLM compose→Mail.Mcp `send_mail`→`NotificationSent` | `specs/060-price-alarm-mail` |
| `mail-mcp` | — | İlk standalone MCP server; tek tool `send_mail` (MailKit→Mailpit); yalnız NotificationAgent tüketir, ChatAgent'a KAYITLI DEĞİL | `specs/060-price-alarm-mail` |
| `file` | fileDb | Kapak **kayıt defteri** (082: DB'siz proxy → Marten BC); `FileAsset` (ImageName=ISBN tekil/değişmez unique-index + metadata) + nested `FileStorageLocation` (çoklu fiziki depo: R2/Local/…, upsert invariant). Fiziki bit `IFileStore` ardında (`S3FileStore`→R2, byte DB'de değil); URL provider-agnostik lokal çözülür (`CoverUrlResolver`, StorageFilePath=key + config-base, full URL saklanmaz). `GET /files/v1/covers/{isbn}` anonim serve; S2S `POST /internal/files` (yaz+kayıt) + `/resolve` (batch, 0 dış çağrı) + `GET .../locations`; idempotent R2 backfill (config-gated). **083: kapak akışı kablosu** — RabbitMQ transport (bugüne dek in-proc only); `ProductAdded` tüketir (`CatalogConsumers`, R2'de yoksa yerel staging'den yükle+kayıt) → `CoverIngested(isbn,url)` yayar → Catalog `Product.ImageUrl` doldurur | `specs/081-cover-image-store` |
| `mcp-gateway` | — | **Tek müşteri+admin MCP fasadı** (DB'siz proxy); alt BC `/mcp`'lerini LAZY toplar (SDK `WithListToolsHandler`/`WithCallToolHandler`), ad→BC token-forward proxy; **085: TEK `/mcp` ucu** (`/mcp-admin` + ikinci PRM söküldü) — görünürlük yol-prefix değil TOKEN SCOPE'una göre (BC `ConfigureSessionOptions`); TEK PRM `scopes_supported` = müşteri+admin UNION (iki OAuth istemci — `external-customer-agent` + `external-admin-agent` — aynı uca bağlanır); `tools/list` cache anahtarı scope-parmakizi, yönlendirme registry'si ayrı m2m tam-katalog cache'inde; auth HER ZAMAN upfront login (anonim/step-up modu + `McpStepUpMiddleware` kalıcı olarak SÖKÜLDÜ, kullanıcı kararı); **mağazanın TEK müşteri yüzeyi** (ChatAgent+UI söküldü); **047: PaymentGateway downstream'leri** (`pg-merchant` :5202, `pg-commission` :5203 sabit URL) fasada eklendi — `FacadeScopes.All` + `DiscoveryScope` PG demetini (`merchant.read/write/admin`, `commission.read/write`) ilan eder, tek token EC+PG scope'larını taşır | `specs/073-customer-mcp-facade` · `specs/085-single-mcp-surface` |

- **Ürün yazım yolu (050 pivot — first-party):** Çok-tedarikçi feed (Procurement + Supplier) SÖKÜLDÜ;
  mallar mağazanın. Giriş = **083 Excel import** (admin xlsx→`ImportRow`→TASLAK ürün; 051 books.json
  seeder söküldü) + **074 doktrin kayması: elle ürün OLUŞTURMA VAR** — admin
  `create_product` (`/mcp-admin`, TASLAK doğar, ISBN=Gtin çakışması reddedilir, yayın ayrı
  `set_published`). Düzenleme = MCP admin tool'ları (`update_product`/dimensions/seo/tag/category/
  author/spec; 058 REST ekranları 074'te söküldü). Catalog yeni üründe `ProductLinked` → Stock +
  `ProductChangedEvent` → Storefront. Silme yok (016); yayından kaldırma `IsDeleted:true` (058).
- **UI (WebApp) + ChatAgent SÖKÜLDÜ (2026-09-11):** Mağaza artık ne görsel ekran ne kendi sohbet
  agent'ı host eder — tam **agent-only / BYO-agent**. Müşteri **kendi AI istemcisiyle** (Claude Desktop
  vb.) `mcp-gateway` fasadının **TEK `/mcp` ucuna** (085 — `/mcp-admin` söküldü, tek login, upfront) bağlanır;
  tool'lar alt BC `/mcp`'lerinden toplanır, çağrı sahibi BC'ye kullanıcı token'ıyla proxy'lenir. Admin de
  AYNI `/mcp`'de, token scope'una göre budanmış görünür (085). Login/OIDC doğrudan Identity (agent OAuth);
  web cookie-login yok. Kalkan referanslar: AppHost web/chat-agent kayıtları, Identity
  `ecommerce.bff`+`chat-agent-discovery` client + WebApp redirect URI'ları, NotificationAgent mail'deki
  WebApp ürün linki. UI'a ait `ICustomerRefitService` vb. WebApp ile birlikte gitti.
- **Admin yüzeyi MCP'de (070→085, `specs/070-admin-mcp-surface` · `specs/085-single-mcp-surface`):**
  catalog/stock/customer/discount admin tool'ları TEK `/mcp`'de yaşar — **085: ayrı `MapMcp("/mcp-admin")`
  ucu SÖKÜLDÜ**; görünürlük yol-prefix'ten TOKEN SCOPE'una taşındı (`ConfigureSessionOptions` +
  `McpScopePruningExtension.IsToolVisible`). Tool→scope eşlemesi her BC'nin `*AdminSurface.ToolScopeMap`
  holder'ında (tek kaynak). **TUZAK: yeni admin tool eklerken `ToolNames`+`ToolScopeMap`'in İKİSİNE EKLE**
  (074/085 dersi). Seed OAuth istemcisi `external-admin-agent` artık müşteri istemcisiyle
  (`external-customer-agent`) AYNI `/mcp`'ye bağlanır (fasat tek union PRM ilan eder). Claude Desktop'ta
  iki kayıt aynı URL'e — cache/kimlik ayrışması mcp-remote `--static-oauth-client-info` ile (her kayıt
  kendi `client_id`'ini sabitler; `~/.mcp-auth` hash'i buradan türer, sunucuda ek kod YOK). Her admin yazma
  BC'sinde salt-append `AdminActionLog`. **074: catalog admin parite tamamlandı, domain iş REST'i tümüyle
  SÖKÜLDÜ.** Kalan REST = S2S internal + auth + MCP-infra.
- **085 R3/R5 detay:** Discount'un tek ucu `/mcp-admin`'den korumalı `/mcp`'ye taşındı (anonim seti yok).
  DCR tavanı DEĞİŞMEDİ ama zorlama YERİ değişti — **AgentPlatform `ScopeResolver`** artık
  `talep ∩ istemci-tavanı ∩ (rol ∪ her-zaman-izinli)` kesişimi kurar (`ClientCeilingResolver`; OpenIddict
  scope-izin ön-validasyonu `IgnoreScopePermissions()` ile gevşetildi) — tavan-üstü talep RED değil sessiz
  eleme, union PRM müşteri bağlantısını kırmaz.
- **Müşteri yüzeyi MCP-only:** basket/order/payment/reviews/library/customer(cards+addresses)
  müşteri REST uçları + Commands/Queries ikizleri SÖKÜLDÜ — chat işlemleri yalnız MCP→`Features/Agents`
  slice'larından. **074: admin domain REST'i de söküldü (catalog/stock/customer-merchant + checkout POST)
  — yüzey tümüyle MCP.** Kalan REST = S2S internal (merchant-key + adres varsayılan + 077 Payment intents/
  callback) + auth (Identity OIDC) + MCP-infra (PRM). Kalan senkron kontrat = checkout gRPC (basket) + Order→
  Payment hosted-link S2S (077). Eski "her aggregate REST penceresi"
  kuralı EMEKLİ. Gateway'de yalnız MCP/PRM rotaları (catalog REST proxy `catalog-route` de söküldü).
- **ChatAgent MCP keşfi makine kimliğiyle:** açılışta ListTools `chat-agent-discovery` m2m token'ı taşır
  (061 korumalı transport'lar için; `DiscoveryTokenSource` + `TokenInjectingHandler` HttpContext-yok
  fallback'i). Tool ÇAĞRISI her zaman o anki kullanıcı token'ıyla. Keşifte 401/403 KALICI sayılır (retry
  yok); dış MCP'ler (DropShop) tek deneme — retry bütçesi yalnız Aspire iç boot yarışına.
- **ModerationAgent (ayrı `reviews-moderation-agent` worker'ı):** Singleton ChatClientAgent (Temp=0,
  structured JSON, MCP'siz), retry→error queue. Moderasyon 046'da BC'den broker'lı worker'a taşındı
  (Reviews'te agent-framework yok; iletişim `ReviewModerationRequested`/`ReviewModerated` event'leriyle).
- **NotificationAgent (060):** TEK singleton `MailAgent` (workflow da compose/send ayrımı da YOK —
  kullanıcı kararları); tek LLM çağrısı maili yazar + `send_mail` tool'unu çağırır; her hata
  `NotificationException`→retry→error queue. Mailpit ham container (SMTP 1025/UI 8025); Mail.Mcp
  SMTP hedefini env'den alır (`SmtpOptions`).

## Projeye özel yetki + tuzaklar

- **RBAC (scope; İLKE V):** `AddAuthenticationAndAuthorizationExtension(config, ...scopes)`; `KnownScopes`
  kapalı registry, rol→scope map DB'de, admin `/Admin/*`'ten yönetir. Register → `customer` rolü.
  `[RequiredScope]` Wolverine mesaj handler'larına da uygulanır. Identity.Server **HTTPS zorunlu**.
- **TUZAK (`ScopeClaimArrayHandler`):** `context.TokenType` URN'dir (`TokenTypeIdentifiers.AccessToken`),
  hint DEĞİL — hint'le kıyaslarsan handler no-op → 403 → korumalı yüzeyde redirect döngüsü (tarihsel
  belirti: WebApp sepet ekranı; ekran 066'da söküldü ama tuzak scope-korumalı her uçta geçerli).
- **Dış agent MCP OAuth (061):** basket/order/customer/payment MCP'leri `RequireAuthorization` + RFC 9728
  keşif (`Common/Extensions/McpResourceMetadataExtension`); storefront/catalog/stock MCP anonim KALIR.
  UserKey (X-User-Key) yan yol — Bearer'la çakışmaz.
- **DCR istemcileri (061):** public+PKCE, `ConsentType=Explicit`, kapalı scope demeti
  `ExternalAgentDefaults` (yönetim scope'ları giremez, client_credentials verilmez); seed istemciler
  Implicit kalır (consent görmez). Redirect yalnız loopback + Claude callback (`DcrRequestValidator`).

## Yapma listesi

- **Çok-tedarikçi feed zincirini geri getirme:** Procurement + Supplier (+ eski Supplier.Gateway /
  IngestionAgent) 050'de SÖKÜLDÜ — model first-party, mallar mağazanın. Ürün girişi = ürün-CRUD.
- **`IConfiguration`'dan doğrudan okuma** (Options pattern istisnaları hariç).
- **MCP'yi agent-dışı koddan** imperatif çağırma.
- **Tool description'ı standart-dışı yazma:** expose edilen her MCP tool description'ı [MCP Tool Description Standardı](../AgentPlatform/docs/mcp-tool-description-standard.md)'na uyar (eylem-önce, Türkçe tetikleyici ifade "'...' gibi istekler için", kısıt/PII sonda, prose prefix yok) ve `src/others/Shared/McpToolDescriptions.cs` const'ından referanslanır — inline `[Description("...")]` string bırakma (003).
- **Yeni saga için ayrı orchestration servisi** açma (god-service) — saga sürecin sahibi BC'de host edilir.
- **Kimlik makamını ECommerce'e geri gömme (084):** `identity-server` `AgentPlatform` repo'sunda platform IdP; ECommerce relying party. ECommerce AppHost'a IdP proje-ref'i EKLEME, scope/client/rol'ü koda gömme (yenisi `AgentPlatform` `AppRegistry` config'ine). Nötr auth kablosu `Platform.Auth` paketinde — Common'a geri taşıma.
- **Çözüme (`.slnx`) dahil olmayan klasörlere dokunma** (staging/deneme kodu) — kapsam dışı.
