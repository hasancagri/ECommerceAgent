namespace Common.Utils.Constants;

public static class AuthorizationScopes
{
    // catalog.api — müşteri okuma anonim (scope'suz). Admin yüzeyi (085: TEK /mcp'de, scope-budamalı) ikiye ayrılır:
    // okuma tool'ları AdminCatalogRead, yazma tool'ları AdminCatalogWrite ister.
    public const string AdminCatalogRead = "catalog.read";
    public const string AdminCatalogWrite = "catalog.write";

    // basket.api
    public const string BasketRead = "basket.read";
    public const string BasketWrite = "basket.write";

    // order.api
    public const string OrderRead = "order.read";
    public const string OrderWrite = "order.write";

    // (092: checkout.write scope'u söküldü — Checkout.Orchestrator + REST girişi gitti, choreography'de
    //  HTTP checkout yüzeyi yok. Örnek doğduğunda AgentPlatform AppRegistry config'inden yönetilir.)

    // payment.api
    public const string PaymentRead = "payment.read";
    public const string PaymentWrite = "payment.write";

    // stock.api
    public const string StockWrite = "stock.write";

    // storefront.api
    public const string StorefrontRead = "storefront.read";

    // discount.api (079): kampanya indirimi. Checkout gRPC S2S okuma (Order makine token) +
    // admin /mcp kampanya yönetimi (external-admin-agent). Müşteri yüzeyi indirimi vitrinde görür.
    public const string DiscountRead = "discount.read";
    public const string AdminDiscountWrite = "discount.admin.write";

    // customer.api (022): kayitli kart (Wallet) + adres defteri (AddressBook)
    public const string CustomerRead = "customer.read";
    public const string CustomerWrite = "customer.write";

    // reviews.api (044): yorum yazma + uygunluk sorgusu; Order satin-alma-kaniti gRPC ucu da
    // bu scope'u ister (R4: ayri scope acilmaz, sub==user_id guard'i sunucuda).
    public const string ReviewsWrite = "reviews.write";

    // library.api (060): fiyat alarmı durumu okuma + kurma/kaldırma.
    public const string LibraryRead = "library.read";
    public const string LibraryWrite = "library.write";

    // 088: ölü-mesaj (dead-letter) operatör yüzeyi — her BC'nin /mcp'sinde list/get/replay/discard
    // tool'larını korur. Çapraz-kesen ops yetkisi (tek BC'ye ait değil); rol→scope map AgentPlatform'da.
    public const string OpsDeadletter = "ops.deadletter";

    // identity: API anahtari (UserKey) issue/revoke yuzeyi — Identity.Server kendi Bearer
    // policy'siyle dogrular (audience'siz; m2m apikeys.admin istemcisi tasir).
    public const string ApiKeysManage = "apikeys.manage";

    // identity (030 RBAC): IdP rol/scope/kullanici yonetim yuzeyi. Downstream servis zorlamaz;
    // Identity.Server ic yuzeyini belirler.
    public const string IdentityRolesManage = "identity.roles.manage";

    // (093): PG bloğu (merchant.*/commission.*) SÖKÜLDÜ — PG pivotu, Merchant+Commission BC'leri öldü.
}