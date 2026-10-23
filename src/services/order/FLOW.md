# Order — Domain Süreci (EventStorming)

**BC ne yapar:** Sepet kalemlerini bir **siparişe** bağlar; ödeme başlatır (PG hosted link), ödeme
başarılı olunca stok commit'i talep eder, stok onaylanınca siparişi tamamlar ve satın-alma kanıtını
yayar. **094 pay-by-link: `start_payment` CANLI** — sepet + indirimli tutar → Pending sipariş → Payment
BC'den hosted ödeme linki. **092 koreografisi** ödeme sonucu event'leriyle tetiklenir.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> 🟩 read-model · 👤 aktör · 🩷 dış sistem · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick
> `(…)` = koda köprü, süreç değil. Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir;
> mekanik rename'i guard yakalar.

## Süreç

1. 👤 **Müşteri** → 🟦 **Ödeme başlat** → 🩷 **hosted ödeme linki döner** —                   `(StartPayment →`
   sepet kalemleri (Basket gRPC, sunucu-otoritesi) + aktif indirim (Discount gRPC) ile              ` PaymentIntentClient)`
   tutar hesaplanır (liste−indirim), Pending sipariş kalemleri **etkin fiyatla** eklenir,
   Payment BC'ye S2S ile ödeme niyeti + PG linki istenir; `HostedUrl` müşteriye döner.
   Adres chat-only'de atlanır. Başarısız yolda (boş sepet / mağaza kapalı / PG hata)
   sipariş KALICI OLMAZ. Kart alanı bu akıştan geçmez (İlke VIII).
2. 🟧⭐ **Ödeme alındı** ⇦ Payment → 🟪 **başarılı ödeme stok commit'i ister** →              `(PaymentConsumers →`
   🟦 **Stok commit talep et** ⇨ Stock — `PaymentSucceeded` fanout'u tüketilir;               ` StockCommitRequested)`
   Pending siparişin kalemleri için `StockCommitRequested` yayılır.
3. 🟧 **Ödeme başarısız** ⇦ Payment → 🟪 **başarısız ödeme siparişi düşürür** →              `(PaymentConsumers →`
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
- **Ödeme öncedendir.** Checkout charge çekmez; sipariş ödeme öncesi Pending oluşturulur (OrderId korelasyon anahtarı, PG'ye orderRef).
- **Tutar = liste − aktif indirim.** Sepet/indirim gRPC'den canlı; sipariş kalemleri etkin fiyatla → `TotalPrice` = ödenecek tutar.
- **Başarısız başlatmada sipariş doğmaz (094).** PG linki başarıyla alınmadan Order commit edilmez — yarım Pending kirliliği yok.
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; idempotent.
- **Koreografi (092).** Checkout merkezi saga değil event zinciri; her tüketim idempotent (durum-guard'lı), teknik hata Wolverine retry→DLQ.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim (Payment/PG) ve stok commit/atomiklik Stock BC'nin işidir. Order stok yazmaz,
indirim kuralına sahip değil (yüzdeyi Discount'tan sorar, yalnız tutara uygular), ürün fiyat otoritesi değil.
Merkezi orkestratör/watchdog yok (092): takılma durable mesaj + DLQ + Pending sipariş sorgusuyla görünür.
