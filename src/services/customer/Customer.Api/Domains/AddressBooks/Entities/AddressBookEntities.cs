namespace Customer.Api.Domains.AddressBooks.Entities;

// AddressBook icinde sade entity (base almaz — BasketItem deseni). Kimligi (Id) var, bagimsiz yasamaz.
public class SavedAddress
{
    private SavedAddress() { }

    private SavedAddress(Guid id, Address value)
    {
        Id = id;
        Value = value;
    }

    public Guid Id { get; private set; }
    public Address Value { get; private set; } = default!;
    public bool IsDefault { get; private set; }

    public static SavedAddress Create(Address value) => new(Guid.NewGuid(), value);

    public void Update(Address value) => Value = value;

    public void SetDefault(bool isDefault) => IsDefault = isDefault;
}