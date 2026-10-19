---
status: Kabul
---

# ADR + Öğrenme Notu: Checkout — Orchestration'dan Choreography'ye (092)

> **Gerekçe + öğrenme katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + `specs/092-checkout-choreography`.
> İlgili: [adr-checkout-saga-orchestration](adr-checkout-saga-orchestration.md) (yerini aldığı karar),
> [adr-bounded-context-per-service](adr-bounded-context-per-service.md).

**Durum:** Kabul · **Feature:** 092-checkout-choreography · **Anayasa:** İlke I v1.12.2 (örnek uykuda)

---

## 1. Karar — tek cümle

**Checkout artık ayrı bir orkestratör saga'yla (`Checkout.Orchestrator`/`CheckoutProcess`) değil, servisler-
arası event tepkileriyle (choreography) yürür.** `Checkout.Orchestrator` BC'si + `checkoutDb` söküldü;
yerine üç fanout event (`StockCommitRequested`/`StockCommitted`/`StockCommitFailed`) + mevcut `OrderCompleted`
geldi. Bu, 028/049'un orchestration kararını **bilinçle tersine çevirir**.

## 2. Neden tersine çevirdik

028/049 orchestration'ı seçmişti; ADR'si (§2 tablosu) orchestration'ı "çok adım, telafi, timeout, dallanma"
için, choreography'yi "az adım, basit ileri akış" için öneriyordu. Araya giren iki gerçek dengeyi değiştirdi:

- **Ödeme saga'dan ÖNCE kapatıldı (077 hosted-CF).** Dağıtık işlemin zor kısmı (para↔mal) saga başlamadan
  çözülmüştü. Saga'ya kalan: stok düş → order confirm → sepet sil — hepsi **tek-BC atomik**, az adım, ileri akış.
- **Saga karmaşıklığının tek kaynağı kalem-kalem stok commit'ti.** `NextIndex` döngüsü + `CommittedItems`
  LIFO telafisi + 5 faz + watchdog, yalnız stok her kalemi ayrı commit ettiği için vardı. Stok commit **tek
  atomik batch**'e çevrilince (Stock N `ProductStock`'u tek Marten transaction'ında işler — biri yetersizse
  hiçbiri yazılmaz) telafi edilecek kısmi durum kalmaz: LIFO + fazlar + watchdog hep birden gereksizleşir.

Yani profil 028/049'un kendi ADR'sindeki choreography satırına kaydı. Ek olarak 028 ADR §8 zaten "ayrı
orkestrasyon servisi = anti-pattern" diyordu; 049 bunu bir istisna olarak açmıştı — 092 istisnayı kapatır.

## 3. Yeni akış (choreography)

```
PaymentSucceeded (Payment, fanout)
  → Order.PaymentConsumers: StockCommitRequested(OrderId, UserId, Items) yayar
  → Stock.OrderConsumers: TÜM kalemleri tek tx atomik Commit
        başarı → StockCommitted(OrderId)        yetersiz → StockCommitFailed(OrderId, STOCK_INSUFFICIENT)
  → Order.StockConsumers: StockCommitted → Order.Confirm() → OrderCompleted (Reviews + Library + Basket)
                          StockCommitFailed → Order.Cancel(reason) + manuel-iade uyarısı
  → Basket.OrderConsumers: OrderCompleted → sepeti sil (idempotent)
```

Tüm event'ler fanout; binding'i tüketici kurar (007 soğuk-açılış dersi). Korelasyon = OrderId. Her tüketim
idempotent: `Commit` orderId-anahtarlı, `Confirm/Cancel` durum-guard'lı, sepet silme idempotent.

## 4. Watchdog kaldırıldı — "her bekleyişin timeout dalı" ne oldu?

028 ADR §3 "saga'da süresiz bekleyen durum olamaz — her bekleyen durumun timeout dalı vardır" diyordu.
Choreography'de merkezi bekleyen state yok; bekleyiş = bir event'in durable teslimi. Takılma güvencesi:

- **Teknik hata** (DB/RabbitMQ erişilemez) → Wolverine retry merdiveni → tükenirse **DLQ (088)**; operatör
  `admin_list_dead_letters`/`admin_replay_dead_letter` ile görür + yeniden sürer.
- **İş hatası** (yetersiz stok) → `StockCommitFailed` event → `Order.Cancel` (retry anlamsız).
- **Takılı sipariş** `Pending` kalır — zombi değil, sorgulanabilir gerçek.

Watchdog'un verdiği "en-geç karar" garantisinden bilinçle feragat edildi; karşılığı DLQ görünürlüğü + sade
akış. Kullanıcı kararı (092 tasarım oturumu).

## 5. Kabul edilen borç — "ödendi ama stok yok" iadesi

Rezervasyonsuz (056) + ödeme-önden modelde iki eşzamanlı checkout son birimi kapışırsa biri ödedikten sonra
stok yetersiz çıkabilir. Bu durumda sipariş iptal edilir + uyarı loglanır; **otomatik iade bu feature'ın
kapsamı DIŞI** (bugünkü 049 davranışıyla aynı — orası da "external refund out of scope" diyordu). Otomatik
iade ayrı bir feature/backlog. Bu, modelin bilinen ve kabul edilen riskidir, yeni bir regresyon değil.

## 6. Anayasa etkileşimi

İlke I (v1.11.0) "orkestre saga broker komut/yanıtı"nı meşru kanal olarak eklemiş ve **049 Checkout.
Orchestrator'ı canlı örnek** göstermişti. 092 bu örneği uykuya düşürür (repoda başka orkestre-saga yok).
İlke KALDIRILMADI — choreography zaten İlke I'in birincil kanalı olan fanout integration event'i kullanır
(tam uyum). Yalnız somut örnek "uykuda" işaretlendi (telemetri v1.12.0 + capability-link v1.12.1 "örneksiz
uyku" emsali). Gelecekte gerçekten çok-adımlı/dallanan bir süreç doğarsa orchestration kanalı yine meşrudur.

## 7. Mülakat soru-cevap kartları

- **"Orchestration'ı neden söktün?"** → Ödeme öne alınınca (077) saga'ya kalan iş az-adım/tek-BC-atomik oldu;
  kalem-kalem commit'i atomik batch'e çevirince telafi/watchdog gereksizleşti → choreography profili.
- **"Atomiklik 2PC değil mi?"** → Hayır; tek BC (Stock) içinde tek Marten transaction'ı — yerel ACID,
  dağıtık koordinasyon yok. Dağıtık kısım (ödeme) zaten saga dışında, önceden çözülmüş.
- **"Telafi (LIFO) nereye gitti?"** → Atomik batch'te başarısızlık = sıfır commit → geri alınacak şey yok.
- **"Takılırsa?"** → Durable mesaj + retry + DLQ (088) + Pending sipariş sorgusu; merkezi watchdog yerine
  BC-başına ölü-mesaj görünürlüğü.
- **"Choreography'nin riski?"** → Akış tek dosyada değil üç BC'ye dağılır; izlenebilirlik event log + sipariş
  durumu + DLQ ile sağlanır. Az-adım olduğu için emergent-karmaşıklık riski düşük.
