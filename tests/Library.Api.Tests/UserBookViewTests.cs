using Library.Api.Domains.UserBookView;

namespace Library.Api.Tests;

public class UserBookViewTests
{
    [Fact]
    public void KeyFor_SameUserAndProduct_ReturnsStableDeterministicId()
    {
        var u = Guid.NewGuid();
        var p = Guid.NewGuid();

        UserBookView.KeyFor(u, p).ShouldBe($"{u:N}:{p:N}");
        UserBookView.KeyFor(u, p).ShouldBe(UserBookView.KeyFor(u, p)); // dedup çapası
    }

    [Fact]
    public void Create_WithSnapshot_SetsAllFieldsAndKeyedId()
    {
        var u = Guid.NewGuid();
        var p = Guid.NewGuid();
        var at = DateTimeOffset.UnixEpoch;

        var v = UserBookView.Create(u, p, "1984", "George Orwell", null, "9789750719387", at);

        v.Id.ShouldBe(UserBookView.KeyFor(u, p));
        v.UserId.ShouldBe(u);
        v.ProductId.ShouldBe(p);
        v.Title.ShouldBe("1984");
        v.Author.ShouldBe("George Orwell");
        v.CoverUrl.ShouldBeNull();
        v.Isbn.ShouldBe("9789750719387");
        v.ViewedAt.ShouldBe(at);
    }
}
