# Order — Domain Süreci (EventStorming)

**BC ne yapar:** Adresi + sepet kalemlerini bir **siparişe** bağlar; ödeme başarılı olunca stok commit'i
talep eder, stok onaylanınca siparişi tamamlar ve satın-alma kanıtını yayar. **093 PG pivotu: ödeme
başlatma GEÇİCİ KAPALI** — `start_payment` tool'u durur (dış sözleşme) ama PAYMENT_DISABLED döner;
sipariş oluşturan tek yol şimdilik yok. **092 koreografisi YERİNDE** (uykuda): ödeme sonucu event'leri
gelmeye başladığında zincir değişmeden canlanır.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> 🟩 read-model · 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda
> köprü, süreç değil. Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir;
> mekanik rename'i guard yakalar.

## Süreç

1. 👤 **Müşteri** → 🟦 **Ödeme başlat** → ❌ **kapalı-bildirimi döner (093)** —               `(StartPayment)`
   handler sepete/adrese/Payment'a hiç gitmeden PAYMENT_DISABLED hatası döner;
   Pending sipariş YARATILMAZ. 🟥 Hotspot (open): yeni PG pay-by-link kontratı
   bekleniyor — sepet oku + indirimli tutar + Pending order + link isteme akışı
   burada geri açılacak (sepet/adres/indirim gRPC istemcileri uykuda yerinde).
2. 🟧⭐ **Ödeme alındı** ⇦ Payment → 🟪 **başarılı ödeme stok commit'i ister** →              `(PaymentConsumers →`
   🟦 **Stok commit talep et** ⇨ Stock — `PaymentSucceeded` fanout'u tüketilir;               ` StockCommitRequested)`
   Pending siparişin kalemleri için `StockCommitRequested` yayılır. _(093: tetikleyici
   ödeme yolu kapalı — zincir uykuda, kontrat değişmedi.)_
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
- **Ödeme öncedendir.** Checkout charge çekmez; sipariş ödeme öncesi Pending oluşturulur (OrderId korelasyon anahtarı).
- **Ödeme kapalıyken sipariş doğmaz (093).** `start_payment` kısa devre — yarım Pending kirliliği yok.
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; idempotent.
- **Koreografi (092).** Checkout merkezi saga değil event zinciri; her tüketim idempotent (durum-guard'lı), teknik hata Wolverine retry→DLQ.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim (Payment/PG) ve stok commit/atomiklik Stock BC'nin işidir. Order stok yazmaz,
indirim kuralına sahip değil (yüzdeyi Discount'tan sorar, yalnız tutara uygular), ürün fiyat otoritesi değil.
Merkezi orkestratör/watchdog yok (092): takılma durable mesaj + DLQ + Pending sipariş sorgusuyla görünür.
