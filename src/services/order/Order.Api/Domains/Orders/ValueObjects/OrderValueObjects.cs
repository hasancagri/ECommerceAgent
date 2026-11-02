namespace Order.Api.Domains.Orders.ValueObjects;

public record Address(string Province, string District, string Street, string ZipCode, string Line);