namespace Catalog.Api.Constants;

// Catalog context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
// 095: sökülen alan/davranışların guard sabitleri (SKU/Tag/Dimensions/çoklu-kategori/Category
// mutator'ları) modelle birlikte kaldırıldı.
public static class CatalogResourceConstants
{
    public static readonly string RECORD_NOT_FOUND = "COMMON_MESSAGE_RECORD_NOT_FOUND";
    public static readonly string VALUE_EMPTY = "COMMON_MESSAGE_VALUE_EMPTY";

    public static readonly string PRODUCT_NAME_REQUIRED = "CATALOG_PRODUCT_NAME_REQUIRED";
    // 095: kimlik Gtin (ISBN) — Sku söküldü, var oluş guard'ı Gtin'e taşındı.
    public static readonly string PRODUCT_GTIN_REQUIRED = "CATALOG_PRODUCT_GTIN_REQUIRED";
    // 091: fiyat>0 var oluş invariant'ı (Create; fiyat doğumda sabit — 097).
    public static readonly string PRODUCT_PRICE_REQUIRED = "CATALOG_PRODUCT_PRICE_REQUIRED";
    // 095: tek leaf kategori zorunlu (SetCategory boş Id guard'ı; seed "Genel" fallback'iyle doldurur).
    public static readonly string PRODUCT_CATEGORY_REQUIRED = "CATALOG_PRODUCT_CATEGORY_REQUIRED";

    // get-or-create / REST teklik guard'ları (Author/Publisher yüzeyi için saklanır).
    public static readonly string AUTHOR_ALREADY_EXISTS = "CATALOG_AUTHOR_ALREADY_EXISTS";
    public static readonly string PUBLISHER_ALREADY_EXISTS = "CATALOG_PUBLISHER_ALREADY_EXISTS";
    public static readonly string PRODUCT_ISBN_EXISTS = "CATALOG_PRODUCT_ISBN_EXISTS";
    public static readonly string PRODUCT_AUTHOR_REQUIRED = "CATALOG_PRODUCT_AUTHOR_REQUIRED";
    public static readonly string PRODUCT_PUBLISHER_REQUIRED = "CATALOG_PRODUCT_PUBLISHER_REQUIRED";

    // 095: kapalı kategori ağacı seed guard'ı — alt kategori adı ağaç-global unique varsayımı
    // ihlalinde seed deterministik hata verir (sessiz çift kayıt üretmez).
    public static readonly string CATEGORY_TREE_CONFLICT = "CATALOG_CATEGORY_TREE_CONFLICT";
}
