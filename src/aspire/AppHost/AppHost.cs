var builder = DistributedApplication.CreateBuilder(args);

// Yayın hedefi (k8s): `aspire publish -o k8s-artifacts` AddProject'leri Deployment/Service/ConfigMap/Secret
// + Helm chart'a çevirir; `aspire do push` image'leri ghcr'a iter. Yalnız publish/push'ta devreye girer —
// yerel `aspire run` dev akışı değişmez. (compose publisher kaldırıldı — tek hedef k8s.)
builder.AddKubernetesEnvironment("k8s");

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

// Elasticsearch + Kibana SÖKÜLDÜ — tek tüketici Storefront BC'ydi, o kaldırıldı (arama pg-hibrit'e
// taşınıyor: Catalog'da pg_search BM25 + pgvector + RRF). ES container/volume + Kibana görünürlük aracı gitti.

var catalogDb = postgres.AddDatabase("catalogDb");
var basketDb = postgres.AddDatabase("basketDb");
var orderDb = postgres.AddDatabase("orderDb");
var paymentDb = postgres.AddDatabase("paymentDb");
var customerDb = postgres.AddDatabase("customerDb");
var discountDb = postgres.AddDatabase("discountDb");
var fileDb = postgres.AddDatabase("fileDb");

// identity-server AgentPlatform repo'suna taşındı (ayrı Aspire AppHost). ECommerce servisleri
// IdP'yi IdentityOption.Address (sabit issuer URL, https://localhost:5001) ile bulur — proje-ref/
// service-discovery değil. identityDb de AgentPlatform'da; buradaki identityDb kaydı söküldü.

