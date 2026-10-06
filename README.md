# Agent Framework ile E-Ticaret

> Olay-güdümlü, Domain-Driven mikroservis e-ticaret platformu — müşteri yüzeyi tamamen **kendi getirdiğin AI istemcisi** (Claude Desktop vb.) üzerinden, uçtan uca **.NET Aspire** ile orkestre.

<p>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white">
  <img alt="Aspire" src="https://img.shields.io/badge/.NET_Aspire-orkestrasyon-512BD4">
  <img alt="Marten" src="https://img.shields.io/badge/Marten-document%2Fevent_store-16a34a">
  <img alt="Wolverine" src="https://img.shields.io/badge/Wolverine-CQRS%20%2B%20messaging-0ea5e9">
  <img alt="PostgreSQL" src="https://img.shields.io/badge/PostgreSQL-servis_başına-4169E1?logo=postgresql&logoColor=white">
  <img alt="RabbitMQ" src="https://img.shields.io/badge/RabbitMQ-integration_events-FF6600?logo=rabbitmq&logoColor=white">
  <img alt="OpenIddict" src="https://img.shields.io/badge/OpenIddict-OIDC%2FOAuth-512BD4">
  <img alt="YARP" src="https://img.shields.io/badge/YARP-gateway-blueviolet">
  <img alt="MCP" src="https://img.shields.io/badge/MCP-tek_müşteri_fasadı-9333ea">
  <img alt="Microsoft Agent Framework" src="https://img.shields.io/badge/Microsoft_Agent_Framework-AI_worker-2563eb">
  <img alt="Elasticsearch" src="https://img.shields.io/badge/Elasticsearch-arama_%2B_kNN-f59e0b?logo=elasticsearch&logoColor=white">
  <img alt="Redis" src="https://img.shields.io/badge/Redis-L2_cache_%2B_backplane-DC382D?logo=redis&logoColor=white">
</p>

## Genel Bakış

Her servisin kendi veritabanına sahip, izole bir **bounded context** olduğu tam bir **mikroservis e-ticaret backend'i**. Context'ler arası iletişim yalnızca **integration event**, **Model Context Protocol (MCP)** ve — bir adımın anlık evet/hayır ya da context'ler arası devir gerektirdiği yerde — sanksiyonlu **tipli gRPC** (stok rezervi), **broker command/reply** (checkout sağası) ve **servisten-servise (S2S) çağrı** (Order → Payment hosted ödeme linki) üzerindendir.

Bu proje **agent-only / BYO-agent** duruşundadır: mağaza **ne görsel ekran ne de kendi sohbet agent'ını** host eder. Müşteri **kendi AI istemcisiyle** (Claude Desktop vb.) tek MCP fasadına bağlanır; işlemler alt bounded context'lerin MCP tool'larına, çağıran kullanıcının token'ıyla proxy'lenir. Bir AI agent worker'ı çekirdeğin çevresinde durur, **Microsoft Agent Framework** üzerinde:

- **Notification Agent** — fiyat alarmı e-postası yazan **durumsuz worker** (DB yok). `PriceAlarmTriggered`'ı tüketir, tek LLM çağrısıyla maili yazar ve **Mail.Mcp**'nin `send_mail` tool'unu çağırır, `NotificationSent` yayınlar.

Katalog **first-party**'dir: mağaza envanterin sahibidir, ürün girişi düz ürün-CRUD'dur (eski çok-tedarikçili besleme hattı bilinçli olarak söküldü). Yeni ürünler `Catalog → Stock` (ilk stok) ve `Catalog → Storefront` (event-log → ES projection) yönünde integration event akar.

DDD, CQRS ve olay-güdümlü tasarımın gerçek .NET kodunda ne kadar ileri taşınabileceğini — ve modern bir LLM agent'ının bu mimariye iş mantığını agent katmanına sızdırmadan nasıl temiz eklendiğini — göstermek için kurulmuş bir portföy / öğrenme projesidir.

## Bu proje neyi gösteriyor

