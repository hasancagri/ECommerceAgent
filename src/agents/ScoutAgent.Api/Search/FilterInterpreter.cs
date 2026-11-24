using System.Globalization;
using System.Text.RegularExpressions;

namespace ScoutAgent.Api.Search;

// 022: tipli niyet-slot → search_books argüman dict (saf, İlke VI test-first). Pass-through'lar aynen
// (sort/binding metni Catalog'da yorumlanır); PriceHint/PageHint metin→sayı burada parse edilir. Grup
// null → anahtar yok (uydurma yasağı). Ambiguous/sayısız hint → filtre düşer (tahmin etme).
public static class FilterInterpreter
{
    public static Dictionary<string, object?> ToSearchArgs(BookSearchFilters f)
    {
        var args = new Dictionary<string, object?>
        {
            ["page"] = f.Page < 1 ? 1 : f.Page,
            ["pageSize"] = f.PageSize is < 1 or > 20 ? 10 : f.PageSize,
        };

        // Query + taksonomi kelimeleri (varsa) tek query metnine katlanır (Catalog ExtractFromText facet'e çevirir).
        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Text?.Query)) queryParts.Add(f.Text!.Query!.Trim());
        if (f.Taxonomy is { } tx)
            queryParts.AddRange(new[] { tx.Genres, tx.Moods, tx.Themes, tx.Audience }
                .Where(x => x is { Length: > 0 }).SelectMany(x => x!)
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (queryParts.Count > 0) args["query"] = string.Join(" ", queryParts);

        if (!string.IsNullOrWhiteSpace(f.Text?.Author)) args["author"] = f.Text!.Author!.Trim();

        if (!string.IsNullOrWhiteSpace(f.Publisher?.Include)) args["publisher"] = f.Publisher!.Include!.Trim();
        if (!string.IsNullOrWhiteSpace(f.Publisher?.Exclude)) args["excludePublisher"] = f.Publisher!.Exclude!.Trim();

        if (!string.IsNullOrWhiteSpace(f.SortHint)) args["sort"] = f.SortHint!.Trim();
        if (!string.IsNullOrWhiteSpace(f.BindingHint)) args["binding"] = f.BindingHint!.Trim();

        // Fiyat: metin→(min,max) decimal.
        var (priceMin, priceMax) = ParseRange(f.PriceHint);
        if (priceMin is { } pmin) args["minPrice"] = (decimal)pmin;
        if (priceMax is { } pmax) args["maxPrice"] = (decimal)pmax;

        // Sayfa sayısı: metin→(min,max) int.
        var (pageMin, pageMax) = ParseRange(f.PageHint);
        if (pageMin is { } gmin) args["minPageCount"] = gmin;
        if (pageMax is { } gmax) args["maxPageCount"] = gmax;

        if (f.Flags is { } flags)
        {
            if (flags.OnlyDiscounted) args["onlyDiscounted"] = true;
            if (flags.OnlyInStock) args["onlyInStock"] = true;
            if (flags.OnlySalable) args["onlySalable"] = true;
        }

        return args;
    }

    // Serbest metinden sayı aralığı çıkarır. 2+ sayı → (küçük, büyük). 1 sayı + yön kelimesi → min ya da
    // max. 1 sayı + yön yok, ya da 0 sayı → (null, null) (ambiguous/sayısız → filtre düşer; uydurma yok).
    private static (int? Min, int? Max) ParseRange(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint)) return (null, null);
        var low = hint.ToLowerInvariant();
        var nums = Regex.Matches(low, @"\d+")
            .Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        if (nums.Count == 0) return (null, null);
        if (nums.Count >= 2) return (nums.Min(), nums.Max());

        var n = nums[0];
        // MAX yönü: "en fazla/en çok/maksimum/maks/alt(ı)/aşağı/kadar/ucuz" ya da "en az" OLMAYAN "az".
        bool max = low.Contains("en fazla") || low.Contains("en çok") || low.Contains("maksimum")
            || low.Contains("maks") || low.Contains("alt") || low.Contains("aşağı")
            || low.Contains("kadar") || low.Contains("ucuz")
            || (Regex.IsMatch(low, @"\baz\b") && !low.Contains("en az")); // "az" kelimesi — "fazla" içindeki az DEĞİL
        // MIN yönü: "en az/minimum/üst/üzeri/yukarı/yüksek/pahalı" ya da "en fazla" OLMAYAN "fazla".
        bool min = low.Contains("en az") || low.Contains("minimum") || low.Contains("üst")
            || low.Contains("üzeri") || low.Contains("yukarı") || low.Contains("yüksek")
            || low.Contains("pahalı")
            || (low.Contains("fazla") && !low.Contains("en fazla"));

        if (min && !max) return (n, null);
        if (max && !min) return (null, n);
        return (null, null); // ambiguous (yön yok ya da çelişik) → düş
    }
}
