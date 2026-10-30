namespace Shared;

public static class RabbitMqConstants
{
    // OrderCreated exchange kaldirildi — sepet temizligi artik CheckoutSaga'nin gRPC adimidir.
    // StorefrontEvents kuyruğu SÖKÜLDÜ — Storefront BC kaldırıldı (arama pg-hibrit'e taşınıyor).

    public static class ProductChanged
    {
        public const string Exchange = "product.changed";

        public static class Queues
        {
            // Library fiyat değişimini dinler (alarm tetiği); binding'i tüketici kurar (007).
            public const string Library = "library.events";

            // Discount ürün↔kategori/yazar/yayınevi izdüşümünü (ProductCatalogRef) besler; binding tüketici kurar.
            public const string Discount = "discount.events";
        }
    }

    // 050/051: Catalog yeni ürün YAYINLANINCA yayınlar. Dijital pivot: Stock BC söküldü → tek tüketici File
    // (kapak çözümü). Tüketici başına TEK sıralı kuyruk; binding'i tüketici kurar (007).
    public static class ProductAdded
    {
        public const string Exchange = "catalog.product-added";

        public static class Queues
        {
            // File.Api fanout exchange'e kendi kuyruğunu bağlar (kapak çözümü); binding tüketici kurar.
            public const string File = "file.product-added";
        }
    }

    // File.Api yayınlar (kapak R2'de hazır + registry upsert sonrası), Catalog tüketir (kendi kuyruğunu
    // bağlar — 007 soğuk-açılış dersi) → Product.SetImage → ProductChangedEvent → Storefront.
    public static class CoverIngested
    {
        public const string Exchange = "file.cover-ingested";

        public static class Queues
        {
            public const string Catalog = "catalog.cover-ingested";
        }
    }

    // ReviewSummaryChanged exchange SÖKÜLDÜ — tek tüketici Storefront'tu, o BC kaldırıldı.

    // Order yayınlar (checkout başarı = Confirm pivotu). Reviews tüketir (satın-alma kanıtı projeksiyonu).
    // 086: Library tüketir (kişisel UserPurchase birikimi — Storefront'tan taşındı); Storefront artık DİNLEMEZ.
    public static class OrderCompleted
    {
        public const string Exchange = "order.completed";

        public static class Queues
        {
            public const string Reviews = "reviews.order-completed";
            public const string Library = "library.order-completed";

            // 092: Basket sipariş tamamlanınca sepeti siler (checkout koreografisi); binding'i tüketici kurar.
            public const string Basket = "basket.order-completed";
        }
    }

    // Library yayınlar (üründeki her alarm için bir event), NotificationAgent tüketir
    // (worker kendi kuyruğunu bağlar).
    public static class PriceAlarmTriggered
    {
        public const string Exchange = "library.price-alarm-triggered";

        public static class Queues
        {
            public const string Worker = "notifications.price-alarm-triggered";
        }
    }

    // NotificationAgent yayınlar (gönderim sonucu), Library tüketir → NotificationRecord izi.
    public static class NotificationSent
    {
        public const string Exchange = "notifications.sent";

        public static class Queues
        {
            public const string Library = "library.notifications-sent";
        }
    }

    // Payment yayınlar (hosted-CF callback/expiry sonucu), Order tüketir (kendi kuyruğunu bağlar).
    public static class PaymentSucceeded
    {
        public const string Exchange = "payment.succeeded";

        public static class Queues
        {
            public const string Order = "order.payment-succeeded";
        }
    }

    public static class PaymentFailed
    {
        public const string Exchange = "payment.failed";

        public static class Queues
        {
            public const string Order = "order.payment-failed";
        }
    }

    // ProductDiscountChanged exchange SÖKÜLDÜ — tek tüketici Storefront'tu, o BC kaldırıldı.
    // İndirim yüzdesi Order'a checkout gRPC'siyle akar (ayrı kanal).

    // dijital (PDF) pivot: Stock BC söküldü → checkout stok-commit koreografisi (StockCommitRequested/
    // Committed/Failed) kalktı. Ödeme sonucu doğrudan Order.Confirm/Cancel tetikler (Order PaymentConsumers).
}