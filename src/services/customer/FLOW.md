# Customer — Domain Süreci

**BC ne yapar:** Kullanıcının **adres defterini** tutar (+ merchant kimliği admin kaydı). Checkout yolunun
okuduğu kayıtlı teslimat kaynağıdır. İzole BC: integration event yayınlamaz/tüketmez; kanalı MCP (chat) +
yapısal S2S REST (merchant-key). **Kart-saklama (Wallet/cüzdan) 076'da SÖKÜLDÜ** (kart yönünden vazgeçildi;
ödeme hosted-CF'e taşınacak — [[hosted-cf-checkout-pivot]]).

> Domain-önce anlatı. Sağdaki `(…)` = koda köprü. Süreç değişince güncellenir; guard rename'i yakalar.

## Süreç

1. **Adres eklenir/güncellenir/silinir + varsayılan seçilir.**       `(AddressBook.AddAddress`
   ≤1 varsayılan invariant'ı defterde tek yazmada korunur;           ` / SetDefaultAddress)`
   yüzey chat/MCP (add_address/update/remove/set_default/list).
2. **Onboarding başvurusu PII'siz başlatılır (078).** Admin agent    `(AdminStartOnboarding`
   yalnız PG hosted form linki üretir; PII (TCKN/IBAN) PG formunda     ` → PgOnboardingClient)`
   toplanır, store'a ve sohbete hiç girmez. Aynı e-postada Pending
   başvuru varsa PG yeni oturum açmaz (FormUrl boş; durumu adım 8'den izle).
3. **PG approve → mail + tek kullanımlık teslim linki (PG tarafı).** Admin ikiliyi PG teslim
   sayfasından BİR KEZ görür; store'a taşıma insan-aracılıdır (kontrat 078).
4. **Credential girişi store'un hosted ekranından.** Agent süreli +  `(CredentialEntrySession.Create)`
   tek kullanımlık ekran linki üretir (token = yetki); admin
   MerchantId+Key'i ekrana elle girer.
5. **Kayıt anında PG doğrulaması + tek-kullanım tüketimi.** Geçersiz `(SubmitMerchantCredentials → `
   ikili RET (oturum yaşar); PG erişilemezse "doğrulanamadı"         ` CredentialEntrySession.Consume)`
   işaretiyle saklanır; başarı oturumu öldürür.
6. **Merchant kimliği kaydı ödeme akışını besler.**                  `(MerchantInformation)`
   Yapısal S2S merchant-key ucu (Payment.Api gRPC ile çeker, 077).
7. **Key kaybolursa/sızarsa yenilenir (080).** Admin agent kayıtlı   `(PgOnboardingClient)`
   MerchantId ile PG'de reissue tetikler; yanıt YALNIZ reveal URL.
   Merchant yeni key'i reveal'dan bir kez okur, adım 4-5 yoluyla
   store'a girer (UpdateKey, PG doğrulamalı). Eski key PG'de anında ölü.
8. **Admin kimlik/başvuru durumunu sorgular.** Merchant kimliği       `(AdminGetMerchantStatus /`
   kayıtlı mı + onboarding hangi aşamada; boş sonuç meşru durum.       ` AdminOnboardingStatus)`

## Domain kuralları (süreci yöneten değişmezler)

- **En fazla 1 varsayılan.** `AddressBook`'ta varsayılan seçimi diğerlerini atomik temizler.
- **Kullanıcı başına tek defter.** `UserId` ile keyli; ilk yazımda tembel oluşturulur.
- **Ekran oturumu tek kullanımlık + süreli.** `CredentialEntrySession.Consume` çift tüketimi ve
  süresi geçmişi reddeder; GET tüketmez (vazgeçmek linki öldürmez, süre öldürür).
- **PII/MerchantKey sohbete girmez (078).** Key yalnız ekran POST'unda taşınır, hiçbir log/yanıta
  yazılmaz. (078'in denetim izi kullanıcı kararıyla söküldü, 2026-09-19.)
- **PG erişilemezse ilgili işlem hata döner.** Onboarding başlat / key reissue / durum sorgu PG'ye S2S gider;
  PG down ise `MERCHANT_ONBOARDING_UNAVAILABLE` döner (yeniden denenebilir, kısmi/bozuk kayıt bırakmaz).
- **İzole BC, event yok.** Ne yayınlar ne tüketir; kanal REST/MCP (+ PG'ye S2S REST).
- **Kart-saklama YOK (076).** Cüzdan/tokenize/vault söküldü; ödeme yöntemi hosted-CF (077).

## Sınır (bu BC'nin dokunmadığı)

Ödeme/çekim yok (hosted-CF → PG), sipariş yok (Order BC). Kart verisi hiç tutulmaz.
