# Payment — Domain Süreci (EventStorming)

**BC ne yapar:** Bir sipariş için **hosted ödeme girişimi** (`PaymentIntent`) yürütür: dış PaymentGateway'den
hosted ödeme linki alır, durumu Pending saklar, imzalı callback ya da terk-timeout ile terminal'e taşır.
Kart alanı HİÇ taşımaz; PAN store'a girmez.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🩷 dış sistem ·
> 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟦 **Hosted link iste** ⇦ Order — S2S gRPC; girişim tutar+txRef ile    `(PaymentIntentGrpcService.CreateIntent`
   gelir; re-use: aynı kullanıcı+sepet için canlı Pending varsa            ` → PaymentIntent.Create)`
   mevcut link döner (yeni PG çağrısı yok).
2. 🟦 **MerchantKey'i çöz** ⇨ Customer → 🩷 **PG'ye hosted-payment        `(MerchantKeyClient`
   yolla** — anahtar Customer'dan (tek kaynak); dış PG hosted URL +        ` → PgHostedPaymentClient)`
   referans döner.
3. 🟨 **Girişim Pending doğar** + 🟪 **terk-timer kurulur** — TxRef        `(PaymentIntent.Create;`
   tekil (unique). Timeout dolunca hâlâ Pending ise süresi-doldu.          ` PaymentIntentExpiry → PaymentIntent.Expire)`
4. 🩷 **PG imzalı callback yollar** → 🟪 **imza doğrulanır** —             `(CallbackSignatureValidator)`
   geçersiz/eksik imza → 401, işlem yok.
5. 🟦 **Sonucu işle** → 🟧⭐ **Ödeme alındı** / 🟧 **Ödeme başarısız**     `(HandlePaymentCallback`
   ⇨ Order — girişim terminal'e taşınır (idempotent): başarı →             ` → PaymentIntent.MarkSucceeded/MarkFailed;`
   Succeeded + `PaymentSucceeded`; başarısız/terk → Failed/Expired +       ` → PaymentSucceeded/PaymentFailed)`
   `PaymentFailed`. Aynı transaction'da yayın (durable outbox).
6. 👤 **Kullanıcı ödemelerini okur** — kişi kendi girişimlerini            `(GetAllPaymentsByUserId)`
   listeler; agent için MCP tool'u aynı slice'ı sarar.                     `(GetAllPaymentsByUserIdMcpTool)`

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
