# Payment — Domain Süreci (EventStorming)

**BC ne yapar:** Ödeme girişimlerinin (`PaymentIntent`) durum makinesini + kaydını tutar. **094
pay-by-link: ödeme girişi CANLI** — Order S2S ile niyet + PG hosted link üretilir; PG sonuç webhook'u
(imzalı) niyeti terminal'e taşır ve Order'a sonuç fanout'u yayar. Kart alanı HİÇ taşımaz (İlke VIII).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🩷 dış sistem ·
> 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟦 **Niyet + link oluştur** ⇦ Order → 🩷 **PG hosted link** →                            `(PaymentIntentGrpcService →`
   🟨 **PaymentIntent (Pending)** — Order S2S gRPC ile tutar gelir; PG `POST /payment-links`       ` PgPaymentLinkClient)`
   çağrılır (`X-Api-Key`), dönen `pgPaymentRef` + hosted link niyete yazılır. Mağaza satışa
   kapalıysa (PG `STORE_NOT_LIVE`) niyet OLUŞMAZ (fail-fast). Kart alanı girmez.
2. 🩷 **PG ödeme sonucu** → 🟪 **imzalı webhook niyeti terminal'e taşır** →                  `(CallbackSignatureValidator,`
   🟦 **Sonucu işle** → 🟧⭐ **Ödeme alındı** / 🟧 **Ödeme başarısız** ⇨ Order —              ` ProcessPaymentCallback)`
   `X-Signature` HMAC doğrulanır (fail-closed), `pgPaymentRef` ile niyet eşlenir, tutar
   anti-tamper guard'ından geçer; `Paid`→`MarkSucceeded`→`PaymentSucceeded` /
   başarısız→`MarkFailed`→`PaymentFailed` fanout'u. İdempotent (çift webhook tek koreografi).
3. 👤 Kullanıcı ödemelerini okur _(planned)_ — kişi kendi GEÇMİŞ girişimlerini listeler;
   okuma yüzeyi MCP'yle birlikte söküldü, REST first-party'de yeniden açılacak.

## Domain kuralları (süreci yöneten değişmezler)

- **Kart alanı yoktur.** PAN/kart verisi bu BC'ye hiç girmez; yalnız opak `pgPaymentRef` + tutar.
- **Durum geçişleri aggregate'te.** `Pending→Succeeded/Failed/Expired`; Succeeded terminal +           `(PaymentIntent)`
  geri-alınamaz; `MarkSucceeded`/`MarkFailed` idempotent (durum-guard'lı → webhook retry güvenli).
- **Webhook fail-closed + anti-tamper (İlke VIII).** İmza/eşleşme/tutar uyuşmazsa 404, durum değişmez,
  bilgi sızmaz; sonuç yalnız Pending→terminal gerçek geçişinde fanout'a döner.
- **İzole BC (İLKE I).** Kendi `paymentDb`'si; Order↔Payment S2S gRPC, PG dış-sağlayıcı REST+HMAC;
  sonuç Order'a yalnız fanout event'le çıkar.

## Sınır (bu BC'nin dokunmadığı)

Gerçek para hareketi + kart işleme (dış PG'de); sipariş yaşam döngüsü + stok + sepet (Order/Stock/Basket).
Payment sipariş oluşturmaz, stok bilmez. Merchant kimliği/onboarding PG tarafında (bu BC yalnız config
MerchantId'yi link isteğinde taşır).
