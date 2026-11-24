namespace Catalog.Api.Domains.Products;

/// <summary>
/// 021: Kapalı taksonomi sözlüğü — TEK yetkili kaynak. Değerler enrichment pipeline'ının
/// SYSTEM_PROMPT listeleriyle birebir (tur 20 · ton 20 · seviye 9 · konu 49); serbest string
/// üretimi imkansız (KnownScopes emsali). "belirsiz" tur/seviye için geçerli değerdir; ton/konu
/// boş liste taşır. Canonical* metotları v11 verisindeki az sayıdaki sapmayı (Aşama-1 batch
/// kelime haznesi, ~%0.5-0.7) bariz takma-adlarla kanonikleştirir; eşleşmeyen değer null döner
/// ve çağıran düşürür (FR-005 — import durmaz).
/// </summary>
public static class BookTaxonomy
{
    public const string Unknown = "belirsiz";

    public static readonly FrozenSet<string> Genres = new[]
    {
        "roman", "novella/uzun öykü", "öykü", "şiir", "deneme", "düşünce/felsefe yazısı",
        "biyografi", "otobiyografi/anı", "günlük/mektup", "tiyatro/oyun", "inceleme/araştırma",
        "tarih anlatısı", "gezi yazısı", "röportaj/söyleşi", "başvuru/kılavuz/ders",
        "çocuk resimli kitap", "çizgi roman/grafik roman", "antoloji/derleme", "diğer", Unknown,
    }.ToFrozenSet();

    public static readonly FrozenSet<string> Moods = new[]
    {
        "macera dolu", "gerilimli/sürükleyici", "hüzünlü/melankolik", "karanlık",
        "tedirgin edici", "eğlenceli/komik", "sıcak/samimi", "romantik", "ironik/alaycı",
        "lirik/şiirsel", "epik/destansı", "felsefi/varoluşsal", "ağır/düşündürücü", "nostaljik",
        "gizemli", "deneysel/avangard", "didaktik/öğretici", "ciddi/akademik", "mizahi/absürt",
        "pastoral/sakin",
    }.ToFrozenSet();

    public static readonly FrozenSet<string> AudienceLevels = new[]
    {
        "çocuk (0-3)", "çocuk (4-6)", "çocuk (7-10)", "çocuk (10-12)",
        "genç yetişkin", "yetişkin-giriş", "yetişkin-orta", "yetişkin-ileri", Unknown,
    }.ToFrozenSet();

    public static readonly FrozenSet<string> Themes = new[]
    {
        "aşk/romantizm", "aile", "arkadaşlık", "büyüme/olgunlaşma", "kimlik",
        "yalnızlık/yabancılaşma", "ölüm/kayıp", "göç/sürgün", "savaş/barış", "şiddet/savaş",
        "suç/adalet", "siyaset", "güç/iktidar", "özgürlük", "toplumsal yaşam",
        "sınıf/tabakalaşma", "din/inanç", "ahlak/etik", "varoluş/anlam arayışı", "doğa/çevre",
        "bilim/keşif", "teknoloji/yapay zeka", "sanat/yaratıcılık", "dil/edebiyat",
        "tarih/bellek", "eğitim/öğrenme", "spor", "yeme-içme kültürü", "arzu/tutku",
        "korku/doğaüstü", "distopya/ütopya", "mizah/hiciv", "kader/tesadüf", "insan ilişkileri",
        "cesaret/mücadele", "iş/kariyer", "para/ekonomi", "gençlik/isyan", "cinsellik",
        "ruhsal gelişim", "deliliğin sınırları", "zaman/mekan", "emek/işçilik", "hayvan/doğa",
        "gündelik yaşam", "oyun/eğlence", "macera", "feminist bakış", "kolonyalizm/sömürgecilik",
    }.ToFrozenSet();