- **Bounded-context izolasyonu** — kendi PostgreSQL veritabanı + Marten şeması olan on iki DB-sahibi bounded context, artı YARP gateway ve iki yardımcı süreç (Notification Agent + Mail.Mcp); platform MCP fasadı ile kimlik makamı (IdP) ayrı **AgentPlatform** repo'sunda yaşar. Paylaşılan domain modeli yok; aynı kavram (*Ürün*) her context'te farklı modellenir — Catalog'da zengin aggregate, Basket'te düz sepet-kalemi, Storefront'ta Elasticsearch dokümanı.
- **Zengin aggregate'ler ve zorunlu invariant'lar** — iş kuralları handler'da değil aggregate'in içinde yaşar (private koleksiyon, davranış metotları). Geçersiz durumlar temsil edilemez.
- **Vertical Slice + CQRS** — kod teknik katmana değil feature'a göre örgütlenir. Yazma/okuma ayrı slice'lar; repository yok — handler doğrudan Marten `IDocumentSession` kullanır.
- **Exception yerine Result pattern** — beklenen hatalar (bulunamadı, doğrulama, kural ihlali) tipli `Result` nesneleriyle akar; exception yalnız gerçekten beklenmeyene ayrılır.
- **Scope-tabanlı yetkilendirme** — kimlik OpenIddict + ASP.NET Identity ile verilir; servisler OAuth **scope**'larına göre yetkilendirir (rol downstream'e sızmaz), hem HTTP uçlarında hem Wolverine mesaj handler'larında.
- **Agent-only müşteri yüzeyi (tek MCP fasadı, 073)** — tüm müşteri ekranları ve mağazanın kendi ChatAgent'ı **bilinçli söküldü**. Müşteri kendi AI istemcisiyle **platform MCP fasadına** (AgentPlatform'a taşındı, 001) bağlanır: tek `/mcp`, tek login. Tool'lar alt BC `/mcp`'lerinden LAZY toplanır, çağrı sahibine kullanıcı token'ıyla proxy'lenir.
- **Dış agent MCP OAuth (061)** — kullanıcının kendi AI agent'ı MCP uçlarına **OAuth 2.1** ile bağlanır: RFC 7591 **DCR** (istemci kendini kaydeder), RFC 9728 **PRM** keşfi, tek **consent** sayfası (Explicit), refresh token ile ekransız süreklilik, revocation. `basket/order/customer/payment` MCP korumalı; `storefront/catalog/stock` anonim gezinme için açık kalır.
- **MCP'de admin yüzeyi (070 → 085)** — admin tool'ları müşteri tool'larıyla AYNI tek korumalı `/mcp`'de yaşar (ayrı `/mcp-admin` ucu ve tool-görünürlük budaması bilinçli söküldü); yetki tek katman: her admin slice handler'ındaki `[RequiredScope]`. Her admin yazma BC'sinde salt-append `AdminActionLog` izi.
- **CQRS + Event Sourcing read-path (086)** — `storefront`'ta Postgres artık read-model değil **ürün-stream event-log**; dört kaynak event (Catalog/Stok/Yorum/İndirim) stream'e eklenir, **async projection** katlayıp **Elasticsearch** dokümanı yazar/siler. Müşteri okuma yüzeyi tek tool: `query_storefront` — LLM ham **ES Query DSL** üretir (text + fuzzy + kNN + filtre tek sorguda), minimal rail (sabit index, `_source` whitelist, size≤50) + `AgentQueryLog` izi.
- **Semantik arama (Elasticsearch kNN)** — `text-embedding-3-small` embedding'leri projection sırasında üretilir, ES `dense_vector` alanında yaşar; `{{EMBED}}` yer-tutucusu sorguda kNN vektörüne çözülür. Embedding kesintisi filtre-only aramayı bloklamaz.
- **Excel import + kapak boru hattı (083/081)** — admin xlsx yükler → `ImportRow` staging → arka plan süreç TASLAK ürün üretir (exactly-once, ISBN idempotency); `ProductAdded` event'iyle File BC kapağı R2'ye yazar, `CoverIngested` ile Catalog'a URL döner. Kapak kayıt defteri provider-agnostik (`IFileStore`: R2/Local).
- **Ölü-mesaj gözlemlenebilirliği (088)** — her serviste uniform retry → error-queue yakalama; operatör MCP tool'larıyla listele/incele/replay. Cache boşaltma gibi idempotent mesajlar DLQ'ya düşmeden kendi durable retry merdivenini taşır.
- **Satın-alma şartlı yorum** — `reviews` context'i yalnız ürünü gerçekten satın alan kullanıcıdan 1–5★ yorum kabul eder; hak, `OrderCompleted` event'inden yerel projeksiyonla belirlenir (senkron çağrı değil). Yorum hemen görünür (moderasyon yok); puan özeti Storefront'a akar.
- **Hosted-CF ödeme (077)** — kart alanı sistemden geçmez. Payment BC bir **hosted ödeme linki** üretir (PG hosted sayfası), müşteri orada öder, PG **HMAC-imzalı callback** ile döner → `PaymentSucceeded`/`PaymentFailed` fanout. Terk-timer (`ScheduleAsync`) callback gelmezse Expire eder; `TxRef` unique olduğu için idempotent.
- **Dayanıklı checkout orkestrasyonu (broker-only saga)** — checkout, kendi `Checkout.Orchestrator` servisinde Wolverine dayanıklı sağası olarak çalışır (durum Marten'de, `CheckoutId` anahtarlı). 077'de ödeme öncedendir → saga yalnız `CommitStock → Confirm → ClearBasket` sürer (Charge adımı söküldü). `CommittingStock`'ta stok başarısızsa LIFO telafi + takılan koşu için watchdog.
- **Fiyat alarmı + bildirim** — `library` context'i kullanıcı-ürün ilgi kayıtlarını ve yaşayan fiyat alarmı aboneliklerini tutar; `ProductChangedEvent.OldPrice` tetiğiyle alarm başına `PriceAlarmTriggered` yayınlar; Notification Agent maili üretir.
- **Bildirimsel, kesişen caching + durable invalidation** — okuma sorguları tek `[Cached(...)]` attribute'uyla önbelleklenir (HybridCache L1 + Redis L2, `IMessageBus` decorator'ı; handler'lar dokunulmadan kalır). Boşaltma **epoch-key** desenidir: yazma commit'i sonrası durable mesaj → handler Redis `INCR` ile jenerasyonu artırır, eski girdiler erişilmez kalıp TTL'de ölür; Redis kesintisinde 2 dk aralıklı durable retry merdiveni insansız toparlar. Pod'lar arası L1 tutarlılığı Redis pub/sub backplane ile.
- **Tek komutla orkestrasyon** — .NET Aspire her servisi, gateway'i, Postgres, RabbitMQ ve Redis'i service discovery + connection-string enjeksiyonuyla ayağa kaldırır.
- **Spec-driven development** — önemsiz olmayan feature'lar GitHub spec-kit akışıyla (spec → plan → tasks → implement), proje anayasasının yönetiminde geliştirilir.

## Mimari

```mermaid
flowchart TB
    subgraph Client["Müşteri (mağaza dışı)"]
        AIClient["Kendi AI istemcisi<br/>(Claude Desktop vb.)"]
    end

    AIClient -->|"OAuth 2.1 login + consent"| IdP["Platform IdP (AgentPlatform repo'su)<br/>(OpenIddict OIDC/OAuth + ASP.NET Identity)"]
    AIClient -->|"MCP (/mcp)"| Facade["platform MCP fasadı<br/>(AgentPlatform, DB'siz proxy)"]

    Facade -->|"ad→BC token-forward, kullanıcı token'ı"| GW["Gateway (YARP)"]

    GW --> Catalog["catalog-api"]
    GW --> Basket["basket-api"]
    GW --> Order["order-api"]
    GW --> Stock["stock-api"]
    GW --> Payment["payment-api"]
    GW --> Storefront["storefront-api"]
    GW --> Customer["customer-api"]
    GW --> Reviews["reviews-api"]
    GW --> Library["library-api"]
    GW --> Discount["discount-api"]
    GW -.->|JWT bearer / scope| IdP

    Order -->|"S2S: hosted ödeme linki iste"| Payment
    Payment -->|"hosted link"| PG["Dış PG hosted ödeme sayfası"]
    PG -->|"HMAC-imzalı callback"| Payment

    Catalog & Basket & Order & Stock & Payment & Storefront & Customer & Reviews & Library & Discount -->|integration events| MQ["RabbitMQ (fanout exchange + command kuyrukları)"]
    Payment -->|"PaymentSucceeded / PaymentFailed"| MQ
    MQ -->|"PaymentSucceeded→StartCheckout / PaymentFailed→Cancel"| Order
    MQ -->|"dört kaynak event → event-log + async projection"| Storefront
    MQ -->|"ProductAdded → kapak yükle"| File["file-api"]
    File -->|CoverIngested| MQ
    MQ -->|command / reply| Checkout["checkout-orchestrator"]
    Checkout -->|"CommitStock / Confirm / ClearBasket"| MQ

    Library -->|PriceAlarmTriggered| MQ
    MQ -->|LLM compose| Notif["notification-agent<br/>(durumsuz worker)"]
    Notif -->|"send_mail"| MailMcp["mail-mcp<br/>(standalone MCP server)"]
    MailMcp -->|SMTP| Mailpit["Mailpit"]

    Basket -->|gRPC reserve| Stock
    Order -->|"gRPC sepet kalemleri"| Basket
    Order -->|"gRPC aktif yüzde"| Discount

    Catalog --> DB1[("catalogDb")]
    Basket --> DB2[("basketDb")]
    Order --> DB3[("orderDb")]
    Stock --> DB4[("stockDb")]
    Payment --> DB5[("paymentDb")]
    Storefront --> DB6[("storefrontDb<br/>(event-log)")]
    Storefront --> ES[("Elasticsearch<br/>(arama doc + kNN)")]
    Customer --> DB7[("customerDb")]
    Reviews --> DB8[("reviewsDb")]
    Checkout --> DB9[("checkoutDb")]
    Library --> DB10[("libraryDb")]
    Discount --> DB11[("discountDb")]
    File --> DB12[("fileDb")]

    Catalog & Customer -.->|"L1/L2 cache + epoch backplane"| Redis[("Redis")]
```

Her servis kendi kendine yeten bir bounded context'tir. Senkron trafik **YARP gateway → servis** üzerinden gider; platform IdP'nin (AgentPlatform repo'su — ECommerce ona issuer URL'iyle dış-servis olarak bakar) verdiği OAuth scope'lu JWT bearer token'larla korunur. Durum değişiklikleri RabbitMQ fanout exchange'lerinde **integration event** olarak yayınlanır; `storefront` dört kaynağın (katalog/stok/yorum/indirim) event'lerini ürün-stream **event-log**'una ekler, **async projection** katlayıp Elasticsearch dokümanını yazar — gerçek-kaynak Postgres stream'i, arama ES'tedir.

Müşteri hiçbir mağaza ekranı açmaz: **kendi AI istemcisiyle** platform MCP fasadına (AgentPlatform) bağlanır. Fasad, alt BC `/mcp` uçlarındaki tool'ları LAZY toplar (SDK `WithListToolsHandler`/`WithCallToolHandler`), tool adına göre sahip BC'ye çağrıyı **kullanıcı token'ıyla** proxy'ler (`PerUserMcpTool` server ikizi) — agent *o kullanıcı olarak* davranır.

Olaylar dışında iki senkron kanal sanksiyonludur. **Stok rezervi** tipli **gRPC** kontratı (`Shared/Protos`) üzerindedir: Basket sepete-eklemede Stock'u senkron çağırır (fail-closed). **Order → Payment hosted ödeme linki** ise **servisten-servise** çağrıdır (kart alanı LLM'e/mesaja sızmaz). **Checkout** kendi `Checkout.Orchestrator` servisinde **broker command/reply** sağası olarak çalışır.

