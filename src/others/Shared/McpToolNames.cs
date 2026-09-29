namespace Shared;

// MCP tool adlarının TEK kaynağı. Ad, servisler-arası SÖZLEŞMEDİR (integration-event kontratı
// emsali): sunucu attribute'ı ([McpServerTool(Name = ...)]) ile istemci (ChatAgent allowlist +
// prompt kalıpları) aynı sabiti okur — rename artık derleme hatasıyla yakalanır, sessiz kopmaz.
// DİKKAT: dış MCP istemcileri (Claude Desktop vb., 061) bu adları dışarıdan görür — sabiti
// değiştirmek DIŞ KIRILMADIR, bilinçli yapılır. Sınıf-başına-BC gruplama; DropShop onboarding
// adları BURADA DEĞİL (dış solution kontratı, ChatAgent/ConstValues.cs'te).

public static class CatalogTools
{
    public const string GetProduct = "get_product";
    public const string SearchProducts = "search_products";
    public const string GetPriceHistory = "get_price_history";
    // keşif envanteri Catalog'da (envanter otoritesi; Storefront kitap-arama yüzeyi).
    public const string ListCategories = "list_categories";
    public const string ListAuthors = "list_authors";
    public const string ListPublishers = "list_publishers";
}

public static class BasketTools
{
    public const string AddToCart = "add_to_cart";
    public const string GetBasket = "get_basket";
    public const string RemoveBasketItem = "remove_basket_item";
    public const string UpdateBasketQuantity = "update_basket_quantity";
}

public static class OrderTools
{
    public const string GetOrders = "get_orders";
    // chat'ten uctan uca siparis tamamlama (sunucu orkestrasyonu; cardId?, tek çekim).
    public const string PlaceOrder = "place_order";
    // TAKSİT KALDIRILDI: quote_installments sabiti söküldü (tek çekim; 070 A2A borcu ödendi).
    // hosted-CF ödeme başlat — hosted ödeme linki döner (sepet gRPC, order Pending, Payment S2S).
    public const string StartPayment = "start_payment";
}

public static class PaymentTools
{
    public const string GetMyPayments = "get_my_payments";
}

public static class StockTools
{
    public const string GetStock = "get_stock";
}

public static class StorefrontTools
{
    // tek serbest-sorgu kapısı — search_storefront_products + find_similar_books TAM İKAME silindi.
    public const string QueryStorefront = "query_storefront";
}

public static class CustomerTools
{
    // GÜVENLİK: get_default_card_bin + get_payment_context sabitleri KALDIRILDI (tool'lar söküldü —
    // vault token + buyer PII sohbet bağlamına sızıyordu). Ödeme bağlamı yalnız S2S internal REST.
    public const string ListCards = "list_cards";
    // adres defteri (dış agent yüzeyi; ChatAgent allowlist'inde değil).
    public const string ListAddresses = "list_addresses";
    public const string AddAddress = "add_address";
    public const string UpdateAddress = "update_address";
    public const string RemoveAddress = "remove_address";
    public const string SetDefaultAddress = "set_default_address";
}

public static class ReviewsTools
{
    public const string GetReviews = "get_reviews";
    public const string CheckReviewEligibility = "check_review_eligibility";
    public const string SubmitReview = "submit_review";
}

public static class LibraryTools
{
    public const string GetPriceAlarm = "get_price_alarm";
    public const string CreatePriceAlarm = "create_price_alarm";
    public const string RemovePriceAlarm = "remove_price_alarm";
}

// 070/085: admin yönetim tool'ları — her BC'nin TEK /mcp ucunda, token scope'una göre budanmış yayınlanır
// (anonim /mcp keşif setine GİRMEZ). Yazma tool'ları tek-kayıt işler.

public static class CatalogAdminTools
{
    public const string ListProducts = "admin_list_products";
    public const string GetProduct = "admin_get_product";
    public const string UpdateProduct = "admin_update_product";
    public const string SetPublished = "admin_set_published";
    public const string GetPriceHistory = "admin_get_price_history";
    // REST admin yüzeyi söküldü; parite tool'ları (MCP-only iş yüzeyi).
    public const string CreateProduct = "admin_create_product";
    public const string SetProductDimensions = "admin_set_product_dimensions";
    public const string SetProductSeo = "admin_set_product_seo";
    public const string AssignProductTag = "admin_assign_product_tag";
    public const string RemoveProductTag = "admin_remove_product_tag";
    public const string CreateCategory = "admin_create_category";
    public const string UpdateCategory = "admin_update_category";
    public const string CreateAuthor = "admin_create_author";
    public const string CreateProductTag = "admin_create_product_tag";
    public const string RenameProductTag = "admin_rename_product_tag";
    public const string ListProductTags = "admin_list_product_tags";
    public const string CreateSpecificationAttribute = "admin_create_specification_attribute";
    public const string AddSpecificationAttributeOption = "admin_add_specification_attribute_option";
    public const string ListSpecificationAttributes = "admin_list_specification_attributes";
    // backfill: yayındaki tüm ürünler için ProductChangedEvent'i yeniden yayınla (yeni downstream doldur).
    public const string RepublishProducts = "admin_republish_products";
    // Excel katalog import — token-linkli yükleme başlat, import taslaklarını toplu yayınla, durum raporu.
    public const string ImportCatalog = "admin_import_catalog";
    public const string PublishImported = "admin_publish_imported";
    public const string GetImportStatus = "admin_get_import_status";
}

// 079/085: Discount.Api /mcp kampanya yönetimi (scope discount.admin.write). Yeni tool eklerken
// Program.cs discountAdminToolNames allowlist'ine de EKLE (070/074 deseni). Müşteri /mcp yüzeyi YOK —
// indirim müşteriye vitrinde query_storefront'tan görünür.
public static class DiscountAdminTools
{
    public const string CreateCampaign = "admin_create_campaign";
    public const string CancelCampaign = "admin_cancel_campaign";
    public const string ListCampaigns = "admin_list_campaigns";
}

public static class StockAdminTools
{
    public const string SetStock = "admin_set_stock";
    public const string AdjustStock = "admin_adjust_stock";
    // admin stok genel görünüm (parite; GetAllStock REST'i söküldü).
    public const string ListAllStock = "admin_list_all_stock";
}

public static class CustomerAdminTools
{
    public const string GetMerchantStatus = "admin_get_merchant_status";
    // US4: SetMerchantCredentials + SubmitOnboarding SÖKÜLDÜ — PII/key sohbete girmez;
    // giriş = hosted form (StartOnboarding), teslim = mail + store ekranı (RequestCredentialEntryLink).
    public const string OnboardingStatus = "admin_onboarding_status";
    // PII'siz hosted onboarding — form oturumu açar, yalnız link döner.
    public const string StartOnboarding = "admin_start_onboarding";
    // credential-giriş ekranına süreli + tek kullanımlık link üretir (key sohbete girmez).
    public const string RequestCredentialEntryLink = "admin_request_credential_entry_link";
    // PG'de merchant key yenileme tetikler; yanıt yalnız tek gösterimlik reveal URL (key sohbete girmez).
    public const string ReissueMerchantKey = "admin_reissue_merchant_key";
}

// korumalı MCP'lerdeki ortak oturum-kapatma tool'u (basket/order/payment/customer).
public static class AuthTools
{
    public const string Logout = "logout";
}

public static class MailTools
{
    // Mail.Mcp'nin tek tool'u; tüketici NotificationAgent (ChatAgent'a KAYITLI DEĞİL).
    public const string SendMail = "send_mail";
}