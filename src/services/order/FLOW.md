# Order — Domain Süreci (EventStorming)

**BC ne yapar:** Sepet kalemlerini bir **siparişe** bağlar; ödeme başlatır (PG hosted link), ödeme
başarılı olunca siparişi tamamlar ve satın-alma kanıtını yayar. 094 pay-by-link akışı: sepet +
indirimli tutar → Pending sipariş → Payment BC'den hosted ödeme linki (giriş yüzeyi MCP ile birlikte
söküldü, REST first-party'de yeniden açılacak). Dijital (PDF) pivot: stok commit ayağı KALKTI —
ödeme sonucu siparişi doğrudan sonuçlandırır.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> 🟩 read-model · 👤 aktör · 🩷 dış sistem · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick
> `(…)` = koda köprü, süreç değil. Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir;
> mekanik rename'i guard yakalar.

## Süreç

1. 👤 **Müşteri** → 🟦 Ödeme başlat _(planned)_ → 🩷 **hosted ödeme linki döner** —           `(PaymentIntentClient)`
   sepet kalemleri (Basket gRPC, sunucu-otoritesi) + aktif indirim (Discount gRPC) ile
   tutar hesaplanır (liste−indirim), Pending sipariş kalemleri **etkin fiyatla** eklenir,
   Payment BC'ye S2S ile ödeme niyeti + PG linki istenir; `HostedUrl` müşteriye döner.
   Adres dijital üründe gerekmez. Başarısız yolda (boş sepet / mağaza kapalı / PG hata)
   sipariş KALICI OLMAZ. Kart alanı bu akıştan geçmez (İlke VIII). _(094'te MCP
   `start_payment` olarak CANLIYDI; MCP yüzeyi söküldü — giriş REST first-party'de
   yeniden açılacak, gRPC istemcileri + aggregate davranışı duruyor.)_
2. 🟧⭐ **Ödeme alındı** ⇦ Payment → 🟪 **başarılı ödeme siparişi tamamlar** →                `(PaymentConsumers →`
   🟦 **Onayla** → 🟧⭐ **Sipariş tamamlandı** ⇨ Reviews, Library, Basket —                  ` Order.Confirm → OrderCompleted)`
   `PaymentSucceeded` fanout'u tüketilir; `Order.Confirm` pivotunda `OrderCompleted`
   fanout'u yayılır (Reviews satın-alma kanıtı, Library satın-alma birikimi, Basket
   temizliği). Idempotent (yalnız Pending'den).
3. 🟧 **Ödeme başarısız** ⇦ Payment → 🟪 **başarısız ödeme siparişi düşürür** →              `(PaymentConsumers →`
   🟦 **Siparişi iptal et** — `PaymentFailed` → `Order.Cancel`.                               ` Order.Cancel)`

## Domain kuralları (süreci yöneten değişmezler)

- **Durum geçişi aggregate'te korunur.** Yalnız `Pending→Confirmed` / `Pending→Cancelled`.
- **Ödeme öncedendir.** Checkout charge çekmez; sipariş ödeme öncesi Pending oluşturulur (OrderId korelasyon anahtarı, PG'ye orderRef).
- **Tutar = liste − aktif indirim.** Sepet/indirim gRPC'den canlı; sipariş kalemleri etkin fiyatla → `TotalPrice` = ödenecek tutar.
- **Başarısız başlatmada sipariş doğmaz (094).** PG linki başarıyla alınmadan Order commit edilmez — yarım Pending kirliliği yok.
- **Satın-alma kanıtı Confirm'de yayılır.** `OrderCompleted` yalnız Confirmed pivotunda; idempotent.
- **Stok yok (dijital pivot).** Ürün PDF — tükenme/rezervasyon kavramı yok; ödeme sonucu tek karar noktası.
- **Koreografi (092).** Checkout merkezi saga değil event zinciri; her tüketim idempotent (durum-guard'lı), teknik hata Wolverine retry→DLQ.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim Payment/PG'nin işidir. Order indirim kuralına sahip değil (yüzdeyi Discount'tan sorar,
yalnız tutara uygular), ürün fiyat otoritesi değil, dijital teslim/kütüphane Library'nin işi.
Merkezi orkestratör/watchdog yok (092): takılma durable mesaj + DLQ + Pending sipariş sorgusuyla görünür.
