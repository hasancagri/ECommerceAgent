namespace FileApi.Domains.FileAsset.Features.Queries;

// US2: batch ImageName → URL, fileDb'den LOKAL çözülür (dış-depoya çağrı YOK, SC-001). Tek DB
// sorgusu (WHERE ImageName IN, SC-002). Her biri için tercih edilen konum (PreferredLocation) + config-base
// (CoverUrlResolver). Kayıtsız ImageName → {url:null, exists:false} (hata değil).
public static class ResolveUrls
{
    public record ResolveUrlsQuery(IReadOnlyList<string> ImageNames);

    public record ResolvedUrl(string ImageName, string? Url, bool Exists);

    public class ResolveUrlsResult
    {
        public List<ResolvedUrl> Results { get; set; } = new();
    }

    public class ResolveUrlsQueryHandler(IQuerySession session, CoverUrlResolver resolver)
    {
        public async Task<FeatureObjectResultModel<ResolveUrlsResult>> Handle(ResolveUrlsQuery query, CancellationToken ct)
        {
            var names = query.ImageNames?.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList() ?? new();
            if (names.Count == 0)
                return FeatureObjectResultModel<ResolveUrlsResult>.Ok(new ResolveUrlsResult());

            // Tek sorgu: eşleşen kayıtlar.
            var assets = await session.Query<FileAsset>().Where(a => names.Contains(a.ImageName)).ToListAsync(ct);
            var byName = assets.ToDictionary(a => a.ImageName);

            var results = new List<ResolvedUrl>(names.Count);
            foreach (var name in names)
            {
                if (!byName.TryGetValue(name, out var asset))
                {
                    results.Add(new ResolvedUrl(name, null, false));
                    continue;
                }
                var loc = asset.PreferredLocation(resolver.DefaultStorageType);
                var url = resolver.Resolve(loc.StorageType, loc.StorageFilePath);
                results.Add(new ResolvedUrl(name, url, true));
            }

            return FeatureObjectResultModel<ResolveUrlsResult>.Ok(new ResolveUrlsResult { Results = results });
        }
    }
}