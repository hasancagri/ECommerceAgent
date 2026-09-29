namespace Catalog.Api.Domains.Categories.Features.Agents.Commands;

// UpdateCategory REST Command'ının agent İKİZİ (bilinçli tekrar) ama KISMİ güncelleme
// (AdminUpdateProduct deseni): agent "adını X yap" der, tüm formu göndermez — yalnız verilen alan değişir.
// Bulunamadı → NotFound döner.
public static class AdminUpdateCategory
{
    // NOT: Category aggregate'i yalnız Rename + SetSeo mutasyonu sunar (Description Create'te sabitlenir,
    // mutator yok). Bu yüzden kısmi güncelleme yüzeyi ad + SEO ile sınırlı — dead param eklenmez.
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminUpdateCategoryCommand(
        Guid UserId,
        Guid CategoryId,
        string? Name,
        string? MetaTitle,
        string? MetaKeywords,
        string? MetaDescription);

    public class AdminUpdateCategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
    }

    [Transactional]
    public class AdminUpdateCategoryCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminUpdateCategoryResponse>> Handle(
            AdminUpdateCategoryCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var category = await session.LoadAsync<Category>(cmd.CategoryId, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<AdminUpdateCategoryResponse>.NotFound();

            var changed = new List<string>();

            if (!string.IsNullOrWhiteSpace(cmd.Name) && cmd.Name != category.Name)
            {
                var rename = category.Rename(cmd.Name);
                if (!rename.IsSuccess)
                    return FeatureObjectResultModel<AdminUpdateCategoryResponse>.Error(rename.Messages);
                changed.Add("Name");
            }

            // SEO KISMİ: verilen alanlar üzerine yazılır, verilmeyenler mevcut değerini korur.
            if (cmd.MetaTitle is not null || cmd.MetaKeywords is not null || cmd.MetaDescription is not null)
            {
                var seo = category.SetSeo(SeoMetadata.Create(
                    cmd.MetaTitle ?? category.Seo.MetaTitle,
                    cmd.MetaKeywords ?? category.Seo.MetaKeywords,
                    cmd.MetaDescription ?? category.Seo.MetaDescription));
                if (!seo.IsSuccess)
                    return FeatureObjectResultModel<AdminUpdateCategoryResponse>.Error(seo.Messages);
                changed.Add("Seo");
            }

            session.Store(category);

            return FeatureObjectResultModel<AdminUpdateCategoryResponse>.Ok(new AdminUpdateCategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            });
        }
    }
}

[McpServerToolType]
public static class AdminUpdateCategoryMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.UpdateCategory)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.UpdateCategory)]
    public static Task<FeatureObjectResultModel<AdminUpdateCategory.AdminUpdateCategoryResponse>> AdminUpdateCategoryAsync(
        [Description("Guncellenecek kategori kimligi (list_categories'ten)")] Guid categoryId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Yeni kategori adi (degistirmeyeceksen bos birak)")] string? name = null,
        [Description("Yeni SEO meta baslik")] string? metaTitle = null,
        [Description("Yeni SEO meta anahtar kelimeler")] string? metaKeywords = null,
        [Description("Yeni SEO meta aciklama")] string? metaDescription = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminUpdateCategory.AdminUpdateCategoryResponse>>(
            new AdminUpdateCategory.AdminUpdateCategoryCommand(
                userId, categoryId, name, metaTitle, metaKeywords, metaDescription), ct);
    }
}
