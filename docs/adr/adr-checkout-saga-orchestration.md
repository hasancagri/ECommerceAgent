---
status: Kabul
---

# ADR + Öğrenme Notu: Checkout Saga — Orchestration (028)

> **Gerekçe + öğrenme katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + `specs/028-checkout-saga`.
> İlgili: [adr-bounded-context-per-service](adr-bounded-context-per-service.md).

> ⚠️ **HOST TAŞINDI (049):** Bu notun anlattığı `CheckoutSaga` **Order.Api içinde host** modeli 049'da
> **full-replace** ile söküldü — saga artık ayrı `checkout` BC'sinde (`Checkout.Orchestrator/Sagas/
> CheckoutProcess.cs`, broker-only). Aşağıdaki saga deseni/öğrenme içeriği geçerli; "Order.Api'nin içinde,
> yeni deployable YOK" iddiası artık YANLIŞ (yeni deployable VAR). Güncel host detayı = §4 notu (bu dosya).

**Durum:** Saga deseni yürürlükte, HOST 049'da ayrı BC'ye taşındı · **Feature:** 028-checkout-saga · **PR:** #46 (bd19667, 2026-08-05) · **Anayasa:** v1.4.0 amendment

---

## 1. Saga nedir — tek cümle

**Saga = dağıtık bir iş sürecini, her adımı lokal transaction olan ve başarısızlıkta telafi
(compensating) adımlarıyla geri sarılan bir adım zinciri olarak yürütme deseni.**

Neden var: mikroservislerde **dağıtık transaction (2PC) yok**. Her BC kendi DB'sine yazar;
"siparişi yaz + stoğu düşür + sepeti sil" tek ACID transaction olamaz. Saga bunu şöyle çözer:
her adım kendi transaction'ı; bir adım patlarsa öncekiler **telafi** ile geri alınır.
ACID'in "A"sı yerine **eventual consistency + telafi garantisi** koyarsın.

## 2. İki stil — ve bizim seçim

| | Choreography | Orchestration (BİZİM SEÇİM) |
|---|---|---|
| Kontrol | Merkez yok; her servis event'e tepki verir | Süreç sahibi BC'de merkezi durum makinesi |
| Akışın okunması | 3+ servisin koduna/loglarına dağılır | Tek dosyada (`CheckoutSaga.cs`) |
| Teknik kurulum | Kolay (fanout event zaten var) | Saga state + persistence + mesaj zinciri |
| Mimari maliyet | Dağınıklık, döngüsel event riski | Merkezileşme; orchestrator'ın BC'si şişebilir |
| Ne zaman | Az adım, basit ileri akış | Çok adım, telafi, timeout, dallanma |

**Kritik ayrım (mülakat cümlesi):** *"Orchestration merkezi bir **karar noktası** gerektirir,
merkezi bir **servis** değil."* Ayrı OrchestrationService = god-service anti-pattern (2000'ler SOA,
distributed monolith). Doğrusu: **saga, sürecin sahibi olan BC'de yaşar** — checkout'un sahibi
Order BC, `CheckoutSaga` Order.Api'nin içinde. Yeni deployable YOK.

İkinci ayrım: **"bütün süreçleri" değil "BİR süreci" merkezileştirir.** Her saga tek sürecin
beynidir. Payment saga'sı gelirse o AYRI bir saga sınıfı olur; ortak olan yalnız altyapı
(Wolverine motoru + Marten persistence).

Üçüncü ayrım: adım servisleri **süreçten habersizdir**. Stock "checkout diye bir süreç var"
bilmez; `Commit`/`RevertCommit` komutlarını işler. Süreç bilgisi %100 saga'da.

## 3. Saga'yı saga yapan üç bileşen (enum yetmez!)

1. **Kalıcı state (enum + VERİ):** `Phase` (neredeyim) tek başına anlamsız; karar için veri gerekir:
   `Items` (ne yapılacaktı), `NextIndex` (nerede kaldım), `CommittedItems` (**neyi geri alacağım**),
   `Attempt` (kaç kez denedim). Telafi listesi olmadan "iptal et" diyemezsin.
2. **Mesaj-güdümlü ilerleme:** for-döngüsü değil; her adım bir mesajın işlenmesi. State değişimi +
   sonraki mesaj **aynı transaction'da** (Marten + Wolverine outbox). Crash → mesaj + state DB'de →
   restart'ta kaldığı yerden. RAM'de enum döndüren sınıf bunların hiçbirini veremez.
3. **Her bekleyişin istisna dalı:** iş hatası → telafi; teknik hata → sınırlı retry; cevap yok →
   watchdog. *"Saga'da süresiz bekleyen durum olamaz — her bekleyen durumun timeout dalı vardır."*

Ayrıca: **OrderStatus ≠ saga Phase.** OrderStatus domain gerçeğidir (müşteri görür, kalıcı);
Phase süreç muhasebesidir (geçici, saga bitince belgesiyle silinir). İkisini karıştırmamak önemli.

## 4. Bizim akış (CheckoutSaga.cs)

> Bugünkü host: ayrı `Checkout.Orchestrator` BC (049) — `Sagas/CheckoutProcess.cs`; Order.Api'de saga YOK (yalnız gRPC/consumer). Ayrıca 077: ödeme öncedendir, Charge adımı söküldü.

```
POST /orders
  CreateOrder handler [Transactional]:
    Order doğar: Status=Pending (PaymentId idempotency korunur)
    StartCheckout yayınlanır (outbox: sipariş kaydıyla ATOMİK)
    HTTP HEMEN döner (97 ms canlıda) → UI "Beklemede" rozeti