## Ödeme + sipariş akışı (077, hosted-CF)

Müşteri hiçbir ekrana dokunmadan, sohbetten uçtan uca ödeyip sipariş verir — ama kart verisi asla LLM'e teslim edilmez:

1. **Ödeme başlat** — müşteri (AI istemcisi → platform MCP fasadı) `start_payment`'ı tetikler (Order.Api agent slice'ı). Order sepeti (Basket gRPC, sunucu-yetkili) + adresi okur, **Pending** bir sipariş yaratır ve Payment'tan **S2S** ile hosted ödeme linki ister.
2. **Hosted ödeme** — Payment bir `PaymentIntent` yaratır (kart alanı yok), PG hosted linkini (`PgHostedPaymentClient`, `MerchantKey` S2S) üretir; müşteri PG'nin hosted sayfasında öder.
3. **Callback** — PG, ayrı `CallbackSecret` ile **HMAC-imzalı** callback döner; Payment doğrular → `PaymentSucceeded` veya `PaymentFailed` yayınlar. `TxRef` unique olduğu için idempotent; callback gelmezse terk-timer (`ScheduleAsync`) Expire eder.
4. **Saga tetiği** — Order `PaymentSucceeded`'ı tüketir → `StartCheckout` yayınlar (`CheckoutId = OrderId`) / `PaymentFailed` → siparişi Cancel eder.
5. **Checkout sağası** — `Checkout.Orchestrator` `CommitStock (kalem başına) → Confirm → ClearBasket` sürer (ödeme önceden olduğu için Charge adımı yok). Stok başarısızsa LIFO telafi + watchdog.
6. **Tamamlanma** — Order `Confirm`'de `OrderCompleted` fanout eder (Reviews yorum-hakkı + Storefront `UserPurchase`).

