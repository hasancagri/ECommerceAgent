# Feature Specification: PG Pivot — EC Söküm (merchant/onboarding + PG-bağlı ödeme yüzeyleri)

**Feature Branch**: `093-pg-pivot-teardown`

**Created**: 2026-10-20

**Status**: Draft

**Input**: PG banka-adaptör pivotu (2026-10-19): PG'de Merchant + Commission BC'leri ölüyor, iyzico
çıkıyor, yeni yön pay-by-link + webhook (kontrat henüz YOK). EC'deki tüm PG-bağlı yüzeyler sökülür;
ödeme yolu TAM SÖKÜM (kullanıcı kararı — stub reddedildi), `start_payment` "ödeme geçici kapalı" döner.
**Ödeme işlemleri sonraki geliştirmelerde düzeltilecek** — yeni pay-by-link kontratı ayrı feature
olarak gelir; bu söküm o geri dönüşü kolaylaştıracak çekirdeği (PaymentIntent, 092 koreografi,
event kontratları) bilinçli yerinde bırakır.

**Kademe**: Küçük (spec.md + tasks.md) — saf söküm: yeni aggregate/tablo/event/endpoint kontratı YOK;
kalan kontratlar (PaymentSucceeded/Failed, 092 koreografi) değişmiyor. Kullanıcı kararı bu oturumda.
Kapsam envanteri onaylı plan'da: `/Users/macbook/.claude/plans/s-k-me-ba-layal-m-kapsam-birlikte-steady-goose.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant/onboarding dünyası ortadan kalkar (Priority: P1)

Admin'in AI istemcisinde merchant onboarding tool'ları (başlat/durum/yeniden-anahtar) artık görünmez;
Customer BC merchant kaydı, PG S2S istemcisi ve credential callback ucu yok. Ölü PG'ye bağlanan hiçbir
kod kalmaz.

**Why this priority**: PG tarafında Merchant/Commission BC'leri öldü — bu yüzeyler çağrılsa da
çalışamaz; ölü kod + ölü sır makineleri (bootstrap key, callback secret) güvenlik ve bakım yüküdür.

**Independent Test**: Customer.Api `/mcp` tool listesinde merchant tool'u yok; solution build + tüm
testler yeşil; repoda PG onboarding'e giden HTTP istemcisi/endpoint grep'le bulunamaz.

**Acceptance Scenarios**:

1. **Given** admin MCP oturumu, **When** tool listesi alınır, **Then** `admin_start_onboarding` /
   `admin_onboarding_status` / `admin_get_merchant_status` / `admin_reissue_merchant_key` listede yoktur.
2. **Given** söküm sonrası kod tabanı, **When** `merchant` / `onboarding` / `DropShop` grep'lenir,
   **Then** Customer BC'de yalnız tarihsel spec/ADR referansları kalır (çalışan kod yok).
3. **Given** Customer.Api açılışı, **When** servis ayağa kalkar, **Then** merchant callback ucu
   (`/internal/merchant-registration/callback`) 404'tür ve açılışta onboarding config aranmaz.

---

### User Story 2 - Ödeme başlatma geçici kapalı, sipariş bütünlüğü korunur (Priority: P1)

Müşteri chat'ten `start_payment` çağırınca sistem net "ödeme geçici kapalı" hatası döner; yarım
Pending sipariş YARATILMAZ. PG'ye hosted link isteyen, callback bekleyen, MerchantKey çözen hiçbir
parça kalmaz. Kapalılık GEÇİCİDİR: ödeme, yeni PG pay-by-link kontratıyla sonraki feature'da geri gelir.

**Why this priority**: PG hosted-payment ucu ölünce bu zincir zaten kırık doğar; kullanıcıya
belirsiz hata yerine bilinçli kapalı-bildirimi + veri kirliliği önleme.

**Independent Test**: `start_payment` MCP çağrısı `PAYMENT_DISABLED` kodlu hata döner; orderDb'de
yeni Pending sipariş oluşmaz.

**Acceptance Scenarios**:

1. **Given** dolu sepetli login kullanıcı, **When** `start_payment` çağrılır, **Then** sonuç
   başarısızdır, hata kodu `PAYMENT_DISABLED`'dır ve yeni sipariş kaydı oluşmaz.