// 091: commit'li seed verisi (tek dosya, iki tüketici — Catalog seeder + File kapak migration'ı;
// sözleşme specs/091-catalog-json-seed/contracts/seed-record.md).
var seedBooksJson = Path.GetFullPath(Path.Combine(
    builder.AppHostDirectory, "..", "..", "services", "catalog", "Catalog.Api", "Seeding", "Data", "books.json"));

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithHttpHealthCheck("/health")
    .WithReference(catalogDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WithEnvironment("SeedOptions__SourceJsonPath", seedBooksJson)
    .WaitFor(catalogDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

// dijital (PDF) pivot: Stock BC söküldü — sınırsız kopya, stok/rezervasyon yok.
var basketApi = builder.AddProject<Projects.Basket_Api>("basket-api")
    .WithHttpHealthCheck("/health")
    .WithReference(basketDb)
    .WithReference(rabbit)
    .WithReference(redis)
    .WaitFor(basketDb)
    .WaitFor(rabbit)
    .WaitFor(redis);

var orderApi = builder.AddProject<Projects.Order_Api>("order-api")
    .WithHttpHealthCheck("/health")
    .WithReference(orderDb)
    .WithReference(rabbit)
    // start_payment sepet okuması Basket gRPC ucunu çağırır (GetBasketItems).
    .WithReference(basketApi)
    .WithReference(redis)
    .WaitFor(orderDb)
    .WaitFor(rabbit)
    .WaitFor(basketApi)
    .WaitFor(redis);

// Storefront.Api SÖKÜLDÜ — arama Catalog pg-hibrit'e taşınıyor; ES/event-log yüzeyi + storefrontDb gitti.

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

// 092: Checkout.Orchestrator + checkoutDb SÖKÜLDÜ — checkout artık koreografi (Order/Stock/Basket
// event tüketicileri). Ayrı orkestrasyon servisi yok.

// Reviews BC — satin-alma sartli yorum + puan ozeti. Satin-alma kaniti icin Order gRPC'sine
// senkron sorar (fail-closed). ReviewSummaryChanged fanout'u SÖKÜLDÜ (tek tüketici Storefront'tu).
var reviewsDb = postgres.AddDatabase("reviewsDb");
var reviewsApi = builder.AddProject<Projects.Reviews_Api>("reviews-api")
    .WithReference(reviewsDb)
    .WithReference(rabbit)
    .WithReference(orderApi)
    .WaitFor(reviewsDb)
    .WaitFor(rabbit)
    .WaitFor(orderApi);

// Library BC — fiyat alarmı (yaşayan abonelik) + bildirim izi. Catalog'un product.changed
// fanout'unu dinler, alarm başına PriceAlarmTriggered yayınlar, NotificationSent izini yazar.
var libraryDb = postgres.AddDatabase("libraryDb");
var libraryApi = builder.AddProject<Projects.Library_Api>("library-api")
    .WithReference(libraryDb)
    .WithReference(rabbit)
    .WaitFor(libraryDb)
    .WaitFor(rabbit);

// Discount BC — admin kampanya indirimi. Catalog product.changed'i tüketir (ProductCatalogRef),
// checkout gRPC ile Order'a aktif yüzde döner. ProductDiscountChanged vitrin event'i SÖKÜLDÜ
// (tek tüketici Storefront'tu). Kendi discountDb'si.
var discountApi = builder.AddProject<Projects.Discount_Api>("discount-api")
    .WithHttpHealthCheck("/health")
    .WithReference(discountDb)
    .WithReference(rabbit)
    .WaitFor(discountDb)
    .WaitFor(rabbit);

// Order.Api → Discount.Api checkout gRPC (start_payment aktif yüzde doğrulama, discount.read).
// discountApi orderApi'den SONRA tanımlı → referans burada (paymentApi emsali).
orderApi.WithReference(discountApi).WaitFor(discountApi);

// Mailpit + Mail.Mcp + NotificationAgent SÖKÜLDÜ — tüm MCP temizlendi (Mail.Mcp standalone MCP
// server'dı; NotificationAgent onu MCP client'la sürüyordu). Fiyat-alarmı mail hattı kalktı.

// MCP fasadı AgentPlatform'a TAŞINDI — platform tek MCP girişini de barındırır (Anayasa İlke III).
// EC ürün servislerinin /mcp uçları KORUNUR; fasad platform Aspire host'unda koşar ve buraya sabit
// mutlak URL'lerle downstream olarak bağlanır. Bu repoda fasad projesi/route'u kalmadı.

// File.Api — DB'siz kapak deposu (Mail.Mcp emsali). Kalıcı host diskine yazar (reset'e dayanıklı).
// RootPath = kalıcı host dizini; migration kaynağı = commit'li seed books.json (091 — Catalog seeder'la
// AYNI dosya; repo-dışı xlsx bağımlılığı bitti). Env Options ile enjekte.
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var coverRootPath = Path.Combine(home, "dev", "catalog-data", "cover-store");
var fileApi = builder.AddProject<Projects.File_Api>("file-api")
    .WithReference(fileDb).WaitFor(fileDb)   // kayıt defteri (Marten fileDb)
    .WithReference(rabbit).WaitFor(rabbit)   // kapak akışı (ProductAdded tüket → CoverIngested yay)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("CoverStore__RootPath", coverRootPath)
    .WithEnvironment("CoverMigration__Enabled", "true")
    .WithEnvironment("CoverMigration__SourceJsonPath", seedBooksJson)
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

// Gateway (YARP) SÖKÜLDÜ (2026-10-19) — ajan trafiği AgentPlatform fasadından BC /mcp'lerine
// mutlak URL'le gider, kapak URL'leri R2 public base'e çözülür; rotası kalmamıştı. Canlıda
// kenar işi k8s ingress'in. Local-depo fallback günü gelirse YARP 5 dk'da geri yazılır.

// WebApp (UI) + ChatAgent SÖKÜLDÜ (2026-09-11) — agent-only/BYO-agent yönü: müşteri kendi AI istemcisiyle
// platform MCP fasadına bağlanır; mağaza kendi ekranını/agent'ını host etmez. Admin de aynı /mcp'de (085).

// Tüm deploy edilebilir servisleri ghcr'a bağla — tek tek değil, döngüyle. `aspire do push`
// bunların image'ini basıp ghcr.io/hasancagri/ecommerce/<servis>'e iter. Altyapı (postgres/rabbit/
// redis/mailpit) public image → girmez.
foreach (var svc in new[]
{
    catalogApi, basketApi, orderApi, paymentApi, customerApi,
    reviewsApi, libraryApi, discountApi,
    fileApi
})
{
    svc.WithContainerRegistry(ghcr);
}

await builder.Build().RunAsync();