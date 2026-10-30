namespace Catalog.Api.Domains.Authors;

// Brand→Author rename. Kitapyurdu künyesi dilinde marka değil "Yazar"dır; Brand zaten yazarı
// tutuyordu, isim yanlıştı. 016 düzeni sürer: yalnız import get-or-create ile doğar; ad immutable,
// rename yok. NormalizedName teklik anahtarıdır (computed unique index, Program.cs).
// ⊕ Kitapyurdu: Bio (yazar açıklaması) — get-or-create'te ilk-dolu kazanır (overwrite yok).
public class Author : AggregateRoot
{
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }
    public string? Bio { get; private set; }

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

    /// <summary>⊕ Yazar biyografisini yazar. Boş gelirse DOKUNMAZ; zaten doluysa DOKUNMAZ
    /// (ilk-dolu kazanır — seed get-or-create aynı yazarı çok kez görür, overwrite yok). Hep Ok.</summary>
    public ResultDomain SetBio(string? bio)
    {
        if (!string.IsNullOrWhiteSpace(bio) && string.IsNullOrWhiteSpace(Bio))
            Bio = bio.Trim();
        return ResultDomain.Ok();
    }
}