2. **Given** söküm sonrası Payment.Api, **When** servis ayağa kalkar, **Then** PG callback ucu yoktur
   ve PG base-url/callback-secret config'i aranmaz (fail-fast yok).
3. **Given** geçmişte oluşmuş ödeme kayıtları, **When** kullanıcı ödeme geçmişini sorar
   (`get_all_payments`), **Then** mevcut kayıtlar okunur döner (veri silinmez).

---

### User Story 3 - Checkout koreografisi uykuda ama bozulmadan kalır (Priority: P2)

092 zinciri (PaymentSucceeded → StockCommitRequested → StockCommitted → Order.Confirm →
OrderCompleted) kontrat ve tüketicileriyle AYNEN kalır; yalnız tetikleyicisi (gerçek ödeme) yoktur.
Yeni PG pay-by-link kontratı geldiğinde zincir değişmeden yeniden canlanır.

**Why this priority**: Pivot sonrası yeniden kurulacak tek şey ödeme adaptörü olmalı; koreografiyi
de söküp geri yazmak israf ve risk. Ödemenin geri dönüşü planlı — dönüş maliyetini bu söküm belirler.

**Independent Test**: PaymentSucceeded/PaymentFailed kontratları, Order `PaymentConsumers` +
`StockConsumers`, Stock `OrderConsumers` dosyaları ve testleri değişmeden durur; build + test yeşil.

**Acceptance Scenarios**:

1. **Given** söküm sonrası kod tabanı, **When** PaymentSucceeded/PaymentFailed kontratlarına ve 092
   tüketicilerine bakılır, **Then** hiçbiri değişmemiştir.

---

### User Story 4 - Platform fasadı ölü PG downstream'lerini bırakır (Priority: P2)

AgentPlatform MCP fasadı pg-merchant/pg-commission'a bağlanmayı denemez; merchant.*/commission.*
scope'ları fasat ilanından ve EC scope registry'sinden çıkar.

**Why this priority**: Ölü downstream açılışta bağlantı denemesi/log gürültüsü üretir; ölü scope
ilanı istemcilere var olmayan yetki vaat eder.

**Independent Test**: AgentPlatform Mcp.Gateway testleri yeşil; fasat açılış logunda pg-* downstream
denemesi yok.

**Acceptance Scenarios**:

1. **Given** fasat config'i, **When** fasat ayağa kalkar, **Then** downstream listesinde pg-merchant /
   pg-commission yoktur ve scope ilanında merchant.*/commission.* yoktur.

---

### Edge Cases

- `start_payment` anonim/boş-sepet çağrıları: mevcut guard'lar korunur; kapalı-bildirimi sepet
  kontrolünden ÖNCE mi sonra mı? → ÖNCE (en ucuz kısa devre; sepet okuma da gereksiz iş).
- Eski DB'de duran MerchantInformation dokümanları: dokunulmaz (Marten şeması geriye-uyumlu; tablo
  temizliği kapsam dışı, dev full-reset zaten siler).
- Canlı Pending PaymentIntent kalıntısı: expiry süreci sökülünce süresiz Pending kalabilir → kabul
  (dev ortamı; yeni kontrat gelince yeniden modellenir).
- Claude Desktop'ta kayıtlı eski admin oturumu merchant tool'unu çağırırsa: tool bulunamaz hatası
  (MCP istemcisi tool listesini yeniler) — sunucu tarafında ek savunma gerekmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Customer BC merchant/onboarding yüzeyi KOMPLE kalkar: MerchantInformations domain'i,
  Onboarding S2S istemci katmanı, credential callback ucu + imza doğrulayıcı, MerchantKey gRPC servisi,
  4 admin MCP tool + Shared ad/description sabitleri, ilgili config section'ları ve testler.
- **FR-002**: Payment BC'nin PG'ye bakan tüm parçaları kalkar: hosted-payment istemcisi, HMAC callback
  zinciri (command + endpoint + validator), MerchantKey istemci zinciri (gRPC client + m2m token
  handler), Order→Payment intent gRPC servisi + proto, terk-timer süreci, PG config alanları.
- **FR-003**: PaymentIntent aggregate'i, testleri ve kullanıcı ödeme-geçmişi sorgusu KALIR; mevcut
  ödeme verisi silinmez ve okunur kalır. Gerekçe: ödeme sonraki geliştirmede geri gelecek, çekirdek
  durum makinesi yeniden kullanılacak.
