# Order — Domain Süreci (EventStorming)

**BC ne yapar:** Adresi + sepet kalemlerini bir **siparişe** bağlar; siparişi Pending doğurur, ödeme
başarılı olunca checkout orkestratörünü tetikler ve satın-alma kanıtını yayar. **077 hosted-CF:** "ödeme
yap" → `start_payment` sepeti okur, siparişi Pending oluşturur, Payment'tan hosted link ister; ödeme
başarılı callback'i (Payment fanout) siparişi checkout'a sokar. Stok/telafi/sepet temizliği saga'nın işi.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> 🟩 read-model · 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda
> köprü, süreç değil. Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir;
> mekanik rename'i guard yakalar.

## Süreç

1. 👤 **Müşteri** → 🟦 **Ödeme başlat** → 🟨 **Sipariş Pending doğar** ⇨ Payment —          `(StartPayment →`
   sepet (gRPC, sunucu-otoritesi) + varsayılan adres okunur; ödenecek tutar                   ` Order.Create;`
   sepet kalemlerine O AN aktif indirim yüzdeleri uygulanarak bulunur (canlı,                 ` DiscountClient; PaymentIntentClient)`
   vitrin snapshot'ına güvenmez; erişilemez→liste fiyatı); Payment'tan hosted
   link istenir + kullanıcıya döner. Re-use: canlı link varsa yeni sipariş yok.
   Boş sepet → dostça mesaj (sipariş yok).
2. 🟧⭐ **Ödeme alındı** ⇦ Payment → 🟪 **başarılı ödeme checkout'u tetikler** →            `(PaymentConsumers →`
   🟦 **Checkout'u başlat** ⇨ Checkout — Payment `PaymentSucceeded` fanout'u                 ` StartCheckout)`
   tüketilir; Pending siparişten `StartCheckout` yayınlanır (CommitStock→
   Confirm→ClearBasket; charge YOK, ödeme öncedendir).
3. 🟧 **Ödeme başarısız/terk** ⇦ Payment → 🟪 **başarısız ödeme siparişi düşürür** →        `(PaymentConsumers →`
   🟦 **Siparişi iptal et** — `PaymentFailed` → `Order.Cancel` (stok hiç                     ` Order.Cancel)`
   düşmedi; saga'ya girmez).
4. 🟦 **Onayla / İptal et** ⇦ Checkout → 🟧⭐ **Sipariş tamamlandı**                        `(CheckoutConsumers →`
   ⇨ Reviews, Storefront, Library — checkout orkestratörü Order                              ` OrderConfirmed/OrderCompleted)`
   `Confirm/Cancel`'ı sürer; Confirm pivotunda `OrderCompleted` fanout'u
   yayılır. Idempotent.
5. 👤 **Müşteri** → 🟩 **Sipariş geçmişi** — kişi kendi geçmişini listeler.                 `(GetOrders)`

## Domain kuralları (süreci yöneten değişmezler)

- **Durum geçişi aggregate'te korunur.** Yalnız `Pending→Confirmed` / `Pending→Cancelled`.
- **Sipariş kullanıcının adresine gider.** Adres varsayılan adres defterinden (FR-001b); ödeme sahibi değiştirmez.
- **Ödeme öncedendir (hosted-CF).** Checkout charge çekmez; StartCheckout OrderId dolu gelir (`CheckoutId=OrderId`, idempotent).
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; idempotent.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim (hosted-CF → Payment/PG), stok commit döngüsü + telafi + sepet temizliği + watchdog **Checkout.
Orchestrator** sağasının. Order stok yazmaz, indirim kuralına sahip değil (yüzdeyi Discount'tan sorar,
yalnız tutara uygular), ürün fiyat otoritesi değil.
