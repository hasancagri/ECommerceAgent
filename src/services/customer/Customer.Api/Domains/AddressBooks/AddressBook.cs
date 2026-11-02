namespace Customer.Api.Domains.AddressBooks;

// Bir kullanicinin adres defteri. UserId ile keyli (kullanici basina tek defter).
// SavedAddress koleksiyonu + "en fazla 1 varsayilan" invariant'i aggregate icinde korunur.
public class AddressBook : AggregateRoot
{
    private AddressBook() { }

    /// <summary>Verilen kullanici icin bos bir adres defteri olusturur.</summary>
    public static AddressBook Create(Guid userId) => new() { UserId = userId };

    public Guid UserId { get; private set; }

    [JsonProperty("Addresses")] private List<SavedAddress> _addresses = new();

    /// <summary>Kayitli adresleri salt-okunur liste olarak dondurur.</summary>
    [JsonIgnore] public IReadOnlyList<SavedAddress> Addresses => _addresses.AsReadOnly();

    /// <summary>Yeni bir kayitli adres olusturup deftere ekler ve dondurur.</summary>
    public ResultDomain<SavedAddress> AddAddress(Address value)
    {
        var address = SavedAddress.Create(value);
        _addresses.Add(address);
        return ResultDomain<SavedAddress>.Ok(address);
    }

    /// <summary>Id ile bulunan adresi gunceller; yoksa NotFound doner.</summary>
    public ResultDomain UpdateAddress(Guid addressId, Address value)
    {
        var address = _addresses.FirstOrDefault(x => x.Id == addressId);
        if (address is null)
            return ResultDomain.Error(new MessageItem { Code = CustomerResourceConstants.RECORD_NOT_FOUND });
        address.Update(value);
        return ResultDomain.Ok();
    }

    /// <summary>Id ile bulunan adresi defterden siler; yoksa NotFound doner.</summary>
    public ResultDomain RemoveAddress(Guid addressId)
    {
        var address = _addresses.FirstOrDefault(x => x.Id == addressId);
        if (address is null)
            return ResultDomain.Error(new MessageItem { Code = CustomerResourceConstants.RECORD_NOT_FOUND });
        _addresses.Remove(address);
        return ResultDomain.Ok();
    }

    /// <summary>Hedef adresi varsayilan yapar, digerlerini false ceker (≤1 varsayilan invariant).</summary>
    // ≤1 varsayilan invariant: hedef bulunur, digerleri false, hedef true (tek yazma — atomik).
    public ResultDomain SetDefaultAddress(Guid addressId)
    {
        var target = _addresses.FirstOrDefault(x => x.Id == addressId);
        if (target is null)
            return ResultDomain.Error(new MessageItem { Code = CustomerResourceConstants.RECORD_NOT_FOUND });

        foreach (var address in _addresses)
            address.SetDefault(address.Id == addressId);

        return ResultDomain.Ok();
    }
}