CheckoutSaga (state Marten belgesi, Id=OrderId):
  Start: CommitNextItem yayınla + watchdog kur (ScheduleAsync, 120sn config)
  CommitNextItem: TEK kalem gRPC StockCommit
     OK → CommittedItems'a ekle, sonraki kalem (kalem başına mesaj!)
     son kalem OK → Phase=ClearingBasket → ClearBasketStep
     iş hatası → retry YOK → CompensateCheckout
     teknik hata → Attempt<3 → 5sn gecikmeli kendine mesaj
  CompensateCheckout: kalem başına TEK gRPC RevertCommit (LIFO)
     bitince → Order.Cancel(sebep) + MarkCompleted
     revert de düşerse → CompensationFailed + LogCritical (manuel müdahale)
  ClearBasketStep: Order.Confirm() ÖNCE yazılır, sonra gRPC ClearBasket
     fail → 3 retry → yine fail → LOG + tamamlan (sipariş Confirmed KALIR)
  CheckoutTimedOut (watchdog): bitmemişse telafi+Cancel(ORDER_TIMEOUT);
     bitmişse NotFound → no-op
```

### Pivot kavramı (mülakat altını)

Saga adımları ikiye ayrılır: **compensatable** (pivot öncesi — geri alınabilir: stok commit)
ve **retryable** (pivot sonrası — yalnız ileri gider: sepet temizliği). **Pivot = Confirm.**
Pivot geçildikten sonra sipariş ASLA iptal edilmez; sepet temizliği düşerse retry + log, o kadar.
Müşterinin parası/siparişi bir UI pürüzüne feda edilmez (FR-009, canlıda kanıtlandı: S4).

### Neden kalem başına TEK mesaj (for-döngüsü değil)?

Crash noktası ne olursa olsun belirsizlik en fazla tek kalem. Her kalemin sonucu state'e yazılıp
persist edilir; restart'ta `NextIndex`'ten devam. Tek handler'da for-döngüsü olsaydı "hangi
kalemler commit edildi?" sorusunun cevabı RAM'le birlikte kaybolurdu.

## 5. Zor problemler ve çözümleri (işin asıl eti)

### a) At-least-once teslimat → idempotency anahtarı

Mesajlar **en az bir kez** teslim edilir; aynı adım iki kez koşabilir. Anahtarsız `Commit` tekrarı
`NO_ACTIVE_RESERVATION` döner ve "zaten yapıldı" ile "rezervasyon gerçekten yok" AYIRT EDİLEMEZ →
yanlış telafi → stok kaçağı. Çözüm: proto'ya `order_id`; `ProductStock` işlenmiş operasyon
anahtarlarını tutar (`"orderId:commit"`, `"orderId:revert"`, bounded 100). Mükerrer çağrı no-op OK.
Ek kural: `RevertCommit` yalnız commit anahtarı VARSA çalışır (kaçak artış engeli).

### b) Arka planda yetki: kullanıcı token'ı YOK

Saga HttpContext dışında koşar; `BearerForwardingHandler` çalışamaz. Kullanıcı token'ını saga
state'ine koymak REDDEDİLDİ: uzun kesintide restart sonrası token expire → telafi 401 alır →
stok tutarsız kalır. Çözüm: **client-credentials makine istemcisi** `order-saga`
(scope: `stock.reserve` + `basket.write`) + `SagaTokenHandler` (static cache'li token).
Kullanıcı kimliği yetki için değil, VERİ olarak taşınır (UserId komut gövdesinde).

### c) Watchdog = duvar saati değil, "en geç" garantisi

`ScheduleAsync` ile kurulan mesaj **Postgres'te bir satırdır** (RAM timer değil). Uygulama kapansa
da kaybolmaz; restart'ta vadesi geçmişse teslim edilir. Garanti: *"karar, sistem tekrar
ayaktayken en geç verilir"* — 10 dk kapalı kaldıysa karar 10. dakikada ama VERİLİR. Canlı kanıt
S3b: Order.Api ~2 dk ölü → restart → watchdog `ORDER_TIMEOUT` ile telafi+iptal; zombi Pending yok.

### d) İş hatası vs teknik hata ayrımı

Retry kararı iş kuralıdır: `STOCK_INSUFFICIENT` retry edilmez (tekrar denesen de yetersiz),
`UNAVAILABLE` 3×5sn denenir. Ayrım sinyali: gRPC yanıtı `Success=false+Code` = iş hatası;
RpcException/deadline = teknik (proxy Result'a çevirir, 012 deseni). Sayaç (`Attempt`) state'te —
restart'a dayanır.

### e) Mock'suz test edilebilirlik: saf karar çekirdeği

Repo kuralı: saf domain birim testi, mock kütüphanesi yok. Çözüm: karar mantığı saf
`On*` metotlarında (`OnCommitResult`, `OnRevertResult`, `OnClearBasketResult`, `OnTimeout`) —
girdi: sonuç nesnesi; çıktı: `NextStep` record (Message/Delay/CompleteSaga/CancelWithReason);
yan etki: state mutasyonu. Handler'lar ince: gRPC çağır → kararı uygula (`DispatchAsync`).
13 birim test bu çekirdeği vurur; gRPC/host/mock yok.

## 6. Anayasa etkileşimi

İlke I gRPC'yi "anlık evet/hayır kararı" için sanksiyonluyordu (v1.2.0, stok rezervasyonu).
Saga adım komutları (RevertCommit, ClearBasket) bunu aşıyordu → **v1.4.0 MINOR amendment**:
sanksiyon "orkestre edilmiş saga'ların adım/telafi komutları"nı da kapsar. DB izolasyonu ve
`Shared/Protos` kontrat kuralı değişmedi. Süreç: çelişen tasarım ya koda uyar ya amendment —
governance işledi.

Yan karar: `OrderCreatedEvent` tamamen SİLİNDİ (kullanıcı kararı: "tam orchestration, choreography
istemiyorum"). Sepet temizliği event tepkisi değil saga adımı. `ReservationExpired` DOKUNULMADI —
o saga değil, TTL yaşam döngüsü.

## 7. Canlı doğrulama kanıtları (2026-08-05, Playwright otomasyonu)

| Senaryo | Kanıt |
|---|---|
| S1 mutlu yol | 97ms yanıt; rozet Beklemede→Onaylandı; stok 7→6, 14→13; saga belgesi silindi |
| Watchdog no-op | 2dk sonra zarf ateşlendi; sipariş Onaylandı kaldı; dead letter 0 (FR-012) |
| S2 telafi | Aynı orderId ile `commit`+`revert` anahtarları; stok 6→5→6; sepet KORUNDU |
| S3a Stock kapalı | 3 retry ~50sn → `İptal+STOCK_COMMIT_UNAVAILABLE`; checkout yine anında döndü |
| S3b restart | Saga ortasında kill -9; restart'ta watchdog `ORDER_TIMEOUT` kararı; zombi yok |
| S4 pivot sonrası | Basket ölü; sipariş `Pending→Confirmed` KALDI; log `deneme 1/3..3/3 → retry tukendi` |

Öğrenilen test dersi: saga kalem sırası = sepete ekleniş sırası (`Items[0]` önce) — sabotaj
hedefini buna göre seçmek gerekti.

## 8. Mülakat soru-cevap kartları

- **"Saga nedir?"** → §1'deki tek cümle + "2PC yokluğunun cevabı".
- **"Orchestration mu choreography mi?"** → §2 tablo + "süreç başına merkezileştirme; god-service değil".
- **"Telafi de başarısız olursa?"** → CompensationFailed + alarm logu + manuel müdahale; sipariş yine
  Cancelled işaretlenir. (Sonsuz retry değil — insan devreye girer.)
- **"Mesaj iki kez gelirse?"** → idempotency anahtarı (§5a); "at-least-once + idempotent adım = effectively-once".
- **"Uygulama saga ortasında çökerse?"** → durable state + durable mesajlar + watchdog (§5c); S3b canlı kanıt.
- **"Saga'yı nerede host edersin?"** → sürecin sahibi BC'de; ayrı orchestration servisi anti-pattern.
- **"Pivot nedir?"** → compensatable/retryable ayrımı (§4); pivot sonrası iptal yasak.
- **"Saga vs 2PC?"** → 2PC bloklayan koordinatör + kilit; saga lokal transaction + telafi, kilit yok,
  eventual consistency. Mikroserviste 2PC pratikte ölü.
- **"Saga vs workflow engine (Temporal/Camunda)?"** → aynı ihtiyaç, motor ürünleşmiş hali; bizde
  Wolverine kütüphane olarak process içinde, durability Postgres'te.

## 9. Gelecek

- Payment feature'ı saga'ya adım olarak girecek (PSP çağrısı + webhook bekleyişi) — watchdog o gün
  "ödeme cevabını en fazla X bekle" anlamı kazanır, süre büyür. Bilgilendirme maili de o kapsamda.
- ~~Bulgu (028 dışı): Basket.Api kapalıyken header sepet sayacı ViewComponent'i TÜM WebApp
  sayfalarını asıyor (timeout yok) — resilience adayı.~~ **KONU DIŞI (2026-10-01):** WebApp
  2026-09-11'de SÖKÜLDÜ (agent-only pivot) — ViewComponent yok. Genel S2S resilience borcu ayrı backlog kalemi.
