namespace ScoutAgent.Api.Agents;

// 019 (B): MCP tool sonuçlarının TOOL-BAŞINA ayna-tipleri. ScoutAgent BC tipine erişemez (İlke I
// izolasyon) → şekli KOPYALAR (bilinçli tekrar; sözleşme = AD). Tip tool başına. Sub-agent'ın tool
// wrapper'ı MCP structuredContent'i `Deserialize<SomutTip>` eder (generic, reflection YOK) → reply.items
// tipli (sniff + LLM uydurması YOK, FR-005 korunur). React tool adına göre render eder.

// catalog (018: get_book SÖKÜLDÜ — tekil kitap da search_books; derin künye React detay sayfasında)
public sealed record SearchBooksResult(IReadOnlyList<SearchBookItem> Items, long Total, int Page);
public sealed record SearchBookItem(
    Guid Id, string Title, string Authors, decimal? Price, bool IsSalable,
    string? CoverUrl, string? Publisher, string? Isbn);

// reviews
public sealed record GetBookReviewsResult(
    IReadOnlyList<ReviewItem> Items, long Total, double Average, int Count, int Page);
public sealed record ReviewItem(int Rating, string? Text, DateTimeOffset CreatedAt, string Author);

// basket (tekil-aggregate: tek sepet, içinde satırlar)
public sealed record GetBasketResult(IReadOnlyList<BasketLineItem> Items, int TotalQuantity, decimal TotalPrice);
public sealed record BasketLineItem(Guid ProductId, string Name, string? ImageUrl, decimal Price, int Quantity);

// order
public sealed record GetMyOrdersResult(IReadOnlyList<OrderItem> Orders);
public sealed record OrderItem(string OrderNo, string Status, DateTime PlacedAt, decimal Total, int LineCount);

// library
public sealed record GetMyLibraryResult(IReadOnlyList<LibraryItem> Items, int Total);
public sealed record LibraryItem(Guid ProductId);

// customer
public sealed record GetMyAddressesResult(IReadOnlyList<AddressItem> Addresses);
public sealed record AddressItem(
    string Province, string District, string Street, string Line, string ZipCode, bool IsDefault);

// personalization
public sealed record GetSearchHistoryResult(
    IReadOnlyList<SearchHistoryItem> Data, int Page, int PageSize, int TotalCount);
public sealed record SearchHistoryItem(string Query, string Mode, DateTimeOffset LastSearchedAt, int Count);
