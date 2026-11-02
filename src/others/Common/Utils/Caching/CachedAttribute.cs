namespace Common.Utils.Caching;

/// <summary>
/// Bir okuma sorgusu (query record) bu işaretle önbelleklenir. Bildirimsel/cross-cutting:
/// handler gövdesine kod girmez. <see cref="CachingMessageBus"/> attribute'u okuyup çağrıyı sarar.
/// </summary>
/// <param name="tag">Geçersizleştirme etiketi (v1 kaba taneli: "catalog-products").</param>
/// <param name="ttlSeconds">L2 (paylaşımlı) yaşam süresi. L1 TTL global ayardan (≤5sn) gelir.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CachedAttribute(string tag, int ttlSeconds) : Attribute
{
    public string Tag { get; } = tag;
    public int TtlSeconds { get; } = ttlSeconds;
}