namespace Shared;

public static class IntegrationEvents
{
    // OrderCreatedEvent kaldirildi — sepet temizligi CheckoutSaga'nin gRPC adimina tasindi.

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    // 006-home-storefront-list: Description/Price/Brand eklendi.
    // 016-category-brand: kimlik + ad birlikte taşınır (R7); Id opak değerdir, tüketici lookup yapmaz.
    // Kategori zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün domain'de yoktur.
    // Specs — kanonik özellik AD çiftleri (Id taşınmaz; sözleşme=AD, taksonomi deseni).
    // Additive + opsiyonel: eski yayıncı/tüketici kırılmaz; null = özellik bilgisi yok (boş sayılır).
    public record ProductSpec(string Attribute, string Option);

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
        List<ProductSpec>? Specs = null,
        // varyant ailesi kodu (opsiyonel; null = ailesiz).
        string? FamilyCode = null,
        // YALNIZ fiyat değişiminde dolu (eski fiyat); null = fiyat-dışı değişiklik.
        // Additive default'lu — eski tüketici (Storefront) kırılmaz. Library tetik kararını bundan verir.
        decimal? OldPrice = null);
    public record StockChangedEvent(Guid ProductId, int Quantity);


    // 050/051: Catalog → Stock. Yalnız YENİ ürün YAYINLANINCA yayılır; Stock BarcodeLink eşlemesini kurar
    // ve OnHand'i InitialStock ile mutlak yazar. 051: ilk yayıncısı = kitap import ("Linked" feed-adı düştü).
    public record ProductAdded(
        string Barcode,
        Guid ProductId,
        int InitialStock);

    // File.Api → Catalog. Kapak R2'de hazır + registry upsert sonrası yayılır. Url = r2.dev public
    // (CoverUrlResolver çıktısı; r2.dev base Catalog'a sızmaz — İLKE I). Catalog FileConsumers tüketir →
    // Product.SetImage → ProductChangedEvent. Kapak yoksa event YOK (ürün placeholder ImageUrl'süz kalır).
    public record CoverIngested(string Isbn, string Url);

    // Reviews → Storefront. Visible yorumlardan MUTLAK özet (delta değil) — geç/yeniden teslim
    // son-yazan-kazanır ile güvenli. Count=0 ⇒ tüketici özeti temizler (rozet çizilmez).
    public record ReviewSummaryChanged(Guid ProductId, decimal Average, int Count);

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

    // Discount → Storefront (fanout). Bir kitabın tek indiriminin penceresini iter; indirim
    // kaldırılınca (bitiş/iptal) temizlik için DiscountPct=0 + null pencere gönderilir. Yayıncı Discount.Api
    // exchange deklare eder; binding'i TÜKETİCİ (Storefront) kurar (007 soğuk-açılış dersi). Discount.Api
    // FİYAT TUTMAZ — yalnız yüzde; etkin fiyatı Storefront kendi liste fiyatından hesaplar.
    public record ProductDiscountChanged(
        Guid ProductId,
        int DiscountPct,      // 0 = indirim yok (temizle); 1-99 = kitabın aktif/gelecek indirimi
        DateTime? StartsAt,   // pencere başlangıcı (null = temizlik)
        DateTime? EndsAt);    // pencere bitişi (null = süresiz ya da temizlik)
}