namespace Common.Utils.Caching;

/// <summary>
/// Bir yazma komutu (command record) bu işaretle, başarılı çalışması sonrası ilgili etikete ait
/// tüm önbellek girdilerini iki katmandan boşalttırır. Commit sonrası (bkz.
/// <see cref="CachingMessageBus"/>) <see cref="CacheInvalidationRequested"/> yayınlanır;
/// boşaltma durable handler'da at-least-once koşar — Redis kesintisi komut sonucunu etkilemez.
/// </summary>
/// <param name="tag">Boşaltılacak etiket (v1 kaba taneli: "catalog-products").</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class InvalidatesCacheAttribute(string tag) : Attribute
{
    public string Tag { get; } = tag;
}