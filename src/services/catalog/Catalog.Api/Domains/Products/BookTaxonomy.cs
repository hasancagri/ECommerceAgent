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
        ["felsefe"] = "varoluş/anlam arayışı",
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

    /// <summary>Ham tur değerini kanonikleştirir; sözlük-dışıysa null (çağıran düşürür).</summary>
    public static string? CanonicalGenre(string raw) => Canonical(raw, Genres, aliases: null);

    /// <summary>Ham ton değerini kanonikleştirir (takma-ad dahil); eşleşmezse null.</summary>
    public static string? CanonicalMood(string raw) => Canonical(raw, Moods, MoodAliases);

    /// <summary>Ham seviye değerini kanonikleştirir (takma-ad dahil); eşleşmezse null.</summary>
    public static string? CanonicalAudienceLevel(string raw) => Canonical(raw, AudienceLevels, AudienceLevelAliases);

    /// <summary>Ham konu değerini kanonikleştirir (takma-ad dahil); eşleşmezse null.</summary>
    public static string? CanonicalTheme(string raw) => Canonical(raw, Themes, ThemeAliases);

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