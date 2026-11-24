using System.ComponentModel;

namespace ScoutAgent.Api.Search;

// 022: Kâşif iki-faz aramanın TİPLİ niyet kontratı (React↔backend). NESTED — her grup nullable:
// grup null = "o konu hiç konuşulmadı" (spike: nested güvenilir parse, flat ~%50 parse-fail). LLM
// yalnız kullanıcının KENDİ kelimelerini doğru slota koyar (çeviri/enum/sayı/id YOK — sunucu yorumlar).
// `[Description]` öznitelikleri structured-output şemasına geçer (model'e alan rehberi).
public sealed record BookSearchFilters(
    [property: Description("Metin slotu: serbest sorgu + yazar. Konuşulmadıysa null.")]
    TextSlot? Text = null,
    [property: Description("Yayınevi slotu: dahil/hariç. Konuşulmadıysa null.")]
    PublisherSlot? Publisher = null,
    [property: Description("Fiyat ifadesi METNİ (aynen): 'ucuz', '100-300 TL', '300 altı'. Yoksa null.")]
    string? PriceHint = null,
    [property: Description("Sıralama ifadesi METNİ: 'ucuzdan pahalıya', 'en çok satan', 'yeni'. Yoksa null.")]
    string? SortHint = null,
    [property: Description("Cilt ifadesi METNİ: 'ciltli' / 'karton'. Yoksa null.")]
    string? BindingHint = null,
    [property: Description("Sayfa ifadesi METNİ: 'kalın', 'kısa', '300 sayfadan fazla'. Yoksa null.")]
    string? PageHint = null,
    [property: Description("Tür/ruh-hali/konu/yaş kelimeleri. Genelde boş bırak — bunları Text.Query'ye koy.")]
    TaxonomySlot? Taxonomy = null,
    [property: Description("Bayraklar: indirimli/stokta/satışta. Konuşulmadıysa null.")]
    FlagsSlot? Flags = null,
    [property: Description("BENZERLİK: 'şuna benzer/buna benzeyen/gibi başka/X tarzında' ise ÇAPA kitabın adı " +
        "(ek'siz, yalın: 'Anna Karenina'). Dolduysa diğer alanları boş bırak. Yoksa null.")]
    string? SimilarToTitle = null,
    [property: Description("Sayfa numarası (1'den).")]
    int Page = 1,
    [property: Description("Sayfa boyutu (en fazla 20).")]
    int PageSize = 10);

public sealed record TextSlot(
    [property: Description("Konu/başlık + tür/ruh-hali kelimeleri (yazar/yayınevi HARİÇ). 'X hakkında' ise X buraya.")]
    string? Query = null,
    [property: Description("YAZARLIK: 'X'in kitapları/eserleri', 'yazar X' ise yazar adı. 'X hakkında' ise null.")]
    string? Author = null);

public sealed record PublisherSlot(
    [property: Description("Yalnız bu yayınevi")] string? Include = null,
    [property: Description("Hariç tutulan yayınevi")] string? Exclude = null);

public sealed record TaxonomySlot(
    string[]? Genres = null, string[]? Moods = null, string[]? Themes = null, string[]? Audience = null);

public sealed record FlagsSlot(
    bool OnlyDiscounted = false, bool OnlyInStock = false, bool OnlySalable = false);
