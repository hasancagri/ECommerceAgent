using System.Security.Cryptography;

namespace Catalog.Api.Import;

// US1/FR-001: Excel yükleme ekranını yetkilendiren kısa-ömürlü TEK KULLANIMLIK capability token
// (078 CredentialEntrySession ikizi). Agent `import_catalog` tool'u üretir; link
// {PublicBaseUrl}/catalog-import/{Token} ile taşınır. Başarılı POST (dosya alındı) Consume ile öldürür;
// GET tüketmez (form açıp vazgeçmek linki öldürmez, süre öldürür). token = yetki (İLKE V v1.11.1
// istisnası: tek-amaç, tek-kullanım, dosya-yazma-only). Import makinesi — aggregate DEĞİL (Domains/ dışı).
public class ImportSession : AggregateRoot
{
    private ImportSession()
    {
    }

    // 256-bit rastgele, URL-safe (base64url) yetki token'ı — link = yetki.
    public string Token { get; private set; } = string.Empty;

    // Linki üreten admin (denetim izi; token izde YER ALMAZ).
    public Guid RequestedByUserId { get; private set; }

    // Oturumun ölüm anı (üretim + LinkLifetime); geçmişse oturum pasif ölüdür.
    public DateTimeOffset ExpiresAt { get; private set; }

    // Başarılı POST (dosya alındı) anı; dolu ise oturum tüketilmiştir (tek kullanım).
    public DateTimeOffset? ConsumedAt { get; private set; }

    // Yükleme sonrası staging'e alınan satır sayısı (Consume'da yazılır).
    public int? RowCount { get; private set; }

    /// <summary>Yeni yükleme oturumu üretir: 256-bit URL-safe token + yaşam süresi. Pozitif olmayan süre RET.</summary>
    // İki parametre bilinçli (078 ikizi): JasperFx tek-parametreli statik Create'i olay-kaynaklı aggregate
    // sanıp evolver üretir; ikinci parametre bunu engeller (RequestedByUserId denetim için de gerçek fayda).
    public static ResultDomain<ImportSession> Create(Guid requestedByUserId, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
            return ResultDomain<ImportSession>.Error(new MessageItem
            { Property = nameof(ExpiresAt), Code = CatalogResourceConstants.IMPORT_LINK_LIFETIME_INVALID });

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return ResultDomain<ImportSession>.Ok(new ImportSession
        {
            Token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            RequestedByUserId = requestedByUserId,
            ExpiresAt = DateTimeOffset.UtcNow + lifetime
        });
    }

    /// <summary>Oturum hâlâ kullanılabilir mi (süre dolmamış + tüketilmemiş) — saf getter.</summary>
    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now <= ExpiresAt;

    /// <summary>Oturumu tüketir (tek kullanım invariant'ı): süresi geçmiş ya da zaten tüketilmişse RET, değilse işaretler + satır sayısı yazar.</summary>
    public ResultDomain Consume(int rowCount, DateTimeOffset now)
    {
        if (!IsUsable(now))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Token), Code = CatalogResourceConstants.IMPORT_SESSION_NOT_USABLE });

        ConsumedAt = now;
        RowCount = rowCount;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}
