# Feature Specification: MerchantId Görünürlük Politikası

**Feature Branch**: `087-merchant-id-visibility`

**Created**: 2026-10-04

**Status**: Draft

**Input**: Teknik borç S4 (kullanıcı için en kritik). MerchantId/MerchantKey yüzeylerde ham açığa çıkıyordu: (1) store credential ekranı `/merchant-credentials/{token}` ikisini elle ister; (2) PG reveal sayfası ikisini birlikte gösterir. (Üçüncü yüzey — komisyon GUID'i sohbette — komisyon sabitlenerek konusuz kaldı, bkz. Clarifications.) Karar (kullanıcı, 2026-10-04): hassas veri insan-yüzeyde hiç render edilmez; kayıt store-başlatır (ECommerce→PG) S2S/gRPC, credential asenkron HMAC-callback'le döner, store-içi kullanım gRPC; LLM yalnız sır-olmayan tutamaç görür. Dayanak ADR: `adr-mcp-control-plane-no-secret-return`.

## Clarifications

### Session 2026-10-04

- Q: Makine-handoff yönü + transport? → A: Store-başlatır — admin MCP aksiyonu tetikler, ECommerce kayıt verisini + bootstrap key'i sunucuda okur, PG'ye S2S/gRPC isteği atar. Yeni PG→store senkron inbound yok; dönüş asenkron callback.
- Q: Kayıt-öncesi (bootstrap) store→PG çağrısını ne doğrular (henüz MerchantKey yok)? → A: Önceden-paylaşılan **ortak bootstrap kayıt key'i** — ops PG'de bir kez elle üretir, ECommerce secret store'da tutar; platform-seviyesi (merchant başına değil), yalnız kayıt ucunu açar, dar kapsam, rotate edilebilir.
- Q: Bootstrap ortaksa kimlik nasıl ayrışır? → A: İki katman — bootstrap (ortak, "kapıyı çal") yalnız çağrının meşruluğunu der; PG onayda her merchant'a **kendine özel MerchantKey** basar (kimlik+yetki). Onay PG admininde (MCP) → onaysız aktivasyon yok.
- Q: Kayıt payload'ında finansal alan (IBAN/banka) var mı → giden veri de korunur mu? → A: Evet, finansal alan var → giden veri MCP arg'ından DEĞİL, S2S/gRPC gövdesinden gider (sunucuda okunur).
- Q: Onaydan sonra MerchantId+Key store'a nasıl aktarılır? → A: ECommerce kayıtta **CallbackUrl + correlation token** gönderir; PG onayda credential'ı o URL'e **`CallbackSecret` ile HMAC-imzalı** POST eder (077 emsali); ECommerce imza+correlation doğrular → persist → gRPC kullanır. Approve tool'u credential DÖNDÜRMEZ, yalnız statü + sunucu-tarafı callback tetikler.
- Q: PG kendi MCP yüzeyinde merchant verisini göstermek isterse? → A: Aynı invariant PG tarafında da — PG MCP tool'ları hassas alanı (finansal + credential) dönüşte VERMEZ; tam değer PG sunucusunda kalır.
- Q: US2 (komisyonda GUID'siz merchant seçimi) 087'de mi? → A: Çıkarıldı. Komisyon şimdilik PG-tarafı **sabit değer**, ECommerce'e bildirilmez; merchant-başına tanımlama akışı YOK → komisyon GUID-sohbet yüzeyi konusuz. Değişken/per-merchant komisyon gelince ayrı spec (D5/D6).
- Q: 078 elle-giriş ekranı ne kadar sökülsün? → A: **Tam sök** — `/merchant-credentials/{token}` endpoint + `CredentialEntrySession` + `SubmitMerchantCredentials` + `AdminRequestCredentialEntryLink` tool + gömülü HTML silinir. Reissue (`AdminReissueMerchantKey`) de ekransız: PG yeni key'i aynı HMAC-callback yoluyla teslim eder.

Üç ayrı sır (ayrı ömür): **bootstrap kayıt key** (ortak, kayıt çağrısı) ≠ **MerchantKey** (per-merchant, ödeme) ≠ **CallbackSecret** (PG→store callback).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant credential'ı hiç ellemez (makine teslim) (Priority: P1)

Merchant onboarding'de MerchantId/MerchantKey'i **elle girmez, hiçbir ekranda görmez**. PG credential'ı ürettikten sonra store'a **S2S (makine-handoff)** ile iletir; store kaydeder ve ödeme yolunda gRPC ile içeride kullanır. Merchant'ın sırrı gözüyle görmesi/kopyalaması gerekmez.

**Why this priority**: Kullanıcının "en kritik" dediği rahatsızlığın kökünü keser — sır hiçbir insan-yüzeyde yoksa karışma/yanlış paylaşım/phishing yüzeyi de yok.

**Independent Test**: Onboarding tamamlandığında store `MerchantInformation`'da MerchantId+Key dolu; hiçbir ekran/MCP tool dönüşü bu değerleri döndürmüyor; ödeme S2S yolu gRPC'den key'i çekip çalışıyor.

**Acceptance Scenarios**:

1. **Given** store kayıt + CallbackUrl gönderdi ve PG admin onayladı, **When** PG HMAC-imzalı callback'i POST eder, **Then** store imza+correlation doğrular, MerchantId+Key'i persist eder ve hiçbir insan ekranı/chat transkriptine değer düşmez.
2. **Given** kayıtlı credential, **When** ödeme akışı çalışır, **Then** store key'i gRPC ile içeride çeker (`X-Api-Key`), değer hiçbir agent/tool dönüşüne girmez.

---

### User Story 2 - Politika tek yerde yazılı (Priority: P2)

Geliştirici/operatör "MerchantId/Key insana nasıl davranır?" kuralını tek kanonik yerde bulur; her yeni merchant/sır-gösteren yüzey bu kurala uyar.

**Why this priority**: Tekrar driftini önler; kural kalıcı referans.

**Independent Test**: Dokümanda (ADR + conventions/CLAUDE.md) "hassas veri insan-yüzeyde render YOK, teslim S2S, kullanım gRPC, LLM yalnız opak tutamaç" yazılı mı.

**Acceptance Scenarios**:

1. **Given** yeni bir merchant-veri yüzeyi, **When** geliştirici politikayı okur, **Then** değeri nasıl ele alacağı (render etme, S2S taşı, gRPC kullan) tek kuraldan bellidir.

---

### Edge Cases

- Kayıt/callback başarısız olursa (store→PG veya PG→store callback hata) credential eksik kalır → fail-closed + retry; merchant'a elle-giriş fallback'i YOK (bilinçli — elle giriş sırrı ekrana sokar).
- Callback gecikir/hiç gelmezse (PG onay sarkar) store PENDING'de bekler; correlation token ile eşleşmeyen/çift callback idempotent yutulur.
- Bir tool handler'ı gRPC reply objesini ham döndürürse sır sızar → dönüş DTO'su sır/PII-free olmalı (ADR nöbetçi kural 1).
- İki repoda (ECommerceAgent store + AgentPlatform PG) politika tutarsız uygulanırsa drift — tek politika, iki uygulama.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: MerchantId ve MerchantKey hiçbir insan-yüzeyinde (web ekranı, chat/MCP tool dönüşü) render EDİLMEMELİ. Tam değer yalnız sunucu-tutar; LLM-yüzeyinde hiç bulunmamalı.
- **FR-002**: Merchant kaydı **store-başlatır**: admin MCP aksiyonu tetikler → ECommerce kayıt verisini (finansal dahil) + bootstrap key'i sunucuda okur → PG'ye S2S/gRPC gövdesinde gönderir. Finansal veri MCP arg'ına girmez. Mevcut 078 elle-giriş ekranı TAM sökülmeli: `/merchant-credentials/{token}` endpoint + `CredentialEntrySession` + `SubmitMerchantCredentials` + `AdminRequestCredentialEntryLink` + gömülü HTML. Reissue de ekransız (yeni key HMAC-callback'le gelir).
- **FR-003**: Kayıt çağrısı **ortak bootstrap kayıt key'i** ile doğrulanmalı (önceden-paylaşılan, secret store; merchant başına değil, platform-seviyesi; yalnız kayıt ucunu açar).
- **FR-004**: Credential dönüşü **asenkron callback** ile olmalı: ECommerce kayıtta CallbackUrl + correlation token gönderir; PG onayda MerchantId+Key'i o URL'e **`CallbackSecret` ile HMAC-imzalı** POST eder; ECommerce imza+correlation doğrular, persist eder. CallbackSecret bootstrap key ve MerchantKey'den ayrı sır.
- **FR-005**: Merchant onayı PG MCP'sinde yapılmalı; approve tool'u credential'ı DÖNDÜRMEMELİ (yalnız statü değişir + sunucu-tarafı callback tetiklenir). Onaysız aktivasyon olmamalı.
- **FR-006**: Store, kayıtlı MerchantKey'i ödeme/doğrulama/reissue yolunda **gRPC/S2S** ile içeride kullanmalı; değer hiçbir agent/tool dönüşüne veya log/trace'e girmemeli.
- **FR-007**: PG MCP tool'ları merchant verisini gösterirken hassas alanları (finansal + credential) dönüşte VERMEMELİ; yalnız hassas-olmayan alanlar.
- **FR-008**: MerchantId/Key ele-alma politikası (render YOK, kayıt S2S, dönüş HMAC-callback, kullanım gRPC, LLM yalnız opak tutamaç) tek kanonik dokümanda yazılı olmalı.
- **FR-009**: Politika iki repoda (ECommerceAgent store yüzeyi + AgentPlatform PG yüzeyi) tutarlı uygulanmalı.
- **FR-010**: Komisyon şimdilik PG-tarafı **sabit değer**; merchant-başına komisyon tanımlama akışı ve komisyonun ECommerce'e bildirimi 087 kapsamı DIŞI (değişken komisyon gelince ayrı spec).

### Key Entities

- **MerchantId**: Merchant'ı tanımlayan değer (GUID). İnsan-yüzeyde render YOK; sunucu-tutar + S2S/gRPC taşınır.
- **MerchantKey**: Merchant S2S sırrı (per-merchant, PG onayda basar); her zaman gizli, yalnız sunucu belleği + gRPC yolunda.
- **Bootstrap kayıt key'i**: Ortak, platform-seviyesi önceden-paylaşılan sır; yalnız store→PG kayıt çağrısını doğrular. Secret store'da, out-of-band kurulur, rotate edilebilir.
- **CallbackSecret**: PG→store asenkron callback HMAC imzasını doğrulayan ayrı sır.
- **Correlation token**: Kayıt ile callback'i eşleştiren, sır-olmayan ilişki tutamacı.
- **Sır-olmayan tutamaç**: LLM/chat'in merchant'a atıfta kullandığı değer (e-posta/ad/opak token); sistem sunucuda MerchantId/Key'e çözer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Onboarding'den ödemeye tüm akışta MerchantId/MerchantKey hiçbir insan ekranında veya chat transkriptinde görünmez (denetim: 0 render noktası).
- **SC-002**: Politika tek dokümandan okunabilir; her iki repodaki merchant-veri yüzeyi aynı kurala (render YOK / S2S / gRPC) uyar.

## Assumptions

- **Karar (kullanıcı, 2026-10-04):** makine-handoff + gRPC yönü seçildi; elle-giriş ekranı fallback'i YOK. Dayanak [[adr-mcp-control-plane-no-secret-return]].
- MerchantKey gizliliği (sunucu-tutar) mevcut `MerchantKeyClient` gRPC deseniyle hizalı; ödeme S2S yolu (`X-Api-Key`) değişmez.
- 078 elle-giriş ekranı ([[adr-credential-link-forward-protection]]) emekli olur; forward/sub-kıyası borcu bu yönle konusuz kalır (sır artık ekrana hiç girmez).
- Komisyon şimdilik sabit (PG-tarafı); merchant-başına tanımlama + GUID-arama (D5/D6) 087 dışı, değişken komisyon gelince ayrı spec.
- Tek politika spec'i; uygulama ECommerceAgent (store kayıt-başlatan + callback alıcı + gRPC kullanım) + AgentPlatform (PG kayıt alıcı + onay-MCP + HMAC callback göndereni) olarak ikiye bölünür.