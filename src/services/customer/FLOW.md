# Customer — Domain Süreci (EventStorming)

**BC ne yapar:** Kullanıcının **adres defterini** tutar. Checkout yolunun okuduğu kayıtlı teslimat
kaynağıdır. İzole BC: integration event yayınlamaz/tüketmez; kanalı MCP (chat) + yapısal S2S gRPC (adres).
**Kart-saklama (Wallet) 076'da, merchant kimliği/onboarding makineleri (087) 093 PG pivotunda SÖKÜLDÜ.**

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> 🟩 read-model · 🩷 dış sistem · 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu
> backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟦 **Adres ekle/güncelle/sil + varsayılan seç** → 🟨 **Adres defteri** — ≤1        `(AddressBook.AddAddress`
   varsayılan invariant'ı defterde tek yazmada korunur; yüzey chat/MCP                ` / SetDefaultAddress)`
   (add_address/update/remove/set_default/list).
2. 🟩 **Varsayılan adres ödeme/sipariş akışını besler** ⇦ Order — yapısal S2S         `(AddressGrpcService)`
   adres ucu (Order.Api gRPC ile çeker). _(093: ödeme yolu uykuda — pay-by-link gelince yeniden canlanır.)_

## Domain kuralları (süreci yöneten değişmezler)

- **En fazla 1 varsayılan.** `AddressBook`'ta varsayılan seçimi diğerlerini atomik temizler.
- **Kullanıcı başına tek defter.** `UserId` ile keyli; ilk yazımda tembel oluşturulur.
- **İzole BC, event yok.** Ne yayınlar ne tüketir; kanal MCP + gRPC.
- **Kart-saklama YOK (076), merchant kaydı YOK (093).** Cüzdan/tokenize/vault ve
  MerchantInformation/onboarding dünyası söküldü — PG pivotu, Merchant BC öldü.

## Sınır (bu BC'nin dokunmadığı)

Ödeme/çekim yok (Payment/PG), sipariş yok (Order BC). Kart verisi hiç tutulmaz; merchant kimliği artık
bu BC'de yaşamaz (yeni PG kontratında ihtiyaç doğarsa yeniden tasarlanır).