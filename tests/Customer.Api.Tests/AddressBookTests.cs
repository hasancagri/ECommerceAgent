namespace Customer.Api.Tests;

public class AddressBookTests
{
    private static Address ValidAddress(string line = "No 12 D3") =>
        Address.Create("Istanbul", "Kadikoy", "Bagdat Cad.", "34710", line).Data!;

    [Fact]
    public void Create_SetsUserId_AndEmptyBook()
    {
        var userId = Guid.NewGuid();

        var book = AddressBook.Create(userId);

        book.UserId.ShouldBe(userId);
        book.Addresses.ShouldBeEmpty();
    }

    [Fact]
    public void AddAddress_AddsToBook()
    {
        var book = AddressBook.Create(Guid.NewGuid());

        var saved = book.AddAddress(ValidAddress()).Data!;

        book.Addresses.Count.ShouldBe(1);
        book.Addresses[0].Id.ShouldBe(saved.Id);
        saved.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void Create_EmptyRequiredField_ReturnsError()
    {
        var result = Address.Create("Istanbul", "", "Bagdat Cad.", "34710", "No 12");

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CustomerResourceConstants.VALUE_IS_REQUIRED);
    }

    [Fact]
    public void SetDefaultAddress_KeepsAtMostOneDefault()
    {
        var book = AddressBook.Create(Guid.NewGuid());
        var a1 = book.AddAddress(ValidAddress("A1")).Data!;
        var a2 = book.AddAddress(ValidAddress("A2")).Data!;

        book.SetDefaultAddress(a1.Id).IsSuccess.ShouldBeTrue();
        book.Addresses.Count(x => x.IsDefault).ShouldBe(1);
        book.Addresses.Single(x => x.IsDefault).Id.ShouldBe(a1.Id);

        // Yeni varsayilan oncekini dusurur.
        book.SetDefaultAddress(a2.Id).IsSuccess.ShouldBeTrue();
        book.Addresses.Count(x => x.IsDefault).ShouldBe(1);
        book.Addresses.Single(x => x.IsDefault).Id.ShouldBe(a2.Id);
    }

    [Fact]
    public void SetDefaultAddress_NotFound_WhenMissing()
    {
        var book = AddressBook.Create(Guid.NewGuid());

        book.SetDefaultAddress(Guid.NewGuid()).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void UpdateAddress_ChangesRecord_ButCapturedSnapshotStaysFrozen()
    {
        var book = AddressBook.Create(Guid.NewGuid());
        var saved = book.AddAddress(ValidAddress("Old")).Data!;
        // Checkout snapshot benzeri: o anki VO referansi kopyalanir/tutulur.
        var snapshot = saved.Value;

        book.UpdateAddress(saved.Id, ValidAddress("New")).IsSuccess.ShouldBeTrue();

        book.Addresses[0].Value.Line.ShouldBe("New");
        // record immutable: tutulan eski deger degismez (dondurma garantisi, SC-004).
        snapshot.Line.ShouldBe("Old");
    }

    [Fact]
    public void RemoveAddress_RemovesFromBook()
    {
        var book = AddressBook.Create(Guid.NewGuid());
        var saved = book.AddAddress(ValidAddress()).Data!;

        book.RemoveAddress(saved.Id).IsSuccess.ShouldBeTrue();
        book.Addresses.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveAddress_NotFound_WhenMissing()
    {
        var book = AddressBook.Create(Guid.NewGuid());

        book.RemoveAddress(Guid.NewGuid()).IsSuccess.ShouldBeFalse();
    }
}