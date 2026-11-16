namespace Catalog.Api.Domains.Products.ValueObjects;

// Product aggregate'inin value object'leri — aggregate başına TEK dosya (VO başına ayrı dosya açılmaz).
// 095: ProductDimensions + SeoMetadata + ProductCategoryAssignment SÖKÜLDÜ (fiziki/nop ekseni +
// çoklu-kategori modeli kalktı — PDF-only sade künye).

/// <summary>
/// Para değeri (tutar + para birimi). Değeriyle tanımlı value object — kimliği yok.
/// Fiyat çıplak decimal yerine birim taşıyan VO'ya sarılır (invariant: tutar >= 0).
/// Dış kontratlara (ProductChangedEvent) yalnız Amount sızar.
/// </summary>
public record Money
{
    public decimal Amount { get; private init; }
    public string Currency { get; private init; } = "TRY";

    private Money() { }

    /// <summary>Tutar negatifse null döner (guard çağıranda). Aksi halde Money üretir.</summary>
    public static Money? Create(decimal amount, string currency = "TRY")
    {
        if (amount < 0)
            return null;
        return new Money { Amount = amount, Currency = currency };
    }

    public static Money Zero(string currency = "TRY") => new() { Amount = 0, Currency = currency };
}

/// <summary>
/// Kitap etiketi (009) — tema/ruh hali/alt-tür. Değeriyle tanımlı value object; kimliği/yaşam
/// döngüsü yok (entity DEĞİL). TEK alan Value = normalize edilmiş metin, LOWERCASE (orijinal casing
/// SAKLANMAZ — kullanıcı kararı); trim + whitespace-collapse + ToLowerInvariant. Türkçe ı/ş/ğ/ü/ö/ç
/// ToUpper'da katlanmadığından (ı≠I) NameNormalization reuse EDİLMEZ; onun yerine lowercase + ASCII
/// ikiz (AsciiVariant): "karanlık" → "karanlık" + "karanlik" iki kayıt, her iki yazımla aranabilir.
/// Kitaplar-arası gruplama değer-eşitliğinden. Marten: parametresiz private ctor + private init
/// (get-only+parametreli-ctor okuma anında 500 verirdi — Money deseni, non-public setter kuralı).
/// </summary>
public record BookTag
{
    public string Value { get; private init; } = default!;

    private BookTag() { }

    /// <summary>Ham etiketi lowercase normalize eder; boş/whitespace ise Error (VALUE_EMPTY).</summary>
    public static ResultDomain<BookTag> Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ResultDomain<BookTag>.Error(new MessageItem
            { Property = nameof(Value), Code = CatalogResourceConstants.VALUE_EMPTY });

        var normalized = System.Text.RegularExpressions.Regex
            .Replace(raw.Trim(), @"\s+", " ").ToLowerInvariant();
        if (normalized.Length == 0)
            return ResultDomain<BookTag>.Error(new MessageItem
            { Property = nameof(Value), Code = CatalogResourceConstants.VALUE_EMPTY });

        return ResultDomain<BookTag>.Ok(new BookTag { Value = normalized });
    }

    /// <summary>Türkçe karakter içeriyorsa ASCII-katlanmış ikiz BookTag (arama köprüsü: "karanlık"
    /// ↔ "karanlik"); fark yoksa null. Kullanıcı kararı: ikiz AYRI kayıt olarak saklanır.</summary>
    public BookTag? AsciiVariant()
    {
        var ascii = Value
            .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g')
            .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c')
            .Replace('â', 'a').Replace('î', 'i').Replace('û', 'u');
        return ascii == Value ? null : new BookTag { Value = ascii };
    }
}
