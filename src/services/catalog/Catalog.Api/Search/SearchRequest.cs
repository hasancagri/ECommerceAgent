namespace Catalog.Api.Search;

// 018: birleşik arama niyeti — tüm müşteri aramaları (basit + kompleks) bu tek DTO'ya dolar.
// Alanların hepsi opsiyonel; basit/kompleks ayrımı yalnız dolu alanlarla ifade edilir. Davranış yok
// (saf veri); mod/sıra kararı SearchModeResolver'da, filtre→SQL SearchPredicateBuilder'da.
public sealed record SearchRequest(
    string? Text = null,
    string? Publisher = null,
    string? ExcludePublisher = null,
    // 021-author: YAZARLIK filtresi ("X'in kitapları" → yalnız X'in eserleri). Ad handler'da Author id
    // SET'ine çözülür → AuthorIds jsonb `?|`. "X hakkında/üzerine" (konu) buraya GİRMEZ, Text'te kalır.
    string? Author = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    int? PageMin = null,
    int? PageMax = null,
    BindingType? Binding = null,
    bool OnlySalable = false,
    bool OnlyDiscounted = false,
    // 021: taksonomi facet'leri — çoklu değer (alan içi OR, alanlar arası AND); exact-match
    // (metin-içi tesadüf değil). Sözlük-dışı değer handler/validation'da reddedilir.
    IReadOnlyList<string>? Genres = null,
    IReadOnlyList<string>? Moods = null,
    IReadOnlyList<string>? AudienceLevels = null,
    IReadOnlyList<string>? Themes = null,
    // 021: StockCount>0 süzgeci (IsSalable'dan ayrı — fiyat/yazar da istemez).
    bool OnlyInStock = false,
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
