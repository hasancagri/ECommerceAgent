namespace Catalog.Api.Domains.Authors.Features.Agents.Commands;

// CreateAuthor agent yüzeyi — MCP-only iş yüzeyi. REST Command'ı aynı ad varsa AÇIK hata döner;
// agent yolu ise ImportBook.GetOrCreateAuthors mantığını izler: aynı NormalizedName varsa MEVCUT
// yazarı döndürür (idempotent, agent yeni yazar oluşturmadan bağ kurabilsin).
public static class AdminCreateAuthor
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    [InvalidatesCache(CatalogCacheTags.AgentLists)]
    public record AdminCreateAuthorCommand(Guid UserId, string Name);

    public class AdminCreateAuthorResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminCreateAuthorCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateAuthorResponse>> Handle(
            AdminCreateAuthorCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<AdminCreateAuthorResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.VALUE_EMPTY
                });

            // get-or-create: aynı normalize ad varsa mevcut yazarı döndür (ImportBook deseni).
            var normalized = NameNormalization.Normalize(cmd.Name);
            var existing = await session.Query<Author>()
                .FirstOrDefaultAsync(a => a.NormalizedName == normalized && !a.IsDeleted, ct);
            if (existing is not null)
            {
                return FeatureObjectResultModel<AdminCreateAuthorResponse>.Ok(new AdminCreateAuthorResponse
                {
                    Id = existing.Id,
                    Name = existing.Name
                });
            }

            var created = Author.Create(cmd.Name);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<AdminCreateAuthorResponse>.Error(created.Messages);

            var author = created.Data!;
            session.Store(author);

            return FeatureObjectResultModel<AdminCreateAuthorResponse>.Ok(new AdminCreateAuthorResponse
            {
                Id = author.Id,
                Name = author.Name
            });
        }
    }
}

// 074/085: ADMIN tool — TEK /mcp ucunda, scope-budamalı yayınlanır (anonim keşif seti DEĞİŞMEZ).
// Kullanıcı token'dan (ICurrentUser); scope katmanı handler'da [RequiredScope(AuthorCreate)].
[McpServerToolType]
public static class AdminCreateAuthorMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateAuthor)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.CreateAuthor)]
    public static Task<FeatureObjectResultModel<AdminCreateAuthor.AdminCreateAuthorResponse>> AdminCreateAuthorAsync(
        [Description("Yazar adi")] string name,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminCreateAuthor.AdminCreateAuthorResponse>>(
            new AdminCreateAuthor.AdminCreateAuthorCommand(userId, name), ct);
    }
}
