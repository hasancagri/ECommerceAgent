# Data Model: EC pay-by-link rewire

Mevcut aggregate'leri yeniden kullanır; yeni tablo/BC yok. Alan eklemeleri additive.

## PaymentIntent (Payment BC — MEVCUT, pekiştirilir)

Bir ödemenin durum makinesi. Kart verisi TUTMAZ (İlke VIII).

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | aggregate kimliği |
| OrderRef | string | = OrderId (insan korelasyonu) |
| Amount | decimal | beklenen tutar (anti-tamper guard kaynağı) |
| Currency | string | "TRY" |
| PgPaymentRef | string? | PG opak referansı — link üretilince yazılır (webhook eşleme anahtarı) |
| Status | PaymentIntentStatus | Pending → Succeeded \| Failed \| Expired |

**Durum geçişleri (aggregate metotları, İlke II):**

- `Create(orderRef, amount, currency)` → **Pending** (amount > 0 invariant'ı; değilse ResultDomain error).
- `AttachPgRef(pgPaymentRef)` → Pending'de PgPaymentRef yazar (link üretildikten sonra; bir kez).
- `MarkSucceeded()` → Pending ise **Succeeded**; zaten Succeeded ise **no-op Ok** (idempotent); Failed/
  Expired'den Succeeded YASAK (ResultDomain error). Webhook retry güvenli.
- `MarkFailed(reasonCode?)` → Pending ise **Failed**; zaten Failed ise no-op Ok (idempotent); Succeeded'den
  Failed YASAK.
- `Expire()` → mevcut (terk/TTL; bu dilimde tetikleyici eklenmez, aggregate korunur).

**Marten saklama (TUZAK):** PaymentIntent + VO'ları private parametresiz ctor + private setter
(`AllowNonPublicDefaultConstructor`); get-only record VO saklanırsa okuma 500 — bkz. memory
marten-record-vo-deserialize-ctor-gotcha. Yeni alan (PgPaymentRef) additive, default null.

## Order (Order BC — MEVCUT, dokunulmaz davranış)

start_payment yalnız Pending yaratır; onay/iptal 092 koreografisinden (değişmez).

| Durum | Tetik |
|---|---|
| Pending | start_payment (ödeme başlar) |
| Confirmed | StockCommitted → Order.Confirm (092) |
| Cancelled | PaymentFailed / StockCommitFailed → Order.Cancel (092) |

- **Boş sepet / tutar≤0 / PG-hata / STORE_NOT_LIVE**: Order Pending **yaratılmaz ya da commit edilmez**
  (kalıcı kirlilik yok — anti-hedef). Karar: start_payment önce tutarı+PG-link'i başarıyla alır, SONRA
  Order+Intent commit eder (tek `[Transactional]` sınırında) → başarısız yolda hiç kalıcı kayıt kalmaz.
  (Alternatif: Pending yaratıp Cancel — reddedildi; yarım-order gürültüsü üretir.)

## İlişkiler

```
Order (orderDb)  --OrderRef(=OrderId)-->  PaymentIntent (paymentDb)
PaymentIntent  --PgPaymentRef-->  PG PaymentLink (PG, dış)
PaymentSucceeded/Failed (integration event, fanout)  -->  092 koreografi (Order/Stock)
```

Not: Order↔PaymentIntent FK yok (ayrı BC/DB); bağ OrderRef string korelasyonu + S2S gRPC.
