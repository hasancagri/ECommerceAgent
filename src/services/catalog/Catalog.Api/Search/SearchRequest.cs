namespace Catalog.Api.Search;

// 018: birleşik arama niyeti — tüm müşteri aramaları (basit + kompleks) bu tek DTO'ya dolar.
// Alanların hepsi opsiyonel; basit/kompleks ayrımı yalnız dolu alanlarla ifade edilir. Davranış yok
// (saf veri); mod/sıra kararı SearchModeResolver'da, filtre→SQL SearchPredicateBuilder'da.
public sealed record SearchRequest(
    string? Text = null,
    string? Publisher = null,
    string? ExcludePublisher = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    int? PageMin = null,
    int? PageMax = null,
    BindingType? Binding = null,
    bool OnlySalable = false,
    bool OnlyDiscounted = false,
    SortOption? Sort = null,
    int Page = 1,
    int PageSize = SearchParameters.DefaultLimit);

// Açık sıralama seçenekleri (verilirse alaka sırasını ezer — bkz. SearchModeResolver / FR-004).
public enum SortOption
{
    PriceAsc,
    PriceDesc,
    Newest,
    Popular,
}
