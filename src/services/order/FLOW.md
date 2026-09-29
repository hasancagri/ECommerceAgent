# Order — Domain Süreci

**BC ne yapar:** Adresi + sepet kalemlerini bir **siparişe** bağlar; siparişi Pending doğurur, ödeme
başarılı olunca checkout orkestratörünü tetikler ve satın-alma kanıtını yayar. **077 hosted-CF:** "ödeme
yap" → `start_payment` sepeti okur, siparişi Pending oluşturur, Payment'tan hosted link ister; ödeme
başarılı callback'i (Payment fanout) siparişi checkout'a sokar. Stok/telafi/sepet temizliği saga'nın işi.

> Domain-önce anlatı. Sağdaki `(…)` = koda köprü. Süreç değişince güncellenir; guard rename'i yakalar.

## Süreç

1. **Kullanıcı ödeme başlatır.** Sepet (gRPC, sunucu-otoritesi) +       `(StartPayment →`
   varsayılan adres okunur; ödenecek tutar sepet kalemlerine O AN         ` Order.Create;`
   aktif indirim yüzdeleri uygulanarak bulunur (canlı, vitrin             ` DiscountClient; PaymentIntentClient)`
   snapshot'ına güvenmez; erişilemez→liste fiyatı); sipariş Pending
   doğar; Payment'tan hosted link istenir + kullanıcıya döner. Re-use:
   canlı link varsa yeni sipariş yok. Boş sepet → dostça mesaj (sipariş yok).
2. **Ödeme başarılı → checkout tetiklenir.** Payment `PaymentSucceeded` `(PaymentConsumers →`
   fanout'unu tüketir; Pending siparişten `StartCheckout` yayınlar        ` StartCheckout)`
   (CommitStock→Confirm→ClearBasket; charge YOK, ödeme öncedendir).
3. **Ödeme başarısız/terk → sipariş iptal.** `PaymentFailed` →          `(PaymentConsumers →`
   `Order.Cancel` (stok hiç düşmedi; saga'ya girmez).                     ` Order.Cancel)`
4. **Checkout orkestratörü siparişi onaylar/iptal eder.** Order          `(CheckoutConsumers →`
   `Confirm/Cancel`'ı tüketir; Confirm pivotunda `OrderCompleted`         ` OrderConfirmed/OrderCompleted)`
   fanout'u yayılır (Reviews/Storefront). Idempotent.
5. **Kullanıcı siparişlerini okur.** Kişi kendi geçmişini listeler.     `(GetOrders)`

## Domain kuralları (süreci yöneten değişmezler)

- **Durum geçişi aggregate'te korunur.** Yalnız `Pending→Confirmed` / `Pending→Cancelled`.
- **Sipariş kullanıcının adresine gider.** Adres varsayılan adres defterinden (FR-001b); ödeme sahibi değiştirmez.
- **Ödeme öncedendir (hosted-CF).** Checkout charge çekmez; StartCheckout OrderId dolu gelir (`CheckoutId=OrderId`, idempotent).
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; idempotent.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim (hosted-CF → Payment/PG), stok commit döngüsü + telafi + sepet temizliği + watchdog **Checkout.
Orchestrator** sağasının. Order stok yazmaz, indirim kuralına sahip değil (yüzdeyi Discount'tan sorar,
yalnız tutara uygular), ürün fiyat otoritesi değil.
