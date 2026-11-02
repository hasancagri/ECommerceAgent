namespace FileApi.Endpoints;

// Kapak servis yüzeyi (anonim public vitrin görseli). GET /files/v1/covers/{isbn}.
public static class CoverEndpoints
{
    public static void MapCoverEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/files/v1/covers/{isbn}", async (string isbn, IFileStore store, CancellationToken ct) =>
        {
            if (!CoverKey.TryCreate(isbn, out var key))
                return Results.BadRequest();          // geçersiz/güvensiz ISBN (traversal)

            var found = await store.TryGetAsync(key, ct);
            if (found is null)
                return Results.NotFound();            // depoda yok (servis çökmez)

            return Results.Stream(found.Value.Content, found.Value.ContentType);
        });
    }
}
