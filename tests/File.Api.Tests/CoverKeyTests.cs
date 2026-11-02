using FileApi.Storage;
using Shouldly;
using Xunit;

namespace File.Api.Tests;

public class CoverKeyTests
{
    [Theory]
    [InlineData("9783161484100")]        // ISBN13 salt rakam
    [InlineData("080442957X")]           // ISBN10 sonu X
    [InlineData("978-3-16-148410-0")]    // tireli
    public void ValidIsbn_ProducesSafeKey(string isbn)
    {
        CoverKey.TryCreate(isbn, out var key).ShouldBeTrue();
        key.ShouldBe(isbn);
    }

    [Theory]
    [InlineData("../etc/passwd")]        // traversal
    [InlineData("..")]                    // traversal
    [InlineData("foo/bar")]              // ileri slash
    [InlineData("foo\\bar")]             // ters slash
    [InlineData("123 456")]             // boşluk
    [InlineData(" 123")]                // baştaki boşluk
    [InlineData("")]                     // boş
    [InlineData("  ")]                    // sadece boşluk
    public void UnsafeIsbn_IsRejected(string isbn)
    {
        CoverKey.TryCreate(isbn, out var key).ShouldBeFalse();
        key.ShouldBe(string.Empty);
    }
}
