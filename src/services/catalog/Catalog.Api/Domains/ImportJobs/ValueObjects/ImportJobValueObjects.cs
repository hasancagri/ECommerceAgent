namespace Catalog.Api.Domains.ImportJobs.ValueObjects;

// ImportJob'un VO'ları — aggregate başına TEK dosya (konvansiyon).

/// <summary>Satır hatası kaydı: hangi sayfa (yayınevi), hangi satır, neden. İş durmaz —
/// hatalar birikir, sonuç raporunda sebepli listelenir (FR-005/007).</summary>
public record ImportError
{
    public string Sheet { get; private init; } = default!;
    public int Row { get; private init; }
    public string Reason { get; private init; } = default!;

    private ImportError() { }

    public ImportError(string sheet, int row, string reason)
    {
        Sheet = sheet;
        Row = row;
        Reason = reason;
    }
}
