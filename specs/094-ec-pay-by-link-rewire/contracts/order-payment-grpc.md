# Contract: Order→Payment intent+link (S2S gRPC, BC-arası)

**Yön**: Order.Api (`PaymentIntentClient`) → Payment.Api gRPC servisi
**Auth**: mevcut S2S makine-token `DelegatingHandler` (REST client'lardaki gibi `AddGrpcClient...
AddHttpMessageHandler`)
**Kaynak**: 093'te sökülen "Order S2S intent gRPC" dönüşü (İlke I — BC-arası S2S = gRPC)

## RPC

```proto
rpc CreateIntentAndLink (CreateIntentAndLinkRequest) returns (CreateIntentAndLinkReply);

message CreateIntentAndLinkRequest {
  string order_id      = 1;  // = OrderRef
  string amount        = 2;  // decimal string (kuruş kaybı yok)
  string currency      = 3;  // "TRY"
  string store_name    = 4;
  string order_summary = 5;
  string return_url    = 6;  // {EcBaseUrl}/checkout/done
}

message CreateIntentAndLinkReply {
  bool    is_success    = 1;
  string  hosted_url    = 2;  // Ayşe'ye dönecek PG hosted link (başarıda)
  string  error_code    = 3;  // başarısızlıkta (ör. STORE_NOT_LIVE)
}
```

## Payment tarafı davranışı (mantık gRPC servis class'ında — tek çağıran Order, conventions S2S kuralı)

1. `PaymentIntent.Create(orderId, amount, "TRY")` (amount>0 invariant).
2. `PgPaymentLinkClient` → PG `POST /payment-links` (merchantId = config MerchantId).
3. STORE_NOT_LIVE / PG hata → `is_success=false, error_code` dön; **PaymentIntent commit ETME**.
4. Başarı → `AttachPgRef(pgPaymentRef)`, intent commit (`[Transactional]`), `hosted_url=linkUrl` dön.

## Order tarafı (start_payment)

- `is_success=false` → Order Pending commit edilmez; `error_code` Ayşe'ye anlaşılır mesajla taşınır.
- `is_success=true` → Order Pending + hosted_url `StartPaymentResponse.HostedUrl` ile döner.
