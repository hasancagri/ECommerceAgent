namespace Catalog.Api.Tests.Authors;

// Kitapyurdu: Author.SetBio — boş input no-op, dolu yazar, ikinci çağrı overwrite ETMEZ (ilk-dolu kazanır).
public class AuthorBioTests
{
    private static Author NewAuthor() => Author.Create("Enis Batur").Data!;

    [Fact]
    public void SetBio_WithText_SetsBio()
    {
        var author = NewAuthor();

        author.SetBio("1952 yılında Eskişehir'de doğdu.").IsSuccess.ShouldBeTrue();

        author.Bio.ShouldBe("1952 yılında Eskişehir'de doğdu.");
    }

    [Fact]
    public void SetBio_Blank_LeavesBioNull()
    {
        var author = NewAuthor();

        author.SetBio("   ").IsSuccess.ShouldBeTrue();

        author.Bio.ShouldBeNull();
    }

    [Fact]
    public void SetBio_WhenAlreadyFilled_DoesNotOverwrite()
    {
        var author = NewAuthor();
        author.SetBio("İlk biyografi");

        author.SetBio("İkinci biyografi");

        author.Bio.ShouldBe("İlk biyografi");
    }

    [Fact]
    public void SetBio_BlankAfterFilled_KeepsExisting()
    {
        var author = NewAuthor();
        author.SetBio("Dolu biyografi");

        author.SetBio(null);

        author.Bio.ShouldBe("Dolu biyografi");
    }
}
