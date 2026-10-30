namespace Catalog.Api.Domains.Categories;

/// <summary>
/// Kategori — kapalı 2 seviyeli ağacın düğümü (095, FR-006/007). Ağaç YALNIZ seed'den kurulur
/// (books.json kanonik yolları); ebeveyn doğumda sabittir — taşıma/yeniden adlandırma/düzenleme
/// davranışı YOKTUR. NormalizedName teklik anahtarıdır (computed unique index) ve ad ağaç-global
/// unique varsayımına dayanır (ihlalde seed deterministik hata verir).
/// </summary>
public class Category : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;

    // Kök kategori için null; en fazla 2 seviye (kök → leaf). Doğumda sabit — mutator yok.
    public Guid? ParentCategoryId { get; private set; }

    private Category()
    {
    }

    // JasperFxIgnore: statik Create fabrikası event-sourcing evolver konvansiyonuyla çakışır;
    // bu bir domain fabrikasıdır, projection değil (source generator'ı devre dışı bırakır).
    /// <summary>Ad'ı doğrular, normalize eder ve yeni bir Category üretir; ad boşsa hata döner.
    /// Ebeveyn doğumda sabitlenir (kapalı ağaç — FR-007).</summary>
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<Category> Create(string name, Guid? parentCategoryId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Category>.Error(new MessageItem
            {
                Property = nameof(Name),
                Code = CatalogResourceConstants.VALUE_EMPTY
            });

        return ResultDomain<Category>.Ok(new Category
        {
            Name = name.Trim(),
            NormalizedName = NameNormalization.Normalize(name),
            ParentCategoryId = parentCategoryId,
        });
    }
}
