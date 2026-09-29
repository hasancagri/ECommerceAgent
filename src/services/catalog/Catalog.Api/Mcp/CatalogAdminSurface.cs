namespace Catalog.Api.Mcp;

// 070/074/083/085: catalog admin yönetim yüzeyi politikası (Program.cs orkestrasyon dışı tutulur).
// 085: ayrı /mcp-admin ucu öldü — bu tool'lar TEK /mcp'de, token scope'una göre budanmış görünür.
public static class CatalogAdminSurface
{
    // Admin tool'ları (anonim çağrıda budanır — ToolScopeMap). ConfigureSessionOptions okur.
    // TUZAK: yeni admin tool eklerken buraya + ToolScopeMap'e EKLE — ad-prefix DEĞİL açık liste (074 dersi).
    public static readonly string[] TOOL_NAMES =
    [
        Shared.CatalogAdminTools.ListProducts, Shared.CatalogAdminTools.GetProduct,
        Shared.CatalogAdminTools.UpdateProduct, Shared.CatalogAdminTools.SetPublished,
        Shared.CatalogAdminTools.GetPriceHistory,
        // 074: parite tool'ları (REST admin söküldü).
        Shared.CatalogAdminTools.CreateProduct, Shared.CatalogAdminTools.SetProductDimensions,
        Shared.CatalogAdminTools.SetProductSeo, Shared.CatalogAdminTools.AssignProductTag,
        Shared.CatalogAdminTools.RemoveProductTag, Shared.CatalogAdminTools.CreateCategory,
        Shared.CatalogAdminTools.UpdateCategory, Shared.CatalogAdminTools.CreateAuthor,
        Shared.CatalogAdminTools.CreateProductTag, Shared.CatalogAdminTools.RenameProductTag,
        Shared.CatalogAdminTools.ListProductTags, Shared.CatalogAdminTools.CreateSpecificationAttribute,
        Shared.CatalogAdminTools.AddSpecificationAttributeOption, Shared.CatalogAdminTools.ListSpecificationAttributes,
        Shared.CatalogAdminTools.RepublishProducts,
        // 083: Excel katalog import.
        Shared.CatalogAdminTools.ImportCatalog, Shared.CatalogAdminTools.PublishImported,
        Shared.CatalogAdminTools.GetImportStatus,
    ];

    // Admin scope demeti (okuma + yazma) — RFC 9728 PRM keşfi/discovery-scope için.
    public static readonly string[] SCOPES =
    [
        AuthorizationScopes.AdminCatalogRead,
        AuthorizationScopes.AdminCatalogWrite,
    ];

    // 085 R1: tool→scope eşlemesi — kaynak [RequiredScope] attribute'larıyla BİREBİR (tek gerçek-kaynak
    // burası; fasada kopyalanmaz). ConfigureSessionOptions oturum tool setini bununla budar.
    public static readonly IReadOnlyDictionary<string, string> TOOL_SCOPE_MAP = new Dictionary<string, string>
    {
        [Shared.CatalogAdminTools.ListProducts] = AuthorizationScopes.AdminCatalogRead,
        [Shared.CatalogAdminTools.GetProduct] = AuthorizationScopes.AdminCatalogRead,
        [Shared.CatalogAdminTools.GetPriceHistory] = AuthorizationScopes.AdminCatalogRead,
        [Shared.CatalogAdminTools.ListProductTags] = AuthorizationScopes.AdminCatalogRead,
        [Shared.CatalogAdminTools.ListSpecificationAttributes] = AuthorizationScopes.AdminCatalogRead,
        [Shared.CatalogAdminTools.GetImportStatus] = AuthorizationScopes.AdminCatalogRead,

        [Shared.CatalogAdminTools.UpdateProduct] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.SetPublished] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.CreateProduct] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.SetProductDimensions] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.SetProductSeo] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.AssignProductTag] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.RemoveProductTag] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.CreateCategory] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.UpdateCategory] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.CreateAuthor] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.CreateProductTag] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.RenameProductTag] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.CreateSpecificationAttribute] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.AddSpecificationAttributeOption] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.RepublishProducts] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.ImportCatalog] = AuthorizationScopes.AdminCatalogWrite,
        [Shared.CatalogAdminTools.PublishImported] = AuthorizationScopes.AdminCatalogWrite,
    };
}