# Payment — Domain Süreci (EventStorming)

**BC ne yapar:** Ödeme girişimlerinin (`PaymentIntent`) durum makinesini ve kaydını tutar. **093 PG
pivotu: ödeme GİRİŞİ söküldü** — hosted-CF yolu (PG hosted link + HMAC callback + MerchantKey zinciri +
terk-timer) gitti; yeni intent oluşturan yol YOK. Aggregate + ödeme-geçmişi okuma + sonuç fanout
kontratları yerinde (pay-by-link kontratı gelince yeniden canlanır). Kart alanı HİÇ taşımaz.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🩷 dış sistem ·
> 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟥 **Hotspot (open): yeni PG pay-by-link kontratı bekleniyor** — ödeme girişi burada geri açılacak:
   link isteme (banka-adaptörlü PG) + sonuç webhook'u + intent yaşam döngüsü yeni kontrat spec'iyle
   yeniden kurulur. O güne dek yeni intent OLUŞMAZ (`start_payment` Order'da PAYMENT_DISABLED döner).
2. 🟧⭐ Ödeme alındı / 🟧 Ödeme başarısız ⇨ Order — fanout kontratı YERİNDE ama UYKUDA _(planned:
   yeni ödeme akışı yayacak)_; exchange'ler açılışta declare edilmeye devam eder.
3. 👤 **Kullanıcı ödemelerini okur** — kişi kendi GEÇMİŞ girişimlerini listeler          `(GetAllPaymentsByUserId)`
   (eski hosted-CF kayıtları dahil; veri silinmedi).

## Domain kuralları (süreci yöneten değişmezler)

- **Kart alanı yoktur.** PAN/kart verisi bu BC'ye hiç girmez.
- **TxRef tekil.** Marten unique index; durum geçişleri + guard'lar aggregate metotlarında             `(PaymentIntent)`
  (Pending→Succeeded/Failed/Expired; Succeeded terminal + geri-alınamaz). Yeni akış aynı
  çekirdeği kullanır.
- **İzole BC (İLKE I).** Kendi `paymentDb`'si; sonuç yalnız fanout event'le (Order tüketir) çıkar.

## Sınır (bu BC'nin dokunmadığı)

Gerçek para hareketi (dış PG'de); sipariş yaşam döngüsü + stok + sepet (Order/Stock/Basket). Payment
sipariş oluşturmaz, stok bilmez. MerchantKey/merchant kimliği dünyası 093'te tamamen söküldü.
