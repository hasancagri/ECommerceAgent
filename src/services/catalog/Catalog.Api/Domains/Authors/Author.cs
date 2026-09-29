namespace Catalog.Api.Domains.Authors;

// Brand→Author rename. Kitapyurdu künyesi dilinde marka değil "Yazar"dır; Brand zaten yazarı
// tutuyordu, isim yanlıştı. 016 düzeni sürer: yalnız import get-or-create ile doğar; ad immutable,
// rename yok. NormalizedName teklik anahtarıdır (computed unique index, Program.cs).
public class Author : AggregateRoot
{
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }

    private Author()
    {
    }

    // JasperFxIgnore: tek parametreli statik Create, event-sourcing evolver konvansiyonuyla çakışır;
    // bu bir domain fabrikasıdır, projection değil (source generator'ı devre dışı bırakır).
    /// <summary>Ad boşsa hata döner; aksi halde ad'ı trimleyip normalize ederek yeni Author üretir.</summary>
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<Author> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Author>.Error(new MessageItem
            {
                Property = nameof(Name),
                Code = CatalogResourceConstants.VALUE_EMPTY
            });

        var normalized = NameNormalization.Normalize(name);
        return ResultDomain<Author>.Ok(new Author
        {
            Name = name.Trim(),
            NormalizedName = normalized
        });
    }
}