namespace Storefront.Api;

public static class CatalogConsumers
{
    public static async Task Handle(
        IntegrationEvents.ProductChangedEvent evt,
        IDocumentSession session,
        CacheInvalidator cacheInvalidator,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        // yeniden-embedding karari ApplyCatalog ONCE alinir (eski aciklama heniz ezilmemisken).
        // hasEmbedding yalniz "aciklama degismedi" dalinda anlamli — yalniz o durumda PK lookup yapilir.
        var hasEmbedding = string.Equals(evt.Description, view.Description, StringComparison.Ordinal)
                           && !string.IsNullOrWhiteSpace(evt.Description)
                           && await session.LoadAsync<ProductDescriptionEmbedding>(evt.ProductId, ct) is not null;
        var embeddingDecision = StorefrontView.DecideEmbedding(evt.Description, view.Description, hasEmbedding);

        view.ApplyCatalog(evt.Name, evt.Description, evt.Price,
            // event yazar çiftlerini read-model'in kendi AuthorRef'ine çevir (Shared tipini saklamaz).
            evt.Authors.Select(a => new Domains.StorefrontView.AuthorRef(a.Id, a.Name)).ToList(),
            evt.PublisherId, evt.Publisher, evt.CategoryId, evt.Category, evt.ImageUrl, evt.IsDeleted,
            // kanonik spec adlari satira denormalize edilir (facet + filtre + detay).
            (evt.Specs ?? []).Select(s => SpecPair.Create(s.Attribute, s.Option)).ToList(),
            // varyant ailesi kodu (null = ailesiz).
            evt.FamilyCode);

        // anlamsal temsil — yalniz aciklama degisince uretilir (fiyat/stok guncellemesi API'ye gitmez);
        // ayri dokumana, AYNI transaction'da yazilir. IsDeleted uretimi ETKILEMEZ (gorunurluk sorgu
        // tarafinda, FR-007). Hata → exception → Wolverine retry/error queue (bilincli kabul, research R4).
        switch (embeddingDecision)
        {
            case EmbeddingDecision.Clear:
                session.Delete<ProductDescriptionEmbedding>(evt.ProductId);
                break;
            case EmbeddingDecision.Generate:
                var vector = await embeddingGenerator.GenerateVectorAsync(evt.Description, cancellationToken: ct);
                session.Store(ProductDescriptionEmbedding.Create(evt.ProductId, vector.ToArray()));
                break;
        }

        session.Store(view);
        await session.SaveChangesAsync(ct);

        // Projeksiyon-BC invalidation kuralı (CLAUDE.md): satırı yazan handler kendi cache'ini
        // boşaltır — CacheInvalidator üzerinden (yerel + backplane). Facet verisini yalnız Catalog
        // kaynaklı alanlar etkiler; StockChangedEvent facet'e girmez, orada boşaltma yok.
        await cacheInvalidator.InvalidateAsync("filters", ct);
    }
}
