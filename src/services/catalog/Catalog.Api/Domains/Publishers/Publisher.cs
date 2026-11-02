namespace Catalog.Api.Domains.Publishers;

// yeni aggregate (Author/Brand kalıbı). Kitabın tek yayınevi olur (kitapyurdu künyesi birebir).
// Kendi kimliği + invariant'ı (tekil normalize ad) var → İlke II'ye göre meşru aggregate, VO değil.
// 4 sabit ad uydurmadan gelir (shape_books.py, ISBN-kararlı); yalnız import get-or-create ile doğar.
public class Publisher : AggregateRoot
{
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }

    private Publisher()
    {
    }

    /// <summary>Ad boşsa hata döner; aksi halde ad'ı trimleyip normalize ederek yeni Publisher üretir.</summary>
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<Publisher> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Publisher>.Error(new MessageItem
            {
                Property = nameof(Name),
                Code = CatalogResourceConstants.VALUE_EMPTY
            });

        var normalized = NameNormalization.Normalize(name);
        return ResultDomain<Publisher>.Ok(new Publisher
        {
            Name = name.Trim(),
            NormalizedName = normalized
        });
    }
}