Neden ödeme hosted + S2S: `paymentId` başarı kanıtı değildir; halüsine bir "başarılı" bedava sipariş demektir. O yüzden ödeme dış PG'nin hosted sayfasında olur, doğrulama HMAC callback ile **sunucu tarafında**dır; LLM yalnız akışı başlatır. Kart ekleme/çıkarma sohbette **reddedilir** (güvenlik) — kart mağazanın işi değil (PSP sorumluluğu).

## Teknoloji Yığını

| Alan | Teknoloji |
|------|-----------|
| Çalışma zamanı | .NET 10, C# (nullable + implicit usings) |
| Orkestrasyon | .NET Aspire (AppHost + ServiceDefaults) |
| Kalıcılık | Marten (PostgreSQL document / event store) |
| Arama | Elasticsearch (turkish analyzer + `dense_vector` kNN; Kibana dev-görünürlük) |
| Bus & messaging | Wolverine (CQRS bus + RabbitMQ messaging + dayanıklı saga) |
| Messaging transport | RabbitMQ (fanout exchange + command/reply kuyrukları) |
| Caching | HybridCache (L1 bellek + Redis L2), AOP decorator; epoch-key durable invalidation + pub/sub backplane |
| Kimlik & yetki | Platform IdP — **AgentPlatform repo'su** (OpenIddict + ASP.NET Identity; OIDC/OAuth, scope-tabanlı, RFC 7591 DCR); ECommerce relying party (`Platform.Auth` paketi) |
| API Gateway | YARP (Aspire service discovery ile) |
| Müşteri yüzeyi | Tek MCP fasadı (AgentPlatform'a taşındı, 001) — dış AI istemcisi tüketir |
| Senkron RPC | gRPC (stok rezervi, sepet kalemleri — paylaşılan proto kontratları) |
| AI agent'lar | Microsoft Agent Framework + Microsoft.Extensions.AI (OpenAI), MCP |
| Semantik arama | Elasticsearch kNN (`text-embedding-3-small`, `{{EMBED}}` yer-tutucu çözümü) |
| DI | Scrutor (konvansiyon-tabanlı otomatik kayıt) |
| Test | xUnit + Shouldly (saf domain birim testleri) |

## Servisler

| Proje | Sorumluluk |
|---------|----------------|
| `catalog-api` | Zengin `Product` + `Category` + `Author` + `Publisher` + tag + spesifikasyon (kitap künyesi: çok-yazar, tek yayınevi); first-party ürün yazımı (Excel import 083: xlsx → staging → TASLAK ürün) + admin düzenleme + fiyat geçmişi; kapak URL'i File'dan `CoverIngested`'le; admin tool'ları tek korumalı `/mcp`'de (085) |
| `basket-api` | Kalıcı sepet + kalem; anonim sahiplik; stok tutmaz; yüzey MCP-only + checkout gRPC |
| `order-api` | Sipariş aggregate + yaşam döngüsü; `start_payment` (sepet+adres oku, Pending sipariş, Payment S2S hosted link); `PaymentSucceeded→StartCheckout` / `PaymentFailed→Cancel`; Confirm'de `OrderCompleted` fanout |
| `stock-api` | `ProductStock` (OnHand); ilk stok `ProductLinked`'ten; checkout düşümü broker'dan; gRPC rezerv sunucusu; admin set/adjust tek korumalı `/mcp`'de (085) |
| `payment-api` | Hosted-CF ödeme (077): `PaymentIntent` (kart alanı yok); PG hosted link + HMAC callback → `PaymentSucceeded`/`PaymentFailed`; terk-timer; `TxRef` unique idempotent |
| `storefront-api` | CQRS + Event Sourcing (086): Postgres ürün-stream event-log (gerçek-kaynak) + async projection → Elasticsearch dokümanı; tek asistan tool `query_storefront` = ham ES Query DSL (text+fuzzy+kNN+filtre; minimal rail + `AgentQueryLog`) |
| `customer-api` | Wallet (tokenize kart, PAN yok; kart YAZMA yüzeyi yok — salt okuma + payment-context) + AddressBook + `MerchantInformation`; merchant-admin tool'ları tek korumalı `/mcp`'de (085); 087: makine-handoff PG onboarding (S2S register + HMAC credential dönüşü — sır insan-yüzeyde render edilmez) |
| `reviews-api` | Satın-alma şartlı yorum (1–5★); hak `OrderCompleted` event'inden; yorum hemen görünür (moderasyon yok); puan özeti Storefront'a |
| `library-api` | Kullanıcı-ürün ilgi kayıtları + yaşayan fiyat alarmı aboneliği (email snapshot) + `NotificationRecord`; `ProductChangedEvent.OldPrice` tetiği → `PriceAlarmTriggered`; `UserPurchase` kişisel satın-alma birikimi (086, `OrderCompleted` upsert) |
| `discount-api` | Admin kampanya indirimi (079): süzgeç (kategori/yazar/yayınevi/tek-kitap) → yüzde; kitap-başına tek indirim, vitrine event'le iter, süre dolunca temizler; fiyat tutmaz — Order'a gRPC aktif-yüzde |
| `file-api` | Kapak kayıt defteri (081/083): `FileAsset` (ImageName=ISBN unique) + çoklu fiziki depo (`IFileStore`: R2/Local); `ProductAdded` tüketir → kapağı yükler → `CoverIngested` yayar; anonim cover serve + S2S resolve |
| `checkout-orchestrator` | Standalone broker-only checkout sağası (`checkoutDb`): `CommitStock → Confirm → ClearBasket`; LIFO telafi + watchdog (ödeme öncedendir) |
| `gateway` | YARP reverse proxy / tek giriş (MCP + PRM rotaları) |
| _(identity-server)_ | **TAŞINDI → AgentPlatform** (084): platform IdP — OpenIddict + ASP.NET Identity, OIDC/OAuth + RBAC (rol = scope demeti) + DCR + consent + revocation; ECommerce relying party, AppHost proje-ref'i YOK |
| _(MCP fasadı)_ | **TAŞINDI → AgentPlatform** (001): tek MCP fasadı artık bu repoda değil; AgentPlatform'ın Aspire host'unda koşar, EC `/mcp` uçlarını sabit URL'lerle downstream toplar. EC ürün `/mcp` uçları KORUNUR |
| `notification-agent` | Durumsuz worker — `PriceAlarmTriggered → LLM compose → Mail.Mcp send_mail → NotificationSent`; DB yok |
| `mail-mcp` | İlk standalone MCP server; tek tool `send_mail` (MailKit → Mailpit); yalnız Notification Agent tüketir |

Paylaşılan temeller `src/others` altında: `Common` (domain yapı taşları, result, caching, dead-letter araçları) ve `Shared` (integration-event kontratları + gRPC protolar + MCP tool adları/açıklamaları).

## Öne Çıkan Tasarım Kararları

- **Bir mikroservis = bir bounded context.** Sınır fiziksel ve sert: ayrı veritabanı, ayrı şema, ayrı domain modeli. Servisler DB paylaşmaz, bir context'in modelini diğerine sızdırmaz.
- **Aggregate'ler invariant'larının sahibidir.** Yeni kural handler'a değil aggregate metoduna gider. Koleksiyonlar private, salt-okunur açılır; mutasyon yalnız davranış metotlarından akar.
- **Exception yerine Result.** Tüm handler, aggregate metodu ve endpoint bir `Result` döner; endpoint `IsSuccess`'i `Ok`/`BadRequest`'e çevirir.
- **Agent-only müşteri yüzeyi, tek fasad.** Her müşteri işlemi MCP paritesine ulaşınca ekranlar ve mağazanın kendi agent'ı söküldü; mağaza artık BYO-agent. Müşteri kendi AI istemcisiyle tek platform MCP fasadına (AgentPlatform) bağlanır, tek login yeterlidir.
- **MCP yalnız agent tüketir.** Agent olmayan kod imperatif `CallToolAsync` süremez → REST/gRPC/S2S. MCP tool'ları ince sarmalayıcıdır: aynı Wolverine command/query'yi çağırır, yalnız LLM-dostu ad + açıklama ekler; sıfır iş-mantığı tekrarı.
- **Saga bir servistir, bir god-object değil.** Checkout orkestrasyonu dört context'e yayılan bir süreç sahibidir, o yüzden **kendi BC'sinde** yaşar ve yalnız broker command/reply konuşur — asla başka servisin veritabanı.
- **İki-fazlı ödeme yerine hosted-CF.** Ödeme dış PG'nin hosted sayfasında, checkout'tan **önce** olur; başarı HMAC callback ile doğrulanır. Saga içinde authorize/capture/void makinesi yoktur — ödeme saga dışıdır, saga yalnız stok+onay sürer.
- **Servisler arası anlık evet/hayır gereken yerde senkron RPC.** Stok rezervi (basket/order → stock) sanksiyonlu senkron kanaldır: tipli gRPC, scope-korumalı, fail-closed. DB izolasyonu korunur — çağıran Stock'un API'sine erişir, veritabanına değil.
- **Servisler arası transaction yerine idempotency.** Context'ler arası yazımlar transaction paylaşamaz; saga bunun yerine yakınsar: deterministik anahtarlar (`CheckoutId`/`TxRef`), en-az-bir-kez teslim, iş hataları sonsuz retry yerine telafiye yönlendirilir.
- **Eventual-consistency akışları event kullanır, gRPC değil.** Satın-alma sonrası yazılan yorum anlık yanıt gerektirmez, o yüzden hak `OrderCompleted` projeksiyonudur — gRPC yalnız anlık-tutarlılığa (stok) ayrılmıştır.
- **Rol downstream'e sızmaz — yalnız scope.** Identity rol verir (rol = scope demeti); servisler saf scope'a göre yetkilendirir. Okumalar (stok, storefront) anonim; token alışveriş yazma yolunda önemlidir.

## Başlangıç

### Ön koşullar

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Docker (Aspire; PostgreSQL, RabbitMQ, Redis, Elasticsearch, Kibana ve Mailpit'i container olarak sağlar)

### Tüm sistemi çalıştır

Dağıtık sistemi her zaman **Aspire AppHost** üzerinden başlat — servisler birbirini, veritabanlarını ve RabbitMQ'yu Aspire service discovery ile bulur. Tek bir API'yi bağımsız çalıştırmak bağımlılıklarını çözemez.

```bash
# Repo kökünden
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

Bu; her servisi, YARP gateway'i ve agent worker'larını, artı PostgreSQL, RabbitMQ (management eklentisiyle), Redis, Elasticsearch, Kibana ve Mailpit'i ayağa kaldırır. **Aspire dashboard** her kaynağın canlı görünümü, logları ve uçlarıyla açılır. Kimlik makamı bu repo'da DEĞİLDİR — platform IdP'si `AgentPlatform` repo'sunun kendi AppHost'uyla ayrıca başlatılır; korumalı yüzeyler token doğrulamayı ona (issuer URL) yapar.

> Platform IdP (AgentPlatform) **HTTPS** üzerinde çalışmalıdır (`SameSite=None; Secure` çerezleri düz HTTP'de sonsuz döner); servislerin `IdentityOption.Address`'i issuer URL'iyle birebir eşleşir.

OpenAI kullanan servisler (**Notification Agent**, ve embedding için **Storefront**) kimlik bilgisi olmadan açılışta fail-fast eder:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "<key>" --project src/agents/NotificationAgent/NotificationAgent.csproj
dotnet user-secrets set "OpenAI:Model"  "gpt-4o-mini" --project src/agents/NotificationAgent/NotificationAgent.csproj
dotnet user-secrets set "OpenAI:ApiKey" "<key>" --project src/services/storefront/Storefront.Api/Storefront.Api.csproj
```

### Kendi AI istemcini bağla

Müşteri yüzeyi bir MCP fasadıdır. Kendi MCP istemcin (Claude Desktop vb.) fasadın `/mcp` ucuna bağlanır; ilk bağlantıda tarayıcıda bir kez OAuth login + consent yapılır (RFC 7591 DCR ile istemci kendini kaydeder), sonrası refresh token'la ekransız sürer.

### Derle & test et

```bash
# Tüm çözümü derle
dotnet build

# Tüm testleri çalıştır
dotnet test

# Tek bir test projesi
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj
```

## Proje Yapısı

```
src/
  aspire/        AppHost (orkestrasyon) + ServiceDefaults
  services/      basket, catalog, checkout, customer, discount, file,
                 gateway, library, order, payment, reviews, stock, storefront
  others/        Common (yapı taşları, caching, dead-letter), Shared (kontratlar + protolar)
  agents/        Mail.Mcp (send_mail), NotificationAgent (fiyat alarmı)
                 # MCP fasadı + Identity → AgentPlatform repo'suna taşındı (001/084)
tests/           Servis başına domain birim testleri (xUnit + Shouldly)
.specify/        Spec-driven development kurulumu (spec-kit)
specs/           Feature spec / plan / task'ları
```

Tek bir servis **Vertical Slice** düzeni izler — kod teknik katmana değil domain feature'ına göre gruplanır:

```
Domains/<Aggregate>/
  <Aggregate>.cs                  # zengin aggregate root (fabrika + davranış metotları)
  <Aggregate>EndpointExtension.cs # Minimal API endpoint map'i
  Features/
    Commands/                     # yazma slice'ları  (IDocumentSession, [Transactional])
    Queries/                      # okuma slice'ları   (salt-okur)
    Agents/Commands|Queries/      # agent'a açık slice'lar — MCP tool sarmalayıcısı
                                  # slice'ıyla AYNI dosyada (ayrı McpTools.cs YOK)
```

Her bounded context ayrıca bir `FLOW.md` taşır — o context'in iş adımlarını, invariant'larını ve sınırını EventStorming irtifasında anlatan domain-süreç belgesi, `scripts/check-flow-links.sh` ile guard'lı.

## Notlar

- **Central Package Management** açık — paket sürümleri tek tek `.csproj`'larda değil `Directory.Packages.props`'ta yaşar.
- Önemsiz olmayan feature'lar **spec-kit** akışıyla (`/speckit-*`), `.specify/memory/` altındaki proje anayasasının yönetiminde geliştirilir. Artefakt derinliği feature boyutuna göre ölçeklenir.

---

*Domain-Driven mikroservisler ve .NET yığınında AI agent'larının pratik bir keşfi olarak inşa edildi.*