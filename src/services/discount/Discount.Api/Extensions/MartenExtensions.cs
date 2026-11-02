namespace Discount.Api.Extensions;

// Discount kalıcılık kurulumu: Marten + aggregate şemaları/index'leri + Wolverine entegrasyonu.
public static class MartenExtensions
{
    public static WebApplicationBuilder AddDiscountMarten(this WebApplicationBuilder builder)
    {
        var discountDb = builder.Configuration.GetConnectionString("discountDb")!;
        builder.Services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = SchemaConstants.DiscountSchemaName;
                opts.Connection(discountDb);
                opts.UseNewtonsoftForSerialization(
                    nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
                    configure: s => s.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor);

                // Yürürlük taraması ürün bazında okuyacak (etkin oran çözümü — süreç sonraki dilim).
                opts.Schema.For<Campaign>().Index(x => x.ProductId);
            })
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup();

        return builder;
    }
}
