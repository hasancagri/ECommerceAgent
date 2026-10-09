var builder = DistributedApplication.CreateBuilder(args);

// Yayın hedefi (publish/push-only): `aspire do push` her AddProject'i container image'ine çevirip ghcr'a iter.
// Yalnız publish/push'ta devreye girer — yerel `aspire run` dev akışı değişmez.
builder.AddDockerComposeEnvironment("compose");

// Otopark (registry) = ghcr.io/hasancagri/ecommerce. PREFIX "ecommerce" ZORUNLU: payment-api/gateway gibi
// isimler AgentPlatform + PaymentGateway ile çakışır; repo-başına ayrı namespace → çakışma yok.
var ghcr = builder.AddContainerRegistry("ghcr", "ghcr.io", "hasancagri/ecommerce");

// pgvector'lu resmi imaj. pg17 = Aspire default'u (postgres:17.x) ile ayni veri yolu; mevcut
// volume uyumlu. pg18 tag'i KULLANMA (WithDataVolume tag'i parse edemez, 17-yolunu mount eder).
// WithImage, WithDataVolume'dan ONCE: veri yolu o andaki imaj annotation'indan cozulur.
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// L2 (paylaşımlı) önbellek katmanı — HybridCache'in IDistributedCache backing'i (opsiyonel).
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

// 086: Storefront arama projeksiyonu = Elasticsearch (tek node dev). Kalıcı + data volume:
// veri her zaman event-log'dan yeniden kurulabilir (rebuildable), ama reset'te boş-yeniden-kurma
// maliyetinden kaçınmak için kalıcı. Yalnız storefront-api referanslar.
var elasticsearch = builder.AddElasticsearch("elasticsearch")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Kibana — ES'i gözle gezmek (Discover + Dev Tools): index içeriği, mapping, DSL deneme.
// Dev-only görünürlük aracı; arama yolu programatik (Claude Desktop → ES DSL). Kibana 8.x elastic
// superuser'ı reddeder (system indices) → service-account token şart. Token ES data volume'de
// kalıcı (restart'ta ölmez, yalnız tam reset'te); secret parameter'dan (user-secrets) enjekte.
// (Yeniden) üretim: scripts/kibana-service-token.sh çıktısını user-secrets'a yaz.
var kibanaToken = builder.AddParameter("kibana-service-token", secret: true);
builder.AddContainer("kibana", "docker.elastic.co/kibana/kibana", "8.17.3")
    .WithEnvironment("ELASTICSEARCH_HOSTS", elasticsearch.GetEndpoint("http"))
    .WithEnvironment("ELASTICSEARCH_SERVICEACCOUNTTOKEN", kibanaToken)
    .WithHttpEndpoint(targetPort: 5601, name: "http")
    .WaitFor(elasticsearch)
    .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = postgres.AddDatabase("catalogDb");
var basketDb = postgres.AddDatabase("basketDb");
var orderDb = postgres.AddDatabase("orderDb");
var paymentDb = postgres.AddDatabase("paymentDb");
var stockDb = postgres.AddDatabase("stockDb");
var storefrontDb = postgres.AddDatabase("storefrontDb");
var customerDb = postgres.AddDatabase("customerDb");
var checkoutDb = postgres.AddDatabase("checkoutDb");
var discountDb = postgres.AddDatabase("discountDb");
var fileDb = postgres.AddDatabase("fileDb");