    // v11 verisindeki bariz sapmaların kanonik karşılığı (Aşama-1 batch'i; karşılıksız sapma
    // haritaya GİRMEZ → null → düşer). Yeni değer EKLEME — sözlük kapalıdır.
    // Geniş eşle, dar sakla: v11 pipeline çıktısındaki TÜM sözlük-dışı ton atomları (10 distinct).
    // Karşılığı olmayan (gerçekçi/soğukkanlı, hafif/akıcı, umutlu/iyimser, eleştirel/polemik) haritada
    // YOK → null → düşer (etiket atlanır, kitap kaydedilir).
    // İmport TEKİL (SetEnrichment için): v11 pipeline sapmaları → tek kanonik. Sorgu sinonimleri AYRI
    // (çoğul) haritada — import verisinin değeri TEKtir, sorgu kullanıcısının kelimesi çoğula fan-out olur.
    private static readonly FrozenDictionary<string, string> MoodAliases = new Dictionary<string, string>
    {
        ["mizahi"] = "mizahi/absürt",
        ["absürt"] = "mizahi/absürt",
        ["ironik/hicivli"] = "ironik/alaycı",
        ["sakin/tefekkür"] = "pastoral/sakin",
        ["samimi/sohbet tonu"] = "sıcak/samimi",
        ["duygusal/dokunaklı"] = "hüzünlü/melankolik",
    }.ToFrozenDictionary();

