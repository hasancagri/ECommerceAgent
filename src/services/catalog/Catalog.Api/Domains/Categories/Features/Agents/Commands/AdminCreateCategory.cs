namespace Catalog.Api.Domains.Categories.Features.Agents.Commands;

// CreateCategory REST Command'ının agent İKİZİ (bilinçli tekrar) — MCP-only iş yüzeyi.
// Yaratım bilinçlidir: aynı ad varsa get-or-create DEĞİL, açık hata (feed yolu Upsert'te).
// Yazılan kategori vitrindedir (K8).
public static class AdminCreateCategory
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    [InvalidatesCache(CatalogCacheTags.AgentLists)]
    public record AdminCreateCategoryCommand(
        Guid UserId,
        string Name,
        string? Description,
        Guid? ParentId,
        int DisplayOrder,
        string? MetaTitle,
        string? MetaKeywords,
        string? MetaDescription);

    public class AdminCreateCategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminCreateCategoryCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateCategoryResponse>> Handle(
            AdminCreateCategoryCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var normalized = NameNormalization.Normalize(cmd.Name ?? string.Empty);
            var exists = await session.Query<Category>()
                .AnyAsync(x => x.NormalizedName == normalized && !x.IsDeleted, ct);
            if (exists)
            {
                return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.CATEGORY_ALREADY_EXISTS
                });
            }

            if (cmd.ParentId is not null)
            {
                var parent = await session.LoadAsync<Category>(cmd.ParentId.Value, ct);
                if (parent is null || parent.IsDeleted)
                {
                    return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(new MessageItem
                    {
                        Property = nameof(cmd.ParentId),
                        Code = CatalogResourceConstants.RECORD_NOT_FOUND
                    });
                }
            }

            var created = Category.Create(cmd.Name!, cmd.Description ?? string.Empty, cmd.ParentId, cmd.DisplayOrder);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(created.Messages);

            var category = created.Data!;

            // K8 düzeni: yazılan kategori vitrindedir.
            var publish = category.SetPublished(true);
            if (!publish.IsSuccess)
                return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(publish.Messages);

            if (cmd.MetaTitle is not null || cmd.MetaKeywords is not null || cmd.MetaDescription is not null)
            {
                var seo = category.SetSeo(SeoMetadata.Create(cmd.MetaTitle, cmd.MetaKeywords, cmd.MetaDescription));
                if (!seo.IsSuccess)
                    return FeatureObjectResultModel<AdminCreateCategoryResponse>.Error(seo.Messages);
            }

            session.Store(category);

            return FeatureObjectResultModel<AdminCreateCategoryResponse>.Ok(new AdminCreateCategoryResponse
            {
                Id = category.Id,
                Name = category.Name
            });
        }
    }
}

// 074/085: ADMIN tool'ları — TEK /mcp ucunda, scope-budamalı yayınlanır (anonim keşif seti DEĞİŞMEZ).
// Kullanıcı token'dan (ICurrentUser); scope katmanı handler'da [RequiredScope(CategoryCreate)].
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).
[McpServerToolType]
public static class AdminCreateCategoryMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateCategory)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.CreateCategory)]
    public static Task<FeatureObjectResultModel<AdminCreateCategory.AdminCreateCategoryResponse>> AdminCreateCategoryAsync(
        [Description("Kategori adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Kategori aciklamasi (opsiyonel)")] string? description = null,
        [Description("Ust kategori kimligi (list_categories'ten; kok icin bos birak)")] Guid? parentId = null,
        [Description("Vitrin siralama degeri (varsayilan 0)")] int displayOrder = 0,
        [Description("SEO meta baslik (opsiyonel)")] string? metaTitle = null,
        [Description("SEO meta anahtar kelimeler (opsiyonel)")] string? metaKeywords = null,
        [Description("SEO meta aciklama (opsiyonel)")] string? metaDescription = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminCreateCategory.AdminCreateCategoryResponse>>(
            new AdminCreateCategory.AdminCreateCategoryCommand(
                userId, name, description, parentId, displayOrder, metaTitle, metaKeywords, metaDescription), ct);
    }
}
