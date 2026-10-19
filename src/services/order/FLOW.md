# Order — Domain Süreci (EventStorming)

**BC ne yapar:** Adresi + sepet kalemlerini bir **siparişe** bağlar; siparişi Pending doğurur, ödeme
başarılı olunca stok commit'i talep eder, stok onaylanınca siparişi tamamlar ve satın-alma kanıtını yayar.
**077 hosted-CF:** "ödeme yap" → `start_payment` sepeti okur, siparişi Pending oluşturur, Payment'tan hosted
link ister; ödeme başarılı callback'i (Payment fanout) checkout koreografisini başlatır. **092: orkestratör
yok** — stok/onay/sepet adımları event tepkileriyle yürür.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> 🟩 read-model · 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda
> köprü, süreç değil. Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir;
> mekanik rename'i guard yakalar.

## Süreç

1. 👤 **Müşteri** → 🟦 **Ödeme başlat** → 🟨 **Sipariş Pending doğar** ⇨ Payment, Discount — `(StartPayment →`
   sepet (gRPC, sunucu-otoritesi) + varsayılan adres okunur; ödenecek tutar                   ` Order.Create;`
   sepet kalemlerine O AN aktif indirim yüzdeleri uygulanarak bulunur (canlı,                 ` DiscountClient; PaymentIntentClient)`
   vitrin snapshot'ına güvenmez; erişilemez→liste fiyatı); Payment'tan hosted
   link istenir + kullanıcıya döner. Re-use: canlı link varsa yeni sipariş yok.
   Boş sepet → dostça mesaj (sipariş yok).
2. 🟧⭐ **Ödeme alındı** ⇦ Payment → 🟪 **başarılı ödeme stok commit'i ister** →              `(PaymentConsumers →`
   🟦 **Stok commit talep et** ⇨ Stock — Payment `PaymentSucceeded` fanout'u                  ` StockCommitRequested)`
   tüketilir; Pending siparişin kalemleri için `StockCommitRequested` yayılır
   (charge YOK, ödeme öncedendir).
3. 🟧 **Ödeme başarısız/terk** ⇦ Payment → 🟪 **başarısız ödeme siparişi düşürür** →        `(PaymentConsumers →`
   🟦 **Siparişi iptal et** — `PaymentFailed` → `Order.Cancel` (stok hiç düşmedi).            ` Order.Cancel)`
4. 🟧 **Stok commit edildi** ⇦ Stock → 🟪 **stok onaylı sipariş tamamlanır** →              `(StockConsumers →`
   🟦 **Onayla** → 🟧⭐ **Sipariş tamamlandı** ⇨ Reviews, Library, Basket —                  ` Order.Confirm → OrderCompleted)`
   `StockCommitted` → `Order.Confirm`; Confirm pivotunda `OrderCompleted` fanout'u
   yayılır. Idempotent (yalnız Pending'den).
5. 🟧 **Stok yetersiz** ⇦ Stock → 🟪 **yetersiz stok siparişi düşürür** →                   `(StockConsumers →`
   🟦 **Siparişi iptal et** — `StockCommitFailed` → `Order.Cancel` (hiç stok                 ` Order.Cancel)`
   düşmedi; ödeme çekilmişti → manuel iade, otomatik iade kapsam dışı). 🟥 Hotspot
   (closed: 092 kabul edilen borç — otomatik iade ayrı feature).
6. 👤 **Müşteri** → 🟩 **Sipariş geçmişi** — kişi kendi geçmişini listeler.                 `(GetOrders)`

## Domain kuralları (süreci yöneten değişmezler)

- **Durum geçişi aggregate'te korunur.** Yalnız `Pending→Confirmed` / `Pending→Cancelled`.
- **Sipariş kullanıcının adresine gider.** Adres varsayılan adres defterinden (FR-001b); ödeme sahibi değiştirmez.
- **Ödeme öncedendir (hosted-CF).** Checkout charge çekmez; sipariş ödeme öncesi Pending oluşturulur (OrderId korelasyon anahtarı).
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; idempotent.
- **Koreografi (092).** Checkout merkezi saga değil event zinciri; her tüketim idempotent (durum-guard'lı), teknik hata Wolverine retry→DLQ.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim (hosted-CF → Payment/PG) ve stok commit/atomiklik Stock BC'nin işidir. Order stok yazmaz,
indirim kuralına sahip değil (yüzdeyi Discount'tan sorar, yalnız tutara uygular), ürün fiyat otoritesi değil.
Merkezi orkestratör/watchdog yok (092): takılma durable mesaj + DLQ + Pending sipariş sorgusuyla görünür.
