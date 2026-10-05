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
2. **Kayıt store-başlatır (makine-handoff, 087).** Admin MCP aksiyonu  `(AdminStartOnboarding →`
   tetikler; store correlationId üretir, kaydı Pending'e alır, PG'ye    ` MerchantInformation.StartRegistration`
   S2S register atar (bootstrap key başlıkta + callbackUrl/correlation  ` → PgOnboardingClient)`
   gövdede). Credential/finans/sır MCP arg'ına ve sohbete HİÇ girmez.
   Tek-aktif kayıt invariant'ı farklı correlation'lı ikinci başlatmayı reddeder.
3. **PG onayı asenkron (PG admin MCP).** Onaysız aktivasyon yok; store Pending'de bekler.
   Credential bu yanıtta DÖNMEZ — asenkron callback'le gelir.
4. **PG HMAC-callback → store credential'ı persist eder.** PG MerchantId+Key'i  `(CallbackSignatureValidator →`
   store callback ucuna `CallbackSecret` ile HMAC-imzalı POST eder; store        ` ReceiveMerchantCredentials →`
   imzayı (deserialize ÖNCESİ) + correlation'ı doğrular → Active'e çeker.         ` MerchantInformation.ApplyCredentialsFromCallback)`
   İmza geçersiz=400; eşleşmeyen/çift correlation idempotent nötr yutulur.
5. **Merchant kimliği kaydı ödeme akışını besler.**                  `(MerchantKeyGrpcService)`
   Yapısal S2S merchant-key ucu (Payment.Api gRPC ile çeker, 077). Değer sohbete/loga girmez.
6. **Key kaybolursa/sızarsa yenilenir — EKRANSIZ (087).** Admin agent  `(AdminReissueMerchantKey →`
   kayıtlı MerchantId ile PG'de reissue tetikler (yeni correlation);    ` PgOnboardingClient)`
   eski key PG'de anında ölür, YENİ key aynı HMAC-callback yoluyla
   (adım 4) store'a otomatik gelir. Reveal URL / elle-giriş adımı YOK.
7. **Admin kimlik/başvuru durumunu sorgular.** Merchant kimliği       `(AdminGetMerchantStatus /`
   kayıtlı mı + onboarding hangi aşamada; boş sonuç meşru durum.       ` AdminOnboardingStatus)`

## Domain kuralları (süreci yöneten değişmezler)

- **En fazla 1 varsayılan.** `AddressBook`'ta varsayılan seçimi diğerlerini atomik temizler.
- **Kullanıcı başına tek defter.** `UserId` ile keyli; ilk yazımda tembel oluşturulur.
- **Credential insan-yüzeyde hiç render edilmez (087).** MerchantId/MerchantKey ekran/MCP-dönüşüne
  girmez; teslim HMAC-callback (S2S), kullanım gRPC. Elle-giriş ekranı emekli ([[adr-mcp-control-plane-no-secret-return]]).
- **Callback fail-closed + imza önce.** `CallbackSignatureValidator` HMAC doğrulanmadan gövde deserialize
  EDİLMEZ; geçersiz imza=400, persist yok. Sahte credential POST'u kesilir.
- **Tek-aktif kayıt + idempotent callback.** `StartRegistration` farklı correlation'lı ikinci başlatmayı
  reddeder; `ApplyCredentialsFromCallback` eşleşmeyen correlation'ı nötr reddeder, çift-callback'i no-op yutar.
- **PG erişilemezse ilgili işlem hata döner.** Kayıt başlat / key reissue / durum sorgu PG'ye S2S gider;
  PG down ise `MERCHANT_ONBOARDING_UNAVAILABLE` döner (yeniden denenebilir; kayıt PG kabulünden önce persist edilmez).
- **İzole BC, event yok.** Ne yayınlar ne tüketir; kanal REST/MCP (+ PG'ye S2S REST).
- **Kart-saklama YOK (076).** Cüzdan/tokenize/vault söküldü; ödeme yöntemi hosted-CF (077).

## Sınır (bu BC'nin dokunmadığı)

Ödeme/çekim yok (hosted-CF → PG), sipariş yok (Order BC). Kart verisi hiç tutulmaz.
