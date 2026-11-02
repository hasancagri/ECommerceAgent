namespace FileApi.Migration;

public enum CoverMigrationDecision
{
    Skip,
    Download
}

// Saf: bir migration satırı indirilsin mi atlansın mı (İLKE VI test-first).
// Var → atla (idempotent); imageUrl boş → atla (kaynak yok); aksi → indir.
public static class MigrationDecision
{
    public static CoverMigrationDecision Decide(bool exists, string? imageUrl)
    {
        if (exists) return CoverMigrationDecision.Skip;
        if (string.IsNullOrWhiteSpace(imageUrl)) return CoverMigrationDecision.Skip;
        return CoverMigrationDecision.Download;
    }
}
