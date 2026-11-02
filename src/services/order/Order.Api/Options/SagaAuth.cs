namespace Order.Api.Options;

// 028/077: Order.Api arka plan makine token'ı (order-saga client_credentials) — section "SagaAuth".
// SagaTokenHandler buradan tip'li okur (magic-string config[...] yerine). Hosted-CF: sepet gRPC
// (basket.read) + varsayılan adres (customer.read) + Payment link isteği (payment.write).
public class SagaAuth
{
    [Required] public string ClientId { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
}
