namespace Shared;

public static class RabbitMqConstants
{
    // OrderCreated exchange kaldirildi — sepet temizligi artik CheckoutSaga'nin gRPC adimidir.

    // Storefront TEK kuyruk dinler: üç exchange de aynı kuyruğa bağlanır ve Sequential işlenir.
    // Aynı StorefrontView satırına eşzamanlı yazım yapısal olarak imkânsızlaşır (ConcurrencyException
    // kaynağı yok edilir; Program.cs'teki retry kuralı yedek güvence olarak durur).
    public static class StorefrontEvents
    {
        public const string Queue = "storefront.events";
    }

    public static class ProductChanged
    {
        public const string Exchange = "product.changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;

            // Library fiyat değişimini dinler (alarm tetiği); binding'i tüketici kurar (007).
            public const string Library = "library.events";

            // Discount ürün↔kategori/yazar/yayınevi izdüşümünü (ProductCatalogRef) besler; binding tüketici kurar.
            public const string Discount = "discount.events";
        }
    }

    public static class StockChanged
    {
        public const string Exchange = "stock.changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;
        }
    }


    // 050/051: Catalog yeni ürün YAYINLANINCA yayınlar, Stock barkod↔ProductId eşlemesini kurar + ilk OnHand.
    // Tüketici başına TEK sıralı kuyruk (aynı barkod sıralı işlenir); binding'i tüketici kurar (007).
    public static class ProductAdded
    {
        public const string Exchange = "catalog.product-added";

        public static class Queues
        {
            public const string Stock = "stock.product-added";

            // File.Api aynı fanout exchange'e kendi kuyruğunu bağlar (kapak çözümü); binding tüketici kurar.
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

    // Reviews yayınlar, Storefront satırına RatingAverage/RatingCount yazar.
    // Storefront TEK kuyruk deseni: mevcut storefront.events kuyruğuna bağlanır (Sequential).
    public static class ReviewSummaryChanged
    {
        public const string Exchange = "reviews.summary-changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;
        }
    }

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

    // Discount yayınlar (kitap başına indirim penceresi / temizlik), Storefront tüketir.
    // Storefront TEK kuyruk deseni: mevcut storefront.events kuyruğuna bağlanır (Sequential).
    public static class ProductDiscountChanged
    {
        public const string Exchange = "discount.product-discount-changed";

        public static class Queues
        {
            public const string Storefront = StorefrontEvents.Queue;
        }
    }

    // 092 checkout koreografisi (orchestration söküldü). Order stok commit talep eder (fanout; tek tüketici
    // Stock, tutarlılık için fanout). Stock sonucu Order'a fanout'la döner. Binding'i TÜKETİCİ kurar (007).
    public static class StockCommitRequested
    {
        public const string Exchange = "stock.commit-requested";

        public static class Queues
        {
            public const string Stock = "stock.commit-requested";
        }
    }

    public static class StockCommitted
    {
        public const string Exchange = "stock.committed";

        public static class Queues
        {
            public const string Order = "order.stock-committed";
        }
    }

    public static class StockCommitFailed
    {
        public const string Exchange = "stock.commit-failed";

        public static class Queues
        {
            public const string Order = "order.stock-commit-failed";
        }
    }
}