// identity-server AgentPlatform repo'suna taşındı (ayrı Aspire AppHost). ECommerce servisleri
// IdP'yi IdentityOption.Address (sabit issuer URL, https://localhost:5001) ile bulur — proje-ref/
// service-discovery değil. identityDb de AgentPlatform'da; buradaki identityDb kaydı söküldü.

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithHttpHealthCheck("/health")
    .WithReference(catalogDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(catalogDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

var stockApi = builder.AddProject<Projects.Stock_Api>("stock-api")
    .WithHttpHealthCheck("/health")
    .WithReference(stockDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(stockDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

// Basket & Order, Stock'a senkron gRPC (rezervasyon Reserve/Release/Commit) çağırır.
var basketApi = builder.AddProject<Projects.Basket_Api>("basket-api")
    .WithHttpHealthCheck("/health")
    .WithReference(basketDb)
    .WithReference(rabbit)
    .WithReference(stockApi)
    .WithReference(redis)
    .WaitFor(basketDb)
    .WaitFor(rabbit)
    .WaitFor(stockApi)
    .WaitFor(redis);

var orderApi = builder.AddProject<Projects.Order_Api>("order-api")
    .WithHttpHealthCheck("/health")
    .WithReference(orderDb)
    .WithReference(rabbit)
    .WithReference(stockApi)
    // checkout saga ClearBasket adimi Basket gRPC ucunu cagirir.
    .WithReference(basketApi)
    .WithReference(redis)
    .WaitFor(orderDb)
    .WaitFor(rabbit)
    .WaitFor(stockApi)
    .WaitFor(basketApi)
    .WaitFor(redis);

// 086: Redis SÖKÜLDÜ — yeni ES/event-log yüzeyinde cache'lenen slice yok (read-model REST kalıntısı gitti).
var storefrontApi = builder.AddProject<Projects.Storefront_Api>("storefront-api")
    .WithHttpHealthCheck("/health")
    .WithReference(storefrontDb)
    .WithReference(rabbit)
    .WithReference(elasticsearch)
    .WaitFor(storefrontDb)
    .WaitFor(rabbit)
    .WaitFor(elasticsearch);

var paymentApi = builder.AddProject<Projects.Payment_Api>("payment-api")
    .WithHttpHealthCheck("/health")
    .WithReference(paymentDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(paymentDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

// Customer BC — Wallet (kayitli kart) + AddressBook (adres defteri). Kendi DB'si;
// bu feature'da servisler-arasi event/gRPC yok (identity token'iyla korunan salt CRUD + MCP okuma).
var customerApi = builder.AddProject<Projects.Customer_Api>("customer-api")
    .WithHttpHealthCheck("/health")
    .WithReference(customerDb)
    .WithReference(redis)
    .WaitFor(customerDb)
    .WaitFor(redis);

// chat siparis tamamlama — Order.Api odeme baglamini (buyer+vaultToken+adres) Customer'dan
// yapisal REST ile ceker (customerApi orderApi'den SONRA tanimli oldugu icin referans burada eklenir).
orderApi.WithReference(customerApi).WaitFor(customerApi);

// Payment.Api → Customer merchant-key S2S (hosted-CF PG X-Api-Key kaynağı). customerApi
// paymentApi'den SONRA tanımlı → service-discovery referansı burada eklenir (orderApi emsali).
// Eksikse services:customer-api:* config null → fallback çözümsüz host → merchant-key null.
paymentApi.WithReference(customerApi).WaitFor(customerApi);

// Order.Api → Payment.Api hosted-CF link isteği (start_payment S2S). paymentApi orderApi'den
// SONRA tanımlı → referans burada. Eksikse services:payment-api:* null → fallback çözümsüz host →
// CreateAsync null → "Ödeme başlatılamadı".
orderApi.WithReference(paymentApi).WaitFor(paymentApi);

// Checkout.Orchestrator — ayrı BC (checkoutDb), broker-only saga. Komutları hedef BC'lere
// yayınlar, yanıtları reply kuyruğundan dinler. BC komut-kuyruğu tüketicileri önce ayağa kalksın
// (soğuk-açılış binding dersi, 007). Giriş endpoint'i checkout.write ile korunur (identity).
var checkoutOrchestrator = builder.AddProject<Projects.Checkout_Orchestrator>("checkout-orchestrator")
    .WithReference(checkoutDb)
    .WithReference(rabbit)
    .WaitFor(checkoutDb)
    .WaitFor(rabbit)
    .WaitFor(orderApi)
    .WaitFor(stockApi)
    .WaitFor(paymentApi)
    .WaitFor(basketApi);

// Reviews BC — satin-alma sartli yorum + puan ozeti. Satin-alma kaniti icin Order gRPC'sine
// senkron sorar (fail-closed); ozet ReviewSummaryChanged fanout'uyla Storefront'a akar.
var reviewsDb = postgres.AddDatabase("reviewsDb");
var reviewsApi = builder.AddProject<Projects.Reviews_Api>("reviews-api")
    .WithReference(reviewsDb)
    .WithReference(rabbit)
    .WithReference(orderApi)
    .WaitFor(reviewsDb)
    .WaitFor(rabbit)
    .WaitFor(orderApi)
    // Tuketici kuyrugu yayincidan once baglansin (007 dersi): Storefront reviews'tan once ayakta.
    .WaitFor(storefrontApi);

// Library BC — fiyat alarmı (yaşayan abonelik) + bildirim izi. Catalog'un product.changed
// fanout'unu dinler, alarm başına PriceAlarmTriggered yayınlar, NotificationSent izini yazar.
var libraryDb = postgres.AddDatabase("libraryDb");
var libraryApi = builder.AddProject<Projects.Library_Api>("library-api")
    .WithReference(libraryDb)
    .WithReference(rabbit)
    .WaitFor(libraryDb)
    .WaitFor(rabbit);

// Discount BC — admin kampanya indirimi. Catalog product.changed'i tüketir (ProductCatalogRef),
// ProductDiscountChanged'i Storefront'a iter (tüketici binding'i önce kalksın → WaitFor storefront), checkout
// gRPC ile Order'a aktif yüzde döner. Kendi discountDb'si.
var discountApi = builder.AddProject<Projects.Discount_Api>("discount-api")
    .WithHttpHealthCheck("/health")
    .WithReference(discountDb)
    .WithReference(rabbit)
    .WaitFor(discountDb)
    .WaitFor(rabbit)
    // Storefront discount exchange kuyruğunu bağlasın (007 soğuk-açılış dersi) — yayından önce ayakta.
    .WaitFor(storefrontApi);

// Order.Api → Discount.Api checkout gRPC (start_payment aktif yüzde doğrulama, discount.read).
// discountApi orderApi'den SONRA tanımlı → referans burada (paymentApi emsali).
orderApi.WithReference(discountApi).WaitFor(discountApi);

// Mailpit — dev posta kutusu (ham container; SMTP 1025 + web UI 8025).
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp")
    .WithLifetime(ContainerLifetime.Persistent);
var mailpitSmtp = mailpit.GetEndpoint("smtp");

// Mail.Mcp — ilk standalone MCP server (send_mail). SMTP hedefi Mailpit endpoint'inden
// env ile (Options pattern SmtpOptions; IConfiguration'dan doğrudan okuma yok).
var mailMcp = builder.AddProject<Projects.Mail_Mcp>("mail-mcp")
    .WithEnvironment(ctx =>
    {
        ctx.EnvironmentVariables["Smtp__Host"] = mailpitSmtp.Property(EndpointProperty.Host);
        ctx.EnvironmentVariables["Smtp__Port"] = mailpitSmtp.Property(EndpointProperty.Port);
    })
    .WaitFor(mailpit);

// NotificationAgent — DB'siz worker (Reviews.Moderation emsali); PriceAlarmTriggered tüketir,
// maili kişiselleştirip Mail.Mcp üzerinden gönderir. WebApp base-url env'i aşağıda (web tanımlanınca).
var notificationAgent = builder.AddProject<Projects.NotificationAgent>("notification-agent")
    .WithReference(rabbit)
    .WithReference(mailMcp)
    .WaitFor(rabbit)
    .WaitFor(mailMcp);

// MCP fasadı AgentPlatform'a TAŞINDI — platform tek MCP girişini de barındırır (Anayasa İlke III).
// EC ürün servislerinin /mcp uçları KORUNUR; fasad platform Aspire host'unda koşar ve buraya sabit
// mutlak URL'lerle downstream olarak bağlanır. Bu repoda fasad projesi/route'u kalmadı.

// File.Api — DB'siz kapak deposu (Mail.Mcp emsali). Kalıcı host diskine yazar (reset'e dayanıklı).
// RootPath = kalıcı host dizini; migration kaynağı = repo-dışı catalog-import.xlsx. Env Options ile enjekte.
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var coverRootPath = Path.Combine(home, "dev", "catalog-data", "cover-store");
var coverSourceXlsx = Path.Combine(home, "dev", "catalog-data", "catalog-import.xlsx");
var fileApi = builder.AddProject<Projects.File_Api>("file-api")
    .WithReference(fileDb).WaitFor(fileDb)   // kayıt defteri (Marten fileDb)
    .WithReference(rabbit).WaitFor(rabbit)   // kapak akışı (ProductAdded tüket → CoverIngested yay)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("CoverStore__RootPath", coverRootPath)
    .WithEnvironment("CoverMigration__Enabled", "true")
    .WithEnvironment("CoverMigration__SourceXlsxPath", coverSourceXlsx)
    .WithEnvironment("CoverMigration__DownloadTimeoutSeconds", "30")
    // R2 backend (credential user-secrets'te; AccountId/Bucket non-secret). Serve+backfill R2'den.
    .WithEnvironment("CoverStore__Backend", "R2")
    .WithEnvironment("R2__AccountId", "92f68fb7048d6312c566a6c28e08dcdb")
    .WithEnvironment("R2__BucketName", "ecommercebucket")
    // URL resolver — tercih edilen depo R2; public base (r2.dev) resolve URL'i için.
    .WithEnvironment("StorageBaseUrls__DefaultStorageType", "R2")
    // r2.dev public base (non-secret) — CoverUrlResolver bunu okur. Eksikti → Resolve() null →
    // CoverIngested hiç yayılmıyordu (Product.ImageUrl boş kalıyordu). Public URL, repo'ya güvenli.
    .WithEnvironment("StorageBaseUrls__Bases__R2", "https://pub-5232b4cfb5174c27bcea9dcdd6c4fec5.r2.dev")
    // US4: mevcut R2 kapakları kayıt defterine idempotent al (bir-kez; re-run yinelemez).
    .WithEnvironment("CoverMigration__RegistryBackfill__Enabled", "true");

var gateway = builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(catalogApi)
    .WithReference(basketApi)
    .WithReference(orderApi)
    .WithReference(paymentApi)
    .WithReference(stockApi)
    .WithReference(storefrontApi)
    .WithReference(customerApi)
    .WithReference(reviewsApi)
    // Library MCP gateway üzerinden (dış agent fiyat alarmı); service discovery için referans.
    .WithReference(libraryApi)
    // kapak görseli servis (anonim /files/**) gateway üzerinden.
    .WithReference(fileApi);

// WebApp (UI) + ChatAgent SÖKÜLDÜ (2026-09-11) — agent-only/BYO-agent yönü: müşteri kendi AI istemcisiyle
// platform MCP fasadına bağlanır; mağaza kendi ekranını/agent'ını host etmez. Admin de aynı /mcp'de (085).

// Tüm deploy edilebilir servisleri (15) ghcr'a bağla — tek tek değil, döngüyle. `aspire do push`
// bunların image'ini basıp ghcr.io/hasancagri/ecommerce/<servis>'e iter. Altyapı (postgres/rabbit/
// redis/es/kibana/mailpit) public image → girmez.
foreach (var svc in new[]
{
    catalogApi, stockApi, basketApi, orderApi, storefrontApi, paymentApi, customerApi,
    checkoutOrchestrator, reviewsApi, libraryApi, discountApi, mailMcp, notificationAgent,
    fileApi, gateway
})
{
    svc.WithContainerRegistry(ghcr);
}

await builder.Build().RunAsync();