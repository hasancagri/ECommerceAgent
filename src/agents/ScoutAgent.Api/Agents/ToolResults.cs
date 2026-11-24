namespace ScoutAgent.Api.Agents;

// MCP search_books sonucunun ayna-tipi. ScoutAgent BC tipine erişemez (İlke I izolasyon) → şekli
// KOPYALAR (bilinçli tekrar; sözleşme = AD). SearchTyped (022 /search) `Deserialize<SearchBooksResult>`
// eder (generic, reflection YOK) → reply.items tipli (sniff + LLM uydurması YOK). React kart render eder.
// 023: diğer tool ayna-tipleri (similar/reviews/basket/order/library/customer/personalization) /messages
// sub-agent'larıyla SÖKÜLDÜ — sohbet yalnız kitap keşfi (search_books).
public sealed record SearchBooksResult(IReadOnlyList<SearchBookItem> Items, long Total, int Page);
public sealed record SearchBookItem(
    Guid Id, string Title, string Authors, decimal? Price, bool IsSalable,
    string? CoverUrl, string? Publisher, string? Isbn,
    string Genre, IReadOnlyList<string> Moods, bool InStock);

// 024: similar_books ayna-tipi GERİ (benzer akışı iki-faza yeniden kablolandı). Catalog
// SimilarBooks.Response/BookItem şeklini KOPYALAR (İlke I; sözleşme = AD). case-insensitive deserialize.
public sealed record SimilarBooksResult(IReadOnlyList<SimilarBookItem> Items, long Total);
public sealed record SimilarBookItem(
    Guid Id, string Title, string Authors, decimal? Price, bool IsSalable,
    bool InStock, string? CoverUrl, string? Publisher, string? Isbn,
    string Genre, IReadOnlyList<string> Moods);
