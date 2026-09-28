namespace Catalog.Api.Domains.Products.Features.Agents.Commands;

// 074: admin künye OLUŞTURMA (agent yüzeyi) — REST CreateProduct söküldü, doktrin kayması: import (051)
// artık tek giriş değil. ISBN mağazada Product.Gtin'de yaşar (ProductId rastgele Guid; ImportBook emsali).
// Çakışma: aynı Gtin varsa Error (çoğaltma yok — agent admin_update_product'a yönlendirir). Yeni ürün
// DRAFT doğar (eski REST auto-publish'ten farklı): yayın ayrı adım (admin_set_published). Draft olduğu için
// ProductChangedEvent YAYILMAZ (AdminUpdateProduct deseni). Fiyat>0 ise geçmişin ilk satırı.
// BUGFIX: ProductAdded ImportBook emsaliyle AYNI ANDA yayılır (InitialStock=0) — Stock'un BarcodeLink+
// OnHand satırı yalnız bu event'ten doğar (StockEventHandlers.Handle(ProductAdded)); event olmadan admin
// stok set/adjust edemez VE checkout CommitStock RECORD_NOT_FOUND ile kalıcı reddeder. ISBN çakışma guard'ı
// (üstte) tek-seferlik create'i garanti ettiği için tekrar tetiklenip mevcut stoğu InitialStock=0'a EZME
// riski yok.
public static class AdminCreateProduct
{
    [RequiredScope(AuthorizationScopes.AdminCatalogWrite)]
    public record AdminCreateProductCommand(
        Guid UserId,
        string Name,
        string Isbn,
        decimal Price,
        string? ShortDescription,
        string? FullDescription,
        List<Guid>? AuthorIds,
        List<string>? NewAuthorNames,
        Guid? PublisherId,
        string? NewPublisherName,
        Guid? CategoryId,
        string? ImageUrl);

    public class AdminCreateProductResponse
    {
        public Guid ProductId { get; set; }
        public string Isbn { get; set; } = default!;
        public bool IsPublished { get; set; }
    }

