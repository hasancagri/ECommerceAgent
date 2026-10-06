namespace Catalog.Api.Domains.Authors.Features.Agents.Queries;

// taşıma (kullanıcı kararı): yazar envanteri Author aggregate'inin evinde. Yalnız YAYINDAKİ en az
// bir üründe geçen yazarlar (FR-006 ruhu). search + maxResults + TotalCount; kitap sayısı çok olan önce.
public static class ListAuthors
{
    public const int DefaultMaxResults = 50;
    public const int MaxResultsLimit = 200;

    [Cached(CatalogCacheTags.AgentLists, 1200)]
    public record ListAuthorsQuery(string? Search = null, int? MaxResults = null);

    public class AuthorItem
    {
        public Guid AuthorId { get; set; }
        public string Name { get; set; } = null!;
        public int ProductCount { get; set; }
    }

    public class ListAuthorsResponse
    {
        public List<AuthorItem> Items { get; set; } = [];
        public int TotalCount { get; set; }
    }

    // Saf çekirdek: yayındaki ürünlerin AuthorIds'i düzleştirilir (çok-yazarlı kitap her yazarına
    // sayılır), ada arama (case-insensitive alt-dizge), ProductCount DESC + Name ASC, kırpma.
    // TotalCount = kırpma ÖNCESİ.
    public static ListAuthorsResponse Build(
        IEnumerable<Products.Product> publishedProducts, IReadOnlyList<Author> allAuthors, ListAuthorsQuery query)
    {
        var byId = allAuthors.ToDictionary(a => a.Id);

        var authors = publishedProducts
            .SelectMany(p => p.AuthorIds.Distinct())
            .GroupBy(id => id)
            .Where(g => byId.ContainsKey(g.Key))
            .Select(g => new AuthorItem { AuthorId = g.Key, Name = byId[g.Key].Name, ProductCount = g.Count() });

        if (!string.IsNullOrWhiteSpace(query.Search))
            authors = authors.Where(a =>
                a.Name.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));

        var filtered = authors.ToList();
        var maxResults = query.MaxResults is null
            ? DefaultMaxResults
            : Math.Clamp(query.MaxResults.Value, 1, MaxResultsLimit);

        return new ListAuthorsResponse
        {
            Items = filtered
                .OrderByDescending(a => a.ProductCount).ThenBy(a => a.Name)
                .Take(maxResults)
                .ToList(),
            TotalCount = filtered.Count
        };
    }

    public class ListAuthorsQueryHandler
    {
        public async Task<FeatureObjectResultModel<ListAuthorsResponse>> Handle(
            ListAuthorsQuery query, IQuerySession session, CancellationToken ct)
        {
            var products = await session.Query<Products.Product>()
                .Where(x => x.Published).ToListAsync(ct);
            var authors = await session.Query<Author>().ToListAsync(ct);

            return FeatureObjectResultModel<ListAuthorsResponse>.Ok(Build(products, authors, query));
        }
    }
}

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).
[McpServerToolType]
public static class ListAuthorsMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.ListAuthors)]
    [Description(Shared.McpToolDescriptions.CatalogTools.ListAuthors)]
    public static Task<FeatureObjectResultModel<ListAuthors.ListAuthorsResponse>> ListAuthorsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Yazar adinda gecen metin (bos = tumu)")] string? search = null,
        [Description("Sonuc sayisi; varsayilan 50, en fazla 200")] int? maxResults = null)
        => bus.InvokeAsync<FeatureObjectResultModel<ListAuthors.ListAuthorsResponse>>(
            new ListAuthors.ListAuthorsQuery(search, maxResults), ct);
}