    // v11 sözlük-dışı konu atomları (35 distinct). Net + gevşek eşleşenler kanonikleşir; yer adı
    // (kent/İstanbul, coğrafya/şehir, taşra/köy), tür artığı (fantastik, bilimkurgu, polisiye/gizem,
    // gerilim), biçim-konusu (bir kişinin hayatı) ve karşılıksız (mitoloji/masal, büyülü gerçekçilik)
    // haritaya girmez → düşer.
    private static readonly FrozenDictionary<string, string> ThemeAliases = new Dictionary<string, string>
    {
        ["tarih"] = "tarih/bellek",
        ["bellek/geçmiş"] = "tarih/bellek",
        ["tarihi-kurgu"] = "tarih/bellek",
        ["savaş"] = "savaş/barış",
        ["suç"] = "suç/adalet",
        ["adalet/vicdan"] = "suç/adalet",
        ["hukuk"] = "suç/adalet",
        ["dostluk"] = "arkadaşlık",
        ["eğitim"] = "eğitim/öğrenme",
        ["hayvanlar"] = "hayvan/doğa",
        ["aşk/ilişkiler"] = "aşk/romantizm",
        ["kimlik/arayış"] = "kimlik",
        ["ekonomi"] = "para/ekonomi",
        ["yaşlılık/ölüm"] = "ölüm/kayıp",
        ["sınıf/yoksulluk"] = "sınıf/tabakalaşma",
        ["bilim/teknoloji"] = "bilim/keşif",
        ["büyüme/ergenlik"] = "büyüme/olgunlaşma",
        ["kadın/toplumsal cinsiyet"] = "feminist bakış",
        ["psikoloji"] = "ruhsal gelişim",
        ["kişisel gelişim"] = "ruhsal gelişim",
        ["delilik/akıl"] = "deliliğin sınırları",
        ["sosyoloji"] = "toplumsal yaşam",
        ["sanat/edebiyat tarihi"] = "sanat/yaratıcılık",
        ["din bilimleri"] = "din/inanç",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> AudienceLevelAliases = new Dictionary<string, string>
    {
        ["yetişkin-ileri/akademik"] = "yetişkin-ileri",
        ["genç (13–17)"] = "genç yetişkin",
        ["çocuk (7–12)"] = "çocuk (7-10)",
        ["okul öncesi (0–6)"] = "çocuk (4-6)",
    }.ToFrozenDictionary();

    // ── 021 SORGU sinonimleri (ÇOĞUL) — kullanıcı kelimesi → kanonik değer DİZİSİ. Net kelime tek-eleman;
    // ZORLAMA/belirsiz kelime (kara, ağır, duygusal, aksiyon) çok-eleman (OR fan-out, tekil hapsetmez).
    // Import TEKİL maplarından AYRI (import değeri tektir). ExtractFromText + agent facet-canonicalize kullanır.
    private static readonly FrozenDictionary<string, string[]> MoodQuery = new Dictionary<string, string[]>
    {
        ["mizah"] = ["mizahi/absürt"],
        ["mizahi"] = ["mizahi/absürt"],
        ["absürt"] = ["mizahi/absürt"],
        ["komik"] = ["eğlenceli/komik"],
        ["komedi"] = ["eğlenceli/komik"],
        ["güldürü"] = ["eğlenceli/komik"],
        ["eğlenceli"] = ["eğlenceli/komik"],
        ["neşeli"] = ["eğlenceli/komik"],
        ["hiciv"] = ["ironik/alaycı"],
        ["ironik"] = ["ironik/alaycı"],
        ["alaycı"] = ["ironik/alaycı"],
        ["kasvetli"] = ["karanlık"],
        ["hüzünlü"] = ["hüzünlü/melankolik"],
        ["melankolik"] = ["hüzünlü/melankolik"],
        ["gerilim"] = ["gerilimli/sürükleyici"],
        ["sürükleyici"] = ["gerilimli/sürükleyici"],
        ["şiirsel"] = ["lirik/şiirsel"],
        ["lirik"] = ["lirik/şiirsel"],
        ["felsefi"] = ["felsefi/varoluşsal"],
        ["akademik"] = ["ciddi/akademik"],
        ["gizem"] = ["gizemli"],
        ["ürkütücü"] = ["tedirgin edici"],
        ["romantik"] = ["romantik"],
        ["nostaljik"] = ["nostaljik"],
        // ZORLAMA/belirsiz → çoğul (tekil hapsetme, plausible kovalara yay)
        ["kara"] = ["karanlık", "mizahi/absürt"],          // kara mizah ∨ karanlık
        ["ağır"] = ["ağır/düşündürücü", "ciddi/akademik"],
        ["duygusal"] = ["hüzünlü/melankolik", "romantik"],
        ["dokunaklı"] = ["hüzünlü/melankolik", "sıcak/samimi"],
        ["aksiyon"] = ["macera dolu", "gerilimli/sürükleyici"],
        ["sakin"] = ["pastoral/sakin", "sıcak/samimi"],
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string[]> GenreQuery = new Dictionary<string, string[]>
    {
        ["hikaye"] = ["öykü"],
        ["hikâye"] = ["öykü"],
        ["öyküler"] = ["öykü"],
        ["novella"] = ["novella/uzun öykü"],
        ["felsefe"] = ["düşünce/felsefe yazısı"],
        ["anı"] = ["otobiyografi/anı"],
        ["mektup"] = ["günlük/mektup"],
        ["günlük"] = ["günlük/mektup"],
        ["oyun"] = ["tiyatro/oyun"],
        ["tiyatro"] = ["tiyatro/oyun"],
        ["inceleme"] = ["inceleme/araştırma"],
        ["araştırma"] = ["inceleme/araştırma"],
        ["gezi"] = ["gezi yazısı"],
        ["söyleşi"] = ["röportaj/söyleşi"],
        ["röportaj"] = ["röportaj/söyleşi"],
        ["antoloji"] = ["antoloji/derleme"],
        ["derleme"] = ["antoloji/derleme"],
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string[]> ThemeQuery = new Dictionary<string, string[]>
    {
        ["distopya"] = ["distopya/ütopya"],
        ["ütopya"] = ["distopya/ütopya"],
        ["aşk"] = ["aşk/romantizm"],
        ["romantizm"] = ["aşk/romantizm"],
        ["savaş"] = ["savaş/barış", "şiddet/savaş"],
        ["suç"] = ["suç/adalet"],
        ["adalet"] = ["suç/adalet"],
        ["tarih"] = ["tarih/bellek"],
        ["bellek"] = ["tarih/bellek"],
        ["varoluş"] = ["varoluş/anlam arayışı"],
        ["din"] = ["din/inanç"],
        ["inanç"] = ["din/inanç"],
        ["doğa"] = ["doğa/çevre", "hayvan/doğa"],
        ["çevre"] = ["doğa/çevre"],
        ["bilim"] = ["bilim/keşif"],
        ["teknoloji"] = ["teknoloji/yapay zeka"],
        ["sanat"] = ["sanat/yaratıcılık"],
        ["korku"] = ["korku/doğaüstü"],
        ["gençlik"] = ["gençlik/isyan"],
        ["ekonomi"] = ["para/ekonomi"],
        ["para"] = ["para/ekonomi"],
        ["spor"] = ["spor"],
        ["aile"] = ["aile"],
        ["kimlik"] = ["kimlik"],
        ["özgürlük"] = ["özgürlük"],
        ["yalnızlık"] = ["yalnızlık/yabancılaşma"],
        ["göç"] = ["göç/sürgün"],
        ["ölüm"] = ["ölüm/kayıp"],
        ["macera"] = ["macera"],
        ["cesaret"] = ["cesaret/mücadele"],
        ["feminizm"] = ["feminist bakış"],
        ["yapayzeka"] = ["teknoloji/yapay zeka"],
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string[]> AudienceQuery = new Dictionary<string, string[]>
    {
        ["bebek"] = ["çocuk (0-3)"],
        ["genç"] = ["genç yetişkin"],
        ["ergen"] = ["genç yetişkin"],
        ["yetişkin"] = ["yetişkin-giriş", "yetişkin-orta", "yetişkin-ileri"], // çoğul: hangi seviye belirsiz
        ["çocuk"] = ["çocuk (0-3)", "çocuk (4-6)", "çocuk (7-10)", "çocuk (10-12)"], // yaş yoksa tüm çocuk
    }.ToFrozenDictionary();

    private static readonly FrozenSet<string> ChildLevels = new[]
    {
        "çocuk (0-3)", "çocuk (4-6)", "çocuk (7-10)", "çocuk (10-12)",
    }.ToFrozenSet();

    private static readonly FrozenSet<string> AdultLevels = new[]
    {
        "yetişkin-giriş", "yetişkin-orta", "yetişkin-ileri",
    }.ToFrozenSet();

    private const string BridgeLevel = "genç yetişkin";

    public static bool IsValidGenre(string value) => Genres.Contains(value);
    public static bool IsValidMood(string value) => Moods.Contains(value);
    public static bool IsValidAudienceLevel(string value) => AudienceLevels.Contains(value);
    public static bool IsValidTheme(string value) => Themes.Contains(value);

    // ── İMPORT TEKİL kanonikleştirme (SetEnrichment): v11 pipeline değeri → tek kanonik (exact + import alias).
    /// <summary>İmport: ham tur → tek kanonik; sözlük-dışıysa null.</summary>
    public static string? CanonicalGenre(string raw) => Canonical(raw, Genres, aliases: null);

    /// <summary>İmport: ham ton → tek kanonik (v11 alias); eşleşmezse null.</summary>
    public static string? CanonicalMood(string raw) => Canonical(raw, Moods, MoodAliases);

    /// <summary>İmport: ham seviye → tek kanonik (v11 alias); eşleşmezse null.</summary>
    public static string? CanonicalAudienceLevel(string raw) => Canonical(raw, AudienceLevels, AudienceLevelAliases);

    /// <summary>İmport: ham konu → tek kanonik (v11 alias); eşleşmezse null.</summary>
    public static string? CanonicalTheme(string raw) => Canonical(raw, Themes, ThemeAliases);

    // ── SORGU ÇOĞUL çözümleme (kullanıcı/agent): kelime → kanonik değer DİZİSİ. Net=tek, zorlama=çoklu
    // (OR fan-out). Exact sözlük üyesi → [değer]; sorgu-sinonimi → değerler; eşleşmezse boş.
    public static IReadOnlyList<string> ResolveGenre(string word) => Resolve(word, Genres, GenreQuery);
    public static IReadOnlyList<string> ResolveMood(string word) => Resolve(word, Moods, MoodQuery);
    public static IReadOnlyList<string> ResolveAudienceLevel(string word) => Resolve(word, AudienceLevels, AudienceQuery);
    public static IReadOnlyList<string> ResolveTheme(string word) => Resolve(word, Themes, ThemeQuery);

    // Türkçe çekim ekleri (uzundan kısaya) — kök-ayıklamada sırayla denenir. Yalnız sözlükte karşılığı
    // olan kök kabul edilir (yanlış-stem önlenir: "karanlık"→"karan" sözlükte yok → reddedilir).
    private static readonly string[] TurkishSuffixes =
        ["larını", "lerini", "ları", "leri", "lık", "lik", "luk", "lük", "lar", "ler",
         "dan", "den", "tan", "ten", "da", "de", "ta", "te", "yı", "yi", "ı", "i", "u", "ü", "a", "e"];

    private static IReadOnlyList<string> Resolve(string word, FrozenSet<string> set, FrozenDictionary<string, string[]> query)
    {
        var w = word?.Trim().ToLowerInvariant() ?? string.Empty;
        if (w.Length == 0 || w == Unknown) return [];
        if (set.Contains(w)) return [w];
        if (query.TryGetValue(w, out var values)) return values;

        // Çekim eki soy → kökü ara (yalnız sözlükte varsa; en uzun ek önce).
        foreach (var suffix in TurkishSuffixes)
        {
            if (w.Length <= suffix.Length + 2 || !w.EndsWith(suffix, StringComparison.Ordinal)) continue;
            var stem = w[..^suffix.Length];
            if (set.Contains(stem)) return [stem];
            if (query.TryGetValue(stem, out var stemValues)) return stemValues;
        }
        return [];
    }

    /// <summary>
    /// 021 "net karşılık": serbest metni TARAYIP taksonomi kelimelerini facet'e çıkarır (sözlük
    /// deterministik; LLM kaçırsa bile kurtarır). Her kelime tek eksene (mood>genre>seviye>konu
    /// öncelik) iner — belirsizlik önlenir. Eşleşen kelimeler metinden düşer (BM25 over-constrain
    /// olmasın). "belirsiz" üretilmez (Unknown facet değildir). Çok-kelimeli başlıkta yanlış-pozitif
    /// riski düşük: yalnız NET sözlük kelimeleri eşleşir, belirsizler (kara/ağır) sözlükte yok.
    /// </summary>
    public static TextFacets ExtractFromText(string? text)
    {
        var genres = new List<string>();
        var moods = new List<string>();
        var levels = new List<string>();
        var themes = new List<string>();
        var remaining = new List<string>();

        foreach (var raw in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var word = raw.Trim().ToLowerInvariant();
            // Öncelik: mood → genre → seviye → konu. İlk eşleşen eksene iner (ÇOĞUL değer olabilir),
            // kelime metinden düşer. Eşleşmezse metinde kalır (ad/yazar).
            if (ResolveMood(word) is { Count: > 0 } m) { AddAll(moods, m); continue; }
            if (ResolveGenre(word) is { Count: > 0 } g) { AddAll(genres, g); continue; }
            if (ResolveAudienceLevel(word) is { Count: > 0 } a) { AddAll(levels, a); continue; }
            if (ResolveTheme(word) is { Count: > 0 } t) { AddAll(themes, t); continue; }
            remaining.Add(raw);
        }

        return new TextFacets(genres, moods, levels, themes, string.Join(' ', remaining));

        static void AddAll(List<string> list, IReadOnlyList<string> vals)
        { foreach (var v in vals) if (!list.Contains(v)) list.Add(v); }
    }

    /// <summary>
    /// Seviye uyum grubu (similar_books hard filtresi): çocuk↔çocuk, yetişkin↔yetişkin,
    /// "genç yetişkin" köprü (herkesle), "belirsiz" her yerde.
    /// </summary>
    public static IReadOnlyList<string> CompatibleAudienceLevels(string level)
    {
        if (ChildLevels.Contains(level))
            return [.. ChildLevels, BridgeLevel, Unknown];
        if (AdultLevels.Contains(level))
            return [.. AdultLevels, BridgeLevel, Unknown];
        // köprü (genç yetişkin) ve belirsiz/bilinmeyen: tüm seviyelerle uyumlu
        return [.. AudienceLevels];
    }

    private static string? Canonical(string raw, FrozenSet<string> set, FrozenDictionary<string, string>? aliases)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0) return null;
        if (set.Contains(value)) return value;
        if (aliases is not null && aliases.TryGetValue(value, out var mapped)) return mapped;
        return null;
    }
}

// 021: ExtractFromText çıktısı — metinden lift edilen facet'ler + taksonomi kelimeleri çıkarılmış
// kalan metin (BM25 için; ad/yazar vb.).
public sealed record TextFacets(
    IReadOnlyList<string> Genres, IReadOnlyList<string> Moods,
    IReadOnlyList<string> AudienceLevels, IReadOnlyList<string> Themes, string RemainingText);