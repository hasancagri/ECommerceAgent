# Tasarım: Ayşe uçtan-uca kartla ödeme (checkout canlandırma epiği)

**Tarih:** 2026-10-22 (fork A güncellemesi 2026-10-23) · **Durum:** fork A onaylı, spec-kit'e hazır ·
**Kapsam:** 3 repo (ECommerceAgent, PaymentGateway, AgentPlatform)

> **Fork kararı (2026-10-23): A — merchant AgentPlatform'da.** PG anayasası v2.0.0 İlke V PG'ye
> MCP+merchant girişini NON-NEGOTIABLE yasakladığı için merchant domaini PG yerine AgentPlatform'da
> yaşar (IdP+fasat+kontrol-düzlemi zaten orada). PG DOKUNULMAZ. Aşağıdaki PG/AgentPlatform bölümleri
> bu karara göre güncellendi; EC bölümü değişmedi.

## Niyet / neden

Bugün müşteri (Ayşe) sepetten kartla alışveriş tamamlayamıyor. 093 PG pivotunda EC↔PG ödeme
bağlantısı söküldü (`start_payment` = `PAYMENT_DISABLED`, intent gRPC'si kaldırıldı). Amaç: zinciri
uçtan-uca canlandırmak — Ayşe chat'te "öde" der, güvenli hosted sayfada kartını girer, ödeme geçer,
siparişi onaylanır.

**Kritik bulgu (keşif):** PG tarafı BİTMİŞ ve CANLI (spec 089/090). İş ~%90 EC yeniden-kablolama +
merchant kapısı. PG'de ödeme mekaniği yeniden yazılmayacak.

## Hedefler

- Ayşe uçtan-uca kartla ödeyip siparişini tamamlar (NonSecure ilk sürüm, SimBank'larla dev).
- Merchant kendi AI istemcisiyle PG'ye kaydolur (pending); super-admin Claude Desktop'tan (MCP)
  kayıtları görür, onaylar/reddeder, aktive eder — satışı "açar".
- Ödeme başarısı EC'de uyuyan 092 koreografiyi tetikler (stok commit → order confirm → fanout).
- Kart PII asla agent/chat/MCP'den geçmez — yalnız PG hosted sayfasında.

## Sıralama (kritik yol) — fork A (2026-10-23)

**Tam merchant önce, demo bekler** (kullanıcı kararı). **Merchant AgentPlatform'da** (anayasa). Sıra:
AgentPlatform merchant domaini + admin MCP (en büyük, net-new, kilitleyici) → EC rewire (start_payment
+ webhook + link-yarat) → uçtan-uca test. **PG DOKUNULMAZ** (en fazla merchant-durumu S2S sorgu seam'i).
Ayşe demosu en büyük işin arkasında bilinçli bekler.

## Hedef-değil (bu epikte YOK)

- 3D-Secure akışı (PG'de seam var; ilk sürüm NonSecure). Sonraki sürüm.
- Gerçek banka/iyzico entegrasyonu — SimBank yeterli.
- Dönüş (thank-you) web sayfası — chat-only; ReturnUrl minimal.
- Settlement/komisyon hesabı, cüzdan.
- Çoklu para birimi — yalnız TRY.

## Anti-hedef (başarı sayılmaz)

- Kart numarasının herhangi bir EC/agent/log yüzeyinde görünmesi (Anayasa İlke VIII ihlali).
- Yarım/Pending order kirliliği (ödeme başlamadan kalıcı order yaratmak — ölçülecek).
- PG'nin kendi RabbitMQ event'lerini EC'nin doğrudan tüketmesi (sistemler-arası broker paylaşımı).
  Entegrasyon = HMAC webhook (dış-taraf callback istisnası), EC-içi event ayrı.

## Kısıtlar

- **Kart PII sınırı:** kart yalnız PG hosted HTML sayfasında; EC yalnız opak `pgPaymentRef` görür.
- **EC↔PG kontratı = REST + HMAC webhook** (gRPC değil — 093'te söküldü, PG REST sunuyor):
  - EC→PG: `POST /payment-links` (header `X-Api-Key: <StoreApiKey>`), gövde
    `{merchantId, amount, currency:"TRY", orderRef, returnUrl, storeName, orderSummary}` → `{linkUrl, pgPaymentRef}`
  - PG→EC: `POST {CallbackUrl}` webhook, header `X-Signature: HMACSHA256(CallbackSecret, rawBody)`,
    gövde `{pgPaymentRef, orderRef, status, amount, currency, bankCode, reasonCode}`
- **Dev sırları (user-secrets'ta mevcut):** StoreApiKey `dev-store-api-key-2026`,
  CallbackSecret `dev-sim-callback-secret-0909`.
- **092 koreografi değişmez** — yalnız tetikleyici (`PaymentSucceeded` yayınlayıcısı) eklenir.
- Her repo kendi spec-kit akışıyla ilerler (proje konvansiyonu; bu belge koordinasyon kaynağı).

## Karar verilenler (nasıl)

### EC (ECommerceAgent)
1. **`start_payment` canlandır** (`Order.Api/.../Agents/Commands/StartPayment.cs`): kısa devreyi kaldır;
   sepet kalemlerini al (uykudaki `BasketItemsClientProxy` gRPC'yi canlandır), tutarı **liste − aktif
   indirim** ile hesapla (canlı `DiscountClient` kullan; adres chat-only'de atlanır), `Order` Pending +
   `PaymentIntent` (mevcut aggregate yeniden kullan) yarat, PG `POST /payment-links` çağır, dönen
   `pgPaymentRef`'i `PaymentIntent`'e YAZ, `linkUrl`'yi `StartPaymentResponse.HostedUrl`'de Ayşe'ye
   döndür. `orderRef` = OrderId.
2. **Webhook alıcı REST ucu** (093'te silinen `HandlePaymentCallback` + `CallbackSignatureValidator`
   dönüşü, PG webhook alanlarına uyarlı): `X-Signature` HMAC doğrula, **`pgPaymentRef` ile**
   `PaymentIntent` eşle (`orderRef`=OrderId insan korelasyonu), **`amount` ↔ `PaymentIntent.Amount`
   eşitlik guard'ı** (anti-tamper), `status`=Paid → `MarkSucceeded` → `PaymentSucceeded` yayınla;
   başarısız → `MarkFailed` → `PaymentFailed`. İdempotentlik `MarkSucceeded`/`MarkFailed` durum
   guard'ıyla (webhook retry güvenli). `[Transactional]` outbox.
3. **092 koreografi** zaten kurulu — `PaymentSucceeded` → `StockCommitRequested` → `StockCommitted` →
   `Order.Confirm` + `OrderCompleted` fanout. Dokunulmaz, yalnız artık tetiklenir.
4. **Config (Options pattern):** PG base URL = **düz config base adresi (sabit localhost port, cross-host
   service-discovery DEĞİL)**, StoreApiKey, CallbackSecret, kendi MerchantId. `IConfiguration` doğrudan
   okuma yok.
5. **ReturnUrl hedefi:** EC'de 200 dönen basit `GET /checkout/done` stub'ı (tarayıcı redirect'i düşer;
   gerçek durum webhook'tan). Görsel akış chat-only.
6. **Wolverine keşfi:** yeni consumer/handler `*Consumers`/`*Handler` değilse `IncludeType` ekle.
7. **Tahsilat kapısı (fork A, PG'den taşındı):** `start_payment` PG `POST /payment-links` çağrısından
   ÖNCE mağaza-merchant'ın Active olduğunu doğrular (fail-fast, Ayşe sayfayı görmeden). Doğrulama kaynağı
   AgentPlatform merchant-durumu (AP-13 seam) — ilk sürüm demo tek-merchant olduğu için config-sabit
   MerchantId + açılışta/periyodik durum kontrolü yeterli olabilir (builder kararı, açık soruya bağlı).

### PG (PaymentGateway) — DOKUNULMAZ (fork A)
7. **Merchant domaini PG'ye GİRMEZ** (anayasa v2.0.0 İlke V: PG MCP expose etmez, ödeme akışı MCP'ye
   girmez). PG pay-by-link akışı zaten canlı (089/090) — bu epikte PG koduna dokunulmaz.
8. **Tahsilat kapısı PG'de DEĞİL.** `CreatePaymentLink` merchant varlık/durum sorgusu yapmaz. "Merchant
   Active mi" kapısı EC `start_payment` tarafında (bkz. EC-7, builder kararı). İleride gerekirse
   AgentPlatform→PG merchant-durumu S2S seam'i ayrı spec'le açılır.

### AgentPlatform — merchant domaini (net-new, en büyük parça; PG'den taşındı, fork A)
9. **İki aggregate + durum makinesi:** `Merchant` (durumlar Active↔Passive↔Suspended) + `RegisterRequest`
   (Pending→Approved/Rejected). Kayıt onaylanınca Merchant Active doğar. **AdminActionLog YOK** (kullanıcı
   kararı 2026-10-23 — denetim izi bu sürümde gerekmiyor). Marten document/event store, kendi DB/şema
   (AgentPlatform host'unda).
10. **Merchant-yüzü kayıt:** `register_merchant` tool'u pending `RegisterRequest` yaratır (yeni `merchant`
    rolü). Sır/kimlik değeri dönüşe render edilmez (İlke VIII).
11. **9 super-admin tool'u** (adlar hazır): `admin_get_pending_registrations`, `admin_approve_registration`,
    `admin_reject_registration`, `admin_activate_merchant`, `admin_deactivate_merchant`,
    `admin_suspend_merchant`, `admin_get_merchants`, `admin_get_merchant`, `admin_update_merchant`. Her
    biri slice + `[RequiredScope(merchant.*)]` + `[McpServerToolType]`; AgentPlatform'ın kendi `/mcp` +
    fasadında görünür (anayasaya uygun — PG değil).
12. **Scope + rol + kayıt yolu:** super-admin scope `merchant.*` (admin tool'ları, IdP AppRegistry'ye
    ekle, super-admin rolüne bağla); yeni `merchant` rolü + `external-merchant-agent` seed client + DCR
    şablonu + rol-atama UX (`/Admin/*`). AgentPlatform'da kendi başına bir feature.
13. **Merchant-durumu S2S seam (EC gate için):** EC `start_payment` Active-gate'ini AgentPlatform
    merchant-durumu sorgusuyla besler (AgentPlatform→EC yönünde gRPC/REST, builder kararı). Demo
    tek-merchant (mağaza) olduğu için ilk sürümde config-sabit MerchantId yeterli olabilir — bkz. açık soru.

## Builder'a bırakılan / açık sorular

- **Repo/spec sırası (fork A):** AgentPlatform lider (merchant domaini, kritik yol, kilitleyici) → EC
  rewire → uçtan-uca test. PG DOKUNULMAZ (spec yok). Her repo kendi spec-kit akışıyla ayrı spec (AP-00x,
  EC-09x); bu belge iki spec'in ortak gerçek-kaynağı.
- **Merchant Active-gate yeri (fork A açtı):** gate EC `start_payment`'ta (EC-7). AÇIK: durum kaynağı
  canlı S2S sorgu mu (AP-13 seam, her checkout'ta AgentPlatform'a sorar) yoksa demo tek-merchant olduğu
  için config-sabit MerchantId + kaba durum kontrolü mü? İlk sürüm basiti seçer; builder/clarify netleştirir.
- **Çok-host dev koşumu:** 3 Aspire host elle sırayla başlar; EC→PG + fasat→PG sabit localhost
  portlarıyla çözülür (service-discovery tek-host). Uçtan-uca "done" doğrulama script'i bu sırayı izler.
- **Adres/kargo:** chat-only'de kargo yok → adres toplanmaz (atlanır). İleride kargo feature'ı açarsa
  `start_payment` adres gRPC'sini canlandırır.
