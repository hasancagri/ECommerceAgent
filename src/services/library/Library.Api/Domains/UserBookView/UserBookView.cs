namespace Library.Api.Domains.UserBookView;

// Son Gezdiğim Kitaplar (2026-11-26): login kullanıcının gezdiği kitap — kişisel read-model
// (AggregateRoot DEĞİL; UserPurchase deseni). Id = "{userId:N}:{productId:N}" → idempotent upsert:
// aynı kitabı tekrar gezme aynı satırı günceller (dedup çapası). Künye ANLIK SNAPSHOT (yaklaşım A) —
// detay sayfası gönderir, BC-arası çağrı yok; bayatlık zararsız (tarihsel görüntü). Embedding/taste YOK.
public class UserBookView
{
    public string Id { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Author { get; private set; } = null!;
    public string? CoverUrl { get; private set; }
    public string Isbn { get; private set; } = null!;
    public DateTimeOffset ViewedAt { get; private set; }

    private UserBookView()
    {
    }

    public static string KeyFor(Guid userId, Guid productId) => $"{userId:N}:{productId:N}";

    public static UserBookView Create(
        Guid userId, Guid productId, string title, string author, string? coverUrl, string isbn, DateTimeOffset viewedAt) => new()
    {
        Id = KeyFor(userId, productId),
        UserId = userId,
        ProductId = productId,
        Title = title,
        Author = author,
        CoverUrl = coverUrl,
        Isbn = isbn,
        ViewedAt = viewedAt,
    };
}
