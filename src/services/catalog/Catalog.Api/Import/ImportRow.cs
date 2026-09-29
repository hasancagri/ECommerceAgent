namespace Catalog.Api.Import;

// US1: Excel'den okunan ham katalog kaydı + işleme durumu. Import makinesinin geçici defteri
// (aggregate DEĞİL; Domains/ dışı — conventions read-model/seeder muafiyeti). ISBN = idempotency
// anahtarı; processor bekleyen satırları ürüne çevirir (exactly-once). Not kullanılan xlsx kolonları:
// imageUrl (kapak async File.Api'den) + discount (v1 dışı) — staging'e alınmaz.
public enum ImportRowStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2
}

public class ImportRow : AggregateRoot
{
    private ImportRow()
    {
    }

    public Guid ImportSessionId { get; private set; }
    public string Isbn { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Authors { get; private set; } = string.Empty;      // `;` ayraçlı çoklu-yazar
    public string Publisher { get; private set; } = string.Empty;
    public decimal PriceTry { get; private set; }
    public int Stock { get; private set; }
    public string CategoryMid { get; private set; } = string.Empty;
    public string CategoryLeaf { get; private set; } = string.Empty;
    public string Tags { get; private set; } = string.Empty;         // `;` ayraçlı
    public string Specs { get; private set; } = string.Empty;        // `k=v; k=v`
    public string Description { get; private set; } = string.Empty;
    public string? FamilyCode { get; private set; }

    public ImportRowStatus Status { get; private set; } = ImportRowStatus.Pending;
    public string? Error { get; private set; }

    // İşlenince dolar; publish_imported ürünü Gtin sorgusu OLMADAN bu Id ile bulur.
    public Guid? ProductId { get; private set; }

    /// <summary>Excel satırından bekleyen (Pending) staging kaydı üretir. ISBN zorunlu (idempotency anahtarı).</summary>
    public static ResultDomain<ImportRow> Create(
        Guid importSessionId, string isbn, string title, string authors, string publisher,
        decimal priceTry, int stock, string categoryMid, string categoryLeaf,
        string tags, string specs, string description, string? familyCode)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            return ResultDomain<ImportRow>.Error(new MessageItem
            { Property = nameof(Isbn), Code = CatalogResourceConstants.IMPORT_ISBN_REQUIRED });

        return ResultDomain<ImportRow>.Ok(new ImportRow
        {
            ImportSessionId = importSessionId,
            Isbn = isbn.Trim(),
            Title = title ?? string.Empty,
            Authors = authors ?? string.Empty,
            Publisher = publisher ?? string.Empty,
            PriceTry = priceTry,
            Stock = stock,
            CategoryMid = categoryMid ?? string.Empty,
            CategoryLeaf = categoryLeaf ?? string.Empty,
            Tags = tags ?? string.Empty,
            Specs = specs ?? string.Empty,
            Description = description ?? string.Empty,
            FamilyCode = string.IsNullOrWhiteSpace(familyCode) ? null : familyCode.Trim()
        });
    }

    /// <summary>Zorunlu alanı (ISBN) eksik satırı doğrudan Failed staging kaydı olarak üretir (FR-010 raporlama;
    /// işlenemez ama sebebi görünür). ISBN boş kalabilir — bu satırın kimliği kendi Guid Id'si.</summary>
    public static ImportRow CreateFailed(Guid importSessionId, string? rawIsbn, string error) =>
        new()
        {
            ImportSessionId = importSessionId,
            Isbn = rawIsbn?.Trim() ?? string.Empty,
            Status = ImportRowStatus.Failed,
            Error = error
        };

    /// <summary>Satırı işlendi (terminal) işaretler; oluşan/atlanmış ürünün Id'sini tutar. Terminal satırı yeniden işaretlemez.</summary>
    public ResultDomain MarkProcessed(Guid productId)
    {
        if (Status != ImportRowStatus.Pending)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_ROW_ALREADY_TERMINAL });

        Status = ImportRowStatus.Processed;
        ProductId = productId;
        Error = null;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Satırı başarısız (terminal) işaretler + sebep. Terminal satırı yeniden işaretlemez.</summary>
    public ResultDomain MarkFailed(string error)
    {
        if (Status != ImportRowStatus.Pending)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Status), Code = CatalogResourceConstants.IMPORT_ROW_ALREADY_TERMINAL });

        Status = ImportRowStatus.Failed;
        Error = error;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}