    [Transactional]
    public class AdminCreateProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateProductResponse>> Handle(
            AdminCreateProductCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.Isbn))
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Isbn), Code = CatalogResourceConstants.PRODUCT_SKU_REQUIRED });

            // Kimlik = ISBN (Gtin). Çakışma kontrolü: aynı Gtin varsa çoğaltma yok → Error.
            var existing = await session.Query<Product>().FirstOrDefaultAsync(p => p.Gtin == cmd.Isbn, ct);
            if (existing is not null)
            {
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Isbn), Code = CatalogResourceConstants.PRODUCT_ISBN_EXISTS });
            }

            var price = Money.Create(cmd.Price);
            if (price is null)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Price), Code = CatalogResourceConstants.PRODUCT_PRICE_NEGATIVE });

            // Yazar(lar): AuthorIds mevcut olmalı; NewAuthorNames get-or-create (normalize). En az bir yazar.
            var authors = new List<Author>();
            foreach (var authorId in (cmd.AuthorIds ?? []).Distinct())
            {
                var author = await session.LoadAsync<Author>(authorId, ct);
                if (author is null || author.IsDeleted)
                    return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                    { Property = nameof(cmd.AuthorIds), Code = CatalogResourceConstants.RECORD_NOT_FOUND });
                authors.Add(author);
            }
            foreach (var name in (cmd.NewAuthorNames ?? []).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var normalized = NameNormalization.Normalize(name);
                var author = authors.FirstOrDefault(a => a.NormalizedName == normalized)
                             ?? await session.Query<Author>().FirstOrDefaultAsync(a => a.NormalizedName == normalized, ct);
                if (author is null)
                {
                    author = Author.Create(name).Data!;
                    session.Store(author);
                }
                if (authors.All(a => a.Id != author.Id))
                    authors.Add(author);
            }
            if (authors.Count == 0)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.AuthorIds), Code = CatalogResourceConstants.PRODUCT_AUTHOR_REQUIRED });

            // Yayınevi: PublisherId YA DA NewPublisherName (get-or-create); zorunlu.
            Publisher? publisher = null;
            if (!string.IsNullOrWhiteSpace(cmd.NewPublisherName))
            {
                var normalized = NameNormalization.Normalize(cmd.NewPublisherName);
                publisher = await session.Query<Publisher>().FirstOrDefaultAsync(p => p.NormalizedName == normalized, ct);
                if (publisher is null)
                {
                    publisher = Publisher.Create(cmd.NewPublisherName).Data!;
                    session.Store(publisher);
                }
            }
            else if (cmd.PublisherId is { } pubId)
            {
                publisher = await session.LoadAsync<Publisher>(pubId, ct);
            }
            if (publisher is null || publisher.IsDeleted)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.PublisherId), Code = CatalogResourceConstants.PRODUCT_PUBLISHER_REQUIRED });

            // Kategori: zorunlu, var olmalı.
            if (cmd.CategoryId is not { } categoryId)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.CategoryId), Code = CatalogResourceConstants.PRODUCT_CATEGORY_REQUIRED });
            var category = await session.LoadAsync<Category>(categoryId, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.CategoryId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            // Oluştur — SKU=ISBN (ImportBook emsali). Draft doğar (Publish YOK); event yayılmaz.
            var product = Product.Create(cmd.Name, cmd.Isbn, ProductType.Simple, price,
                cmd.ShortDescription ?? string.Empty, cmd.FullDescription ?? string.Empty);
            product.SetIdentifiers(cmd.Isbn, gtin: cmd.Isbn, manufacturerPartNumber: null);

            var setAuthors = product.SetAuthors(authors.Select(a => a.Id));
            if (!setAuthors.IsSuccess)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(setAuthors.Messages);
            var setPublisher = product.SetPublisher(publisher.Id);
            if (!setPublisher.IsSuccess)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(setPublisher.Messages);
            product.SetImage(cmd.ImageUrl);
            var assign = product.AssignToCategory(categoryId, isFeatured: false, displayOrder: 0);
            if (!assign.IsSuccess)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(assign.Messages);

            session.Store(product);

            // 058 FR-013: fiyat>0 ise geçmişin ilk satırı (OldPrice=null); fiyatsız taslak satır düşürmez.
            if (price.Amount > 0)
                session.Store(ProductPriceChange.Create(product.Id, oldPrice: null, price.Amount, DateTime.UtcNow));

            // Stock BarcodeLink+OnHand satırını bu event'ten kurar (InitialStock=0 — admin ayrıca
            // admin_set_stock/admin_adjust_stock ile gerçek adedi girer; ImportBook emsali).
            await bus.PublishAsync(new IntegrationEvents.ProductAdded(cmd.Isbn, product.Id, InitialStock: 0));

            return FeatureObjectResultModel<AdminCreateProductResponse>.Ok(new AdminCreateProductResponse
            {
                ProductId = product.Id,
                Isbn = cmd.Isbn,
                IsPublished = product.Published,
            });
        }
    }
}

// 074: parite yazma tool'ları — REST admin (Create/SetDimensions/SetSeo/Tag) söküldü, MCP-only.
// TEK /mcp'de, scope-budamalı (085). userId token'dan; scope handler'da.

[McpServerToolType]
public static class AdminCreateProductMcpTool
{
    [McpServerTool(Name = Shared.CatalogAdminTools.CreateProduct)]
    [Description(Shared.McpToolDescriptions.CatalogAdminTools.CreateProduct)]
    public static Task<FeatureObjectResultModel<AdminCreateProduct.AdminCreateProductResponse>> AdminCreateProductAsync(
        [Description("Kitap adi")] string name,
        [Description("ISBN (kimlik; benzersiz)")] string isbn,
        [Description("Fiyat (TL, >= 0)")] decimal price,
        [Description("Kategori kimligi (list_categories'ten)")] Guid categoryId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("Kisa aciklama")] string? shortDescription = null,
        [Description("Tam aciklama")] string? fullDescription = null,
        [Description("Var olan yazar kimlikleri")] List<Guid>? authorIds = null,
        [Description("Katalogda olmayan yeni yazar adlari (olusturulur)")] List<string>? newAuthorNames = null,
        [Description("Var olan yayinevi kimligi")] Guid? publisherId = null,
        [Description("Katalogda olmayan yeni yayinevi adi (olusturulur)")] string? newPublisherName = null,
        [Description("Kapak gorseli URL")] string? imageUrl = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminCreateProduct.AdminCreateProductResponse>>(
            new AdminCreateProduct.AdminCreateProductCommand(
                userId, name, isbn, price, shortDescription, fullDescription,
                authorIds, newAuthorNames, publisherId, newPublisherName, categoryId, imageUrl), ct);
    }
}
