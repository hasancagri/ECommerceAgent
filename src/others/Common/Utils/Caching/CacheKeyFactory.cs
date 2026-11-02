using System.Text;
using System.Text.Json;

namespace Common.Utils.Caching;

/// <summary>
/// Bir mesajdan (query) deterministik önbellek anahtarı üretir:
/// "{prefix}:{queryTypeName}:{paramHash}". paramHash, mesajın JSON gösteriminden FNV-1a ile
/// hesaplanır — süreçler/instance'lar arası tutarlı (string.GetHashCode randomizasyonu KULLANILMAZ,
/// aksi halde iki instance aynı ürün için farklı L2 anahtarı üretir). Parametresiz query
/// (ör. GetAllProductsQuery) "{}" serileşir → sabit anahtar (FR-004). Catalog kapsamında
/// kullanıcı/scope bağlamı anahtara girmez (paylaşımlı anahtar).
/// </summary>
public static class CacheKeyFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static string Build(string prefix, object message)
    {
        var type = message.GetType();
        var json = JsonSerializer.Serialize(message, type, SerializerOptions);
        // Hash'e FullName girer: ayni KISA ada sahip farkli slice record'lari (or. Queries/GetAddressesQuery
        // ile Agent/GetAddressesQuery) ayni anahtara dusmesin — farkli Response tipleri carpisirdi.
        return $"{prefix}:{type.Name}:{Fnv1a64(type.FullName + json):x16}";
    }

    // Kriptografik değil — yalnız anahtarı kısa/sabit uzunluğa indirmek için. Saf matematik olduğundan
    // her süreçte aynı sonucu verir (cross-instance L2 tutarlılığı için şart).
    private static ulong Fnv1a64(string value)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }
}