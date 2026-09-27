using Common.Utils.Constants;

namespace Mcp.Gateway;

// 073/079/085: fasad TEK uç scope demeti (challenge + PRM'de ilan edilir; gerçek yetki downstream'de +
// kullanıcı rolünde + AgentPlatform ScopeResolver'da — bkz. R3). Program.cs orkestrasyon dışı tutulur.
public static class FacadeScopes
{
    // 085 analiz C1: union'ın gerçek-kaynağı SEED İSTEMCİ izin setleridir — müşteri + admin demeti
    // birleşiminden az/çok OLMAZ. 079 tuzağı: PRM'de ilan edilmeyen scope istenmez → istemci o scope'u
    // hiç talep etmez → token audience taşımaz → downstream 401. Eksik scope PRM'e eklenmeden ilan edilemez.
    public static readonly string[] All =
    [
        // external-customer-agent demeti.
        AuthorizationScopes.BasketRead, AuthorizationScopes.BasketWrite,
        AuthorizationScopes.OrderRead, AuthorizationScopes.OrderWrite,
        AuthorizationScopes.CustomerRead, AuthorizationScopes.CustomerWrite,
        AuthorizationScopes.PaymentRead, AuthorizationScopes.StorefrontRead,
        AuthorizationScopes.ReviewsWrite, AuthorizationScopes.LibraryRead, AuthorizationScopes.LibraryWrite,

        // external-admin-agent demeti.
        AuthorizationScopes.AdminCatalogRead, AuthorizationScopes.AdminCatalogWrite,
        AuthorizationScopes.StockWrite, AuthorizationScopes.MerchantCredentialsWrite,
        AuthorizationScopes.AdminDiscountWrite,

        // 047: PaymentGateway demeti — PG Merchant/Commission MCP tool'ları store fasadına downstream.
        // 079 tuzağı: PRM'de ilan edilmeyen scope talep edilmez → token audience taşımaz → PG /mcp 401.
        AuthorizationScopes.MerchantRead, AuthorizationScopes.MerchantWrite, AuthorizationScopes.MerchantAdmin,
        AuthorizationScopes.CommissionRead, AuthorizationScopes.CommissionWrite,
    ];
}