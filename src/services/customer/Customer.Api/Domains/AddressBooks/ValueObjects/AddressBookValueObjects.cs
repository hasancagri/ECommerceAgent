namespace Customer.Api.Domains.AddressBooks.ValueObjects;

// Customer BC'ye ozel Address VO. Order BC'nin Address'inden AYRI tip (BC izolasyonu — sizdirma yok).
// record + private ctor + statik Create fabrikasi; zorunlu alan bosSa Error (FR-002).
public record Address
{
    // Marten Newtonsoft nonpublic ctor/setter destegi icin.
    private Address() { }

    private Address(string province, string district, string street, string zipCode, string line)
    {
        Province = province;
        District = district;
        Street = street;
        ZipCode = zipCode;
        Line = line;
    }

    public string Province { get; private set; } = default!;
    public string District { get; private set; } = default!;
    public string Street { get; private set; } = default!;
    public string ZipCode { get; private set; } = default!;
    public string Line { get; private set; } = default!;

    public static ResultDomain<Address> Create(
        string province, string district, string street, string zipCode, string line)
    {
        if (string.IsNullOrWhiteSpace(province)
            || string.IsNullOrWhiteSpace(district)
            || string.IsNullOrWhiteSpace(street)
            || string.IsNullOrWhiteSpace(zipCode)
            || string.IsNullOrWhiteSpace(line))
            return ResultDomain<Address>.Error(
                new MessageItem { Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

        return ResultDomain<Address>.Ok(new Address(province, district, street, zipCode, line));
    }
}