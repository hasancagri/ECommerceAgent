# Payment — Domain Süreci

**BC ne yapar:** Bir sipariş için **hosted ödeme girişimi** (`PaymentIntent`) yürütür: dış PaymentGateway'den
hosted ödeme linki alır, durumu Pending saklar, imzalı callback ya da terk-timeout ile terminal'e taşır.
Kart alanı HİÇ taşımaz; PAN store'a girmez.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Order hosted link ister (S2S gRPC).** Girişim tutar+txRef ile      `(PaymentIntentGrpcService.CreateIntent`
   gelir; re-use: aynı kullanıcı+sepet için canlı Pending varsa         ` → PaymentIntent.Create)`
   mevcut link döner (yeni PG çağrısı yok).
2. **MerchantKey çözülür, PG'ye hosted-payment yollanır.** Anahtar     `(MerchantKeyClient`
   Customer'dan (tek kaynak); PG hosted URL + referans döner.          ` → PgHostedPaymentClient)`
3. **Girişim Pending saklanır + terk-timer kurulur.** TxRef tekil       `(PaymentIntent.Create;`
   (unique). Timeout dolunca hâlâ Pending ise süresi-doldu.             ` PaymentIntentExpiry → PaymentIntent.Expire)`
4. **PG imzalı callback yollar; imza doğrulanır.** Geçersiz/eksik       `(CallbackSignatureValidator)`
   imza → 401, işlem yok.
5. **Sonuç girişimi terminal'e taşır (idempotent).** Başarı →          `(HandlePaymentCallback`
   Succeeded + `PaymentSucceeded`; başarısız/terk → Failed/Expired      ` → PaymentIntent.MarkSucceeded/MarkFailed;`
   + `PaymentFailed`. Aynı transaction'da yayın (durable outbox).       ` → PaymentSucceeded/PaymentFailed)`
6. **Kullanıcı ödemelerini okur.** Kişi kendi girişimlerini            `(GetAllPaymentsByUserId)`
   listeler; agent için MCP tool'u aynı slice'ı sarar.                  `(GetAllPaymentsByUserIdMcpTool)`

## Domain kuralları (süreci yöneten değişmezler)

- **Kart alanı yoktur.** PAN/kart verisi bu BC'ye hiç girmez; yalnız Amount + hosted link referansları.
- **TxRef tekil; çift callback tek sonuç.** Marten unique index + durum guard → idempotent.
- **Callback at-least-once + sıra-bağımsız.** Intent'ten önce gelen callback TxRef'i bulamaz → 404, PG yeniden
  dener; eşzamanlı çift `CreateIntent`'te TxRef unique index ikinciyi eler (canlı Pending re-use'a düşer).
- **Succeeded terminal + geri-alınamaz.** Void/refund yok; Failed/Expired'den Succeeded'e geçilmez.
- **Callback köken doğrulaması HMAC.** CallbackSecret MerchantKey'den AYRI (sızıntı yalıtımı).
- **Zengin aggregate (İLKE II).** `PaymentIntent` `AggregateRoot`'tan türer; geçişler + guard'lar metotlarda.
- **İzole BC (İLKE I).** Kendi `paymentDb`'si; sonuç yalnız fanout event'le (Order tüketir) çıkar.

## Sınır (bu BC'nin dokunmadığı)

Gerçek para hareketi/iyzico wire + iade (dış PaymentGateway'de); sipariş yaşam döngüsü + stok + sepet
(Order/Checkout/Stock/Basket). Payment sipariş oluşturmaz, stok bilmez; yalnız ödeme girişimini yönetir.
