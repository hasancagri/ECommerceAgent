namespace Shared;

public static class IntegrationEvents
{
    // OrderCreatedEvent kaldirildi — sepet temizligi CheckoutSaga'nin gRPC adimina tasindi.

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    // 006-home-storefront-list: Description/Price/Brand eklendi.
    // 016-category-brand: kimlik + ad birlikte taşınır (R7); Id opak değerdir, tüketici lookup yapmaz.
    // Kategori zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün domain'de yoktur.
    // ProductSpec SÖKÜLDÜ — SpecificationAttribute facet sistemi + tek tüketicisi Storefront kaldırıldı.

    // kitap künyesi — yazar (Id+ad çifti; paralel-liste kırılganlığı olmadan taşınır).
    public record AuthorRef(Guid Id, string Name);

    // kırıcı evrim (tek tüketici Storefront, aynı PR, DB sıfırdan seed). BrandId/Brand çıktı;
    // çok-yazar (Authors) + tek yayınevi (PublisherId+Publisher, fat: tüketici lookup yapmaz) geldi.
    public record ProductChangedEvent(
        Guid ProductId,
        string Name,
        string Description,
        decimal Price,
        List<AuthorRef> Authors,
        Guid PublisherId,
        string Publisher,
        Guid CategoryId,
        string Category,
        string? ImageUrl,
        bool IsDeleted,
        // varyant ailesi kodu (opsiyonel; null = ailesiz).
        string? FamilyCode = null,
        // OldPrice SÖKÜLDÜ (097): fiyat doğumda sabit — "eski fiyat" kavramı kontrattan çıktı.
        // 095: kategori AD zinciri = leaf + ata adları (yayın anında ağaçtan çözülür; related GİRMEZ).
        // null/boş = yalnız tekil Category kullan (eski tüketici kırılmaz; CategoryId hâlâ primary leaf).
        List<string>? Categories = null,
        // 095 additive: kategori ID zinciri = leaf + ata id'leri (Categories ile aynı sıra/semantik).
        // Üst-kategori eşleşmesi gereken tüketiciler bundan beslenir (ör. 096 keşif); null görürse
        // tüketici [CategoryId] fallback'ine düşer (eski event kırılmaz).
        List<Guid>? CategoryIds = null);

    // Catalog → File (dijital pivot: Stock BC söküldü). Yeni ürün yayınlanınca yayılır; File kapak
    // çözümünü (ISBN/Barcode) tetikler. InitialStock SÖKÜLDÜ (stok yok — Stock BC kaldırıldı).
    public record ProductAdded(
        string Barcode,
        Guid ProductId);

    // File.Api → Catalog. Kapak R2'de hazır + registry upsert sonrası yayılır. Url = r2.dev public
    // (CoverUrlResolver çıktısı; r2.dev base Catalog'a sızmaz — İLKE I). Tüketicisi YOK (097 serisi:
    // Catalog ayağı söküldü, kullanıcı kararı — ürünler kapaksız); kontrat ileriye dönük durur.
    public record CoverIngested(string Isbn, string Url);

    // ReviewSummaryChanged SÖKÜLDÜ — tek tüketici Storefront'tu, o BC kaldırıldı. Rating özeti
    // Reviews aggregate'inde durur, yayınlanmaz (arama pg-hibrit'e taşınınca yeniden değerlendirilir).

    // Order → Reviews. YALNIZ odeme onayli tamamlanan siparis (Confirm pivotu) icin yayilir;
    // olusturulan/odenmemis DEGIL. Reviews satin-alma kanitini (yorum hakki) bu event'ten projeksiyonlar.
    // Category/Brand nullable: Order bunlari tutmuyorsa null (BC izolasyonu).
    // Additive: yeni alan default'lu eklenir, eski tuketici kirilmaz.
    public record OrderCompleted(
        Guid OrderId,
        Guid UserId,
        DateTimeOffset OrderedAt,
        IReadOnlyList<OrderCompletedItem> Items);

    public record OrderCompletedItem(
        Guid ProductId,
        int Quantity,
        decimal UnitPrice,
        string? Category = null,
        string? Brand = null);

    // Library → NotificationAgent. Üründeki HER alarm için bir event (alarm açık kalır — yaşayan
    // abonelik, FR-004). Mail'e yetecek her alan event'te; worker başka servise SORMAZ (email snapshot).
    public record PriceAlarmTriggered(
        Guid AlarmId,
        Guid UserId,
        string Email,
        Guid ProductId,
        string ProductName,
        decimal OldPrice,
        decimal NewPrice);

    // NotificationAgent → Library. Gönderim denemesinin sonucu; Library NotificationRecord izi yazar.
    // Detail: "sent" | "no-email" | kısa hata özeti.
    public record NotificationSent(
        Guid UserId,
        Guid ProductId,
        string Email,
        bool Success,
        string Detail);

    // Payment → Order (fanout). Hosted-CF ödeme başarılı callback'i / expiry sonucu. Yayıncı Payment
    // (durable outbox); tüketici Order (binding kurar — soğuk-açılış dersi). Additive: eski tüketici yok.
    public record PaymentSucceeded(Guid OrderId, Guid UserId, Guid PaymentIntentId, string TxRef, decimal Amount);
    public record PaymentFailed(Guid OrderId, Guid PaymentIntentId, string TxRef, string ReasonCode);

    // dijital (PDF) pivot: Stock BC + checkout stok-commit koreografisi (StockCommitRequested/Committed/
    // Failed) söküldü. PaymentSucceeded → doğrudan Order.Confirm + OrderCompleted (Order PaymentConsumers).

    // ProductDiscountChanged SÖKÜLDÜ — tek tüketici Storefront'tu, o BC kaldırıldı. İndirim yüzdesi
    // Order'a checkout gRPC'siyle (ayrı kanal) akmaya devam eder; vitrin event'i artık yayılmaz.
}