- **FR-004**: `start_payment` tool'u adı/description'ı korunarak kısa devre olur: `PAYMENT_DISABLED`
  resource-kodlu hata döner (mesajı ödemenin GEÇİCİ kapalı olduğunu söyler), Pending sipariş yaratmaz,
  Payment'a hiçbir çağrı yapmaz. Order→Payment gRPC istemcisi ve ilgili m2m config'i kalkar.
- **FR-005**: PaymentSucceeded/PaymentFailed kontratları ve 092 checkout koreografisi (Order/Stock/
  Reviews/Library/Basket tüketicileri) DEĞİŞMEDEN kalır.
- **FR-006**: `merchant.*`, `commission.*` ve `merchant.credentials.write` scope sabitleri EC scope
  registry'sinden kalkar; hiçbir `[RequiredScope]`/auth kaydı bunlara referans vermez.
- **FR-007**: Domain süreci belgeleri aynı PR'da güncellenir (İlke VII): customer FLOW.md merchant
  süreci silinir; payment + order FLOW.md ödeme adımları söküm-sonrası gerçeği anlatır ve "yeni PG
  pay-by-link kontratı bekleniyor — ödeme burada geri açılacak" open hotspot taşır. CLAUDE.md BC
  haritası + yüzey modeli ve README hizalanır.
- **FR-008**: AgentPlatform fasadında pg-merchant/pg-commission downstream kayıtları ve
  merchant/commission scope ilanları kalkar; fasat testleri güncellenir (ayrı repo, ayrı commit).
- **FR-009**: `specs/077`, `specs/078`, `specs/087` tarihsel artefakt olarak DURUR (silinmez);
  CLAUDE.md BC haritası Origin sütunu guard'ı (`check-claude-spec-links.sh`) geçer.

### Key Entities

- **MerchantInformation** (Customer BC): SİLİNİR — kod + yüzey; DB'deki eski dokümanlar dokunulmaz.
- **PaymentIntent** (Payment BC): KALIR — durum makinesi PG-bağımsız; sonraki pay-by-link feature'ı
  yeniden kullanacak.
- **Order** (Order BC): davranış değişmez; yalnız ödeme başlatma girişi kapalı-bildirimine kısa devre.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Solution build 0 hata; TÜM test projeleri (söküm dokunulan Customer/Payment/Order dahil)
  yeşil; `check-claude-spec-links.sh` + `check-flow-links.sh` geçer.
- **SC-002**: Admin MCP tool listesinde merchant tool sayısı 0; müşteri tool listesi söküm öncesiyle
  aynı (start_payment dahil — davranışı kapalı-bildirimi).
- **SC-003**: `start_payment` çağrısı kapalı-bildirimi döner ve orderDb'de yeni sipariş satırı oluşmaz
  (elle Aspire doğrulaması).
- **SC-004**: EC + AgentPlatform repo'larında çalışan kod içinde PG merchant/commission/hosted-payment
  referansı kalmaz (tarihsel specs/ADR hariç).
- **SC-005**: 092 koreografi dosyaları diff'te görünmez (kontrat/consumer değişikliği sıfır).

## Assumptions

- **Ödeme geri dönüşü planlı:** yeni PG pay-by-link + webhook kontratı SONRAKİ geliştirmede ayrı
  spec'le gelir; bu söküm onu beklemez, ona ön-karar dayatmaz ve geri dönüş çekirdeğini (PaymentIntent,
  092 koreografi, event kontratları) yerinde bırakır.
- Checkout'un uçtan uca test edilemez kalması bilinçli ve GEÇİCİ kabul (kullanıcı kararı, bu oturum).
- PG repo'sundaki (DropShop) söküm bu spec'in dışı; EC yalnız kendi tarafını temizler.
- Order `Options/SagaAuth.cs` ve Payment `Options/SagaAuth.cs` yalnız sökülecek m2m çağrılar içinse
  kalkar; başka tüketicisi çıkarsa (implement sırasında build doğrular) kalır.
- `IntentTimeoutSeconds` gibi PG-yan config alanları tüketicisiz kalırsa alanla birlikte temizlenir.
- AgentPlatform değişikliği aynı iş paketi, ayrı repo + ayrı commit; EC PR'ına girmez.