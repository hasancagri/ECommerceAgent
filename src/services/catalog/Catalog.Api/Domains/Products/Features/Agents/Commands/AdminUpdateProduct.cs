namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// US1: admin künye güncelleme (agent yüzeyi) — UpdateProduct İKİZİ (bilinçli tekrar) ama KISMİ
// güncelleme: yalnız verilen alanlar değişir (agent "fiyatı 95 yap" der, tüm formu göndermez).
// Gerçek fiyat değişimi ProductPriceChange + ProductChangedEvent.OldPrice akışını AYNEN tetikler.
// Yanıt ürünün GÜNCEL hâli (FR-003: agent ek çağrısız gösterir).
public static class AdminUpdateProduct
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminUpdateProductCommand(
        Guid UserId,
        Guid ProductId,
        string? Name,
        string? ShortDescription,
        string? FullDescription,
        decimal? Price,
        List<Guid>? AuthorIds,
        List<string>? NewAuthorNames,
        Guid? PublisherId,
        string? NewPublisherName,
        Guid? CategoryId,
        string? ImageUrl);

    public class AuthorItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class AdminUpdateProductResponse
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = default!;
        public string ShortDescription { get; set; } = default!;
        public string FullDescription { get; set; } = default!;
        public string? Isbn { get; set; }
        public decimal Price { get; set; }
        public bool IsPublished { get; set; }
        public string? ImageUrl { get; set; }
        public List<AuthorItem> Authors { get; set; } = [];
        public string PublisherName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminUpdateProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminUpdateProductResponse>> Handle(
            AdminUpdateProductCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<AdminUpdateProductResponse>.NotFound();

            var changed = new List<string>();

            if (!string.IsNullOrWhiteSpace(cmd.Name) && cmd.Name != product.Name)
            {
                var rename = product.Rename(cmd.Name);
                if (!rename.IsSuccess)
                    return FeatureObjectResultModel<AdminUpdateProductResponse>.Error(rename.Messages);
                changed.Add("Name");
            }

            if (cmd.ShortDescription is not null || cmd.FullDescription is not null)
            {
                product.UpdateDescriptions(
                    cmd.ShortDescription ?? product.ShortDescription,
                    cmd.FullDescription ?? product.FullDescription);
                changed.Add("Description");
            }

            // FR-013: yalnız GERÇEK fiyat değişimi geçmişe satır düşürür.
            var oldPrice = product.Price.Amount;
            var priceChanged = false;
            if (cmd.Price is { } newPrice && newPrice != oldPrice)
            {
                var price = Money.Create(newPrice);
                if (price is null)
                    return FeatureObjectResultModel<AdminUpdateProductResponse>.Error(new MessageItem
                    {
                        Property = nameof(cmd.Price),
                        Code = CatalogResourceConstants.PRODUCT_PRICE_NEGATIVE
                    });

                product.SetPrice(price);
                session.Store(ProductPriceChange.Create(product.Id, oldPrice, newPrice, DateTime.UtcNow));
                priceChanged = true;
                changed.Add($"Price {oldPrice}→{newPrice}");
            }

            // Yazarlar: AuthorIds verilirse SETİ DEĞİŞTİRİR; NewAuthorNames get-or-create ile eklenir.
            if (cmd.AuthorIds is { Count: > 0 } || cmd.NewAuthorNames is { Count: > 0 })
            {
                var authors = new List<Author>();
                var targetIds = cmd.AuthorIds is { Count: > 0 } ? cmd.AuthorIds : [.. product.AuthorIds];
                foreach (var authorId in targetIds.Distinct())
                {
                    var author = await session.LoadAsync<Author>(authorId, ct);
                    if (author is null || author.IsDeleted)
                        return FeatureObjectResultModel<AdminUpdateProductResponse>.Error(new MessageItem
                        { Property = nameof(cmd.AuthorIds), Code = CatalogResourceConstants.RECORD_NOT_FOUND });
                    authors.Add(author);
                }

                foreach (var name in (cmd.NewAuthorNames ?? []).Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    var normalized = NameNormalization.Normalize(name);
                    var existing = authors.FirstOrDefault(a => a.NormalizedName == normalized)
                                   ?? await session.Query<Author>()
                                       .FirstOrDefaultAsync(a => a.NormalizedName == normalized, ct);
                    if (existing is null)
                    {
                        existing = Author.Create(name).Data!;
                        session.Store(existing);
                    }

                    if (authors.All(a => a.Id != existing.Id))
                        authors.Add(existing);
                }

                var setAuthors = product.SetAuthors(authors.Select(a => a.Id));
                if (!setAuthors.IsSuccess)
                    return FeatureObjectResultModel<AdminUpdateProductResponse>.Error(setAuthors.Messages);
                changed.Add("Authors");
            }

            if (cmd.PublisherId is not null || !string.IsNullOrWhiteSpace(cmd.NewPublisherName))
            {
                Publisher? publisher = null;
                if (!string.IsNullOrWhiteSpace(cmd.NewPublisherName))
                {
                    var normalized = NameNormalization.Normalize(cmd.NewPublisherName);
                    publisher = await session.Query<Publisher>()
                        .FirstOrDefaultAsync(p => p.NormalizedName == normalized, ct);
                    if (publisher is null)
                    {
                        publisher = Publisher.Create(cmd.NewPublisherName).Data!;
                        session.Store(publisher);
                    }
                }
                else if (cmd.PublisherId is { } publisherId)
                {
                    publisher = await session.LoadAsync<Publisher>(publisherId, ct);
                }

                if (publisher is null || publisher.IsDeleted)
                    return FeatureObjectResultModel<AdminUpdateProductResponse>.Error(new MessageItem
                    {
                        Property = nameof(cmd.PublisherId),
                        Code = CatalogResourceConstants.RECORD_NOT_FOUND
                    });

                var setPublisher = product.SetPublisher(publisher.Id);
                if (!setPublisher.IsSuccess)
                    return FeatureObjectResultModel<AdminUpdateProductResponse>.Error(setPublisher.Messages);
                changed.Add("Publisher");
            }

            // K4: dış kontrat tek kategori görür — hedef atanmamışsa eskiler sökülüp yenisi primary yazılır.
            if (cmd.CategoryId is { } categoryId && product.Categories.All(c => c.CategoryId != categoryId))
            {
                var category = await session.LoadAsync<Category>(categoryId, ct);
                if (category is null || category.IsDeleted)
                    return FeatureObjectResultModel<AdminUpdateProductResponse>.Error(new MessageItem
                    {
                        Property = nameof(cmd.CategoryId),
                        Code = CatalogResourceConstants.RECORD_NOT_FOUND
                    });

                foreach (var link in product.Categories.ToList())
                    product.RemoveFromCategory(link.CategoryId);
                var assign = product.AssignToCategory(categoryId, isFeatured: false, displayOrder: 0);
                if (!assign.IsSuccess)
                    return FeatureObjectResultModel<AdminUpdateProductResponse>.Error(assign.Messages);
                changed.Add("Category");
            }

            if (!string.IsNullOrWhiteSpace(cmd.ImageUrl))
            {
                product.SetImage(cmd.ImageUrl);
                changed.Add("Image");
            }

            session.Store(product);

            // Yanıt + fat event için künye adları (güncel bağlar).
            var finalAuthors = (await session.LoadManyAsync<Author>(ct, product.AuthorIds.ToArray())).ToList();
            var finalPublisher = await session.LoadAsync<Publisher>(product.PublisherId, ct);
            var finalCategoryId = product.Categories.Select(c => c.CategoryId).FirstOrDefault();
            var finalCategory = finalCategoryId == Guid.Empty
                ? null
                : await session.LoadAsync<Category>(finalCategoryId, ct);

            // writer-publishes; 058: yalnız yayındaki ürün event yayar (draft vitrine sızmaz).
            if (product.Published)
            {
                await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                    product.Id, product.Name, product.FullDescription, product.Price.Amount,
                    finalAuthors.Select(a => new IntegrationEvents.AuthorRef(a.Id, a.Name)).ToList(),
                    product.PublisherId, finalPublisher?.Name ?? string.Empty,
                    finalCategoryId, finalCategory?.Name ?? string.Empty,
                    product.ImageUrl, IsDeleted: false,
                    // yalnız GERÇEK fiyat değişiminde dolu — Library alarm tetiği.
                    OldPrice: priceChanged ? oldPrice : null));
            }

            return FeatureObjectResultModel<AdminUpdateProductResponse>.Ok(new AdminUpdateProductResponse
            {
                ProductId = product.Id,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                Isbn = product.Gtin,
                Price = product.Price.Amount,
                IsPublished = product.Published,
                ImageUrl = product.ImageUrl,
                Authors = finalAuthors.Select(a => new AuthorItem { Id = a.Id, Name = a.Name }).ToList(),
                PublisherName = finalPublisher?.Name ?? string.Empty,
                CategoryName = finalCategory?.Name ?? string.Empty,
            });
        }
    }
}

[McpServerToolType]
public static class AdminUpdateProductMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.UpdateProduct)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.UpdateProduct)]
    public static Task<FeatureObjectResultModel<AdminUpdateProduct.AdminUpdateProductResponse>> AdminUpdateProductAsync(
        [Description("Guncellenecek urunun kimligi")] Guid productId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Yeni urun adi (degistirmeyeceksen bos birak)")] string? name = null,
        [Description("Yeni kisa aciklama")] string? shortDescription = null,
        [Description("Yeni tam aciklama")] string? fullDescription = null,
        [Description("Yeni fiyat (TL, >= 0); yalniz gercek degisim gecmise yazilir")] decimal? price = null,
        [Description("Yazar kimlikleri — verilirse yazar setini TAMAMEN degistirir")] List<Guid>? authorIds = null,
        [Description("Katalogda olmayan yeni yazar adlari (olusturulup eklenir)")] List<string>? newAuthorNames = null,
        [Description("Yeni yayinevi kimligi")] Guid? publisherId = null,
        [Description("Katalogda olmayan yeni yayinevi adi (olusturulur)")] string? newPublisherName = null,
        [Description("Yeni kategori kimligi (list_categories'ten)")] Guid? categoryId = null,
        [Description("Yeni kapak gorseli URL'i")] string? imageUrl = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminUpdateProduct.AdminUpdateProductResponse>>(
            new AdminUpdateProduct.AdminUpdateProductCommand(
                userId, productId, name, shortDescription, fullDescription, price,
                authorIds, newAuthorNames, publisherId, newPublisherName, categoryId, imageUrl), ct);
    }
}
