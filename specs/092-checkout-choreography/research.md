# Phase 0 Research: Checkout Choreography

Tüm belirsizlikler oturumda çözüldü; bu dosya kararları + gerekçeyi kaydeder.

## D1 — Stok commit: kalem-kalem → atomik batch

- **Karar**: Stock, `StockCommitRequested`'teki TÜM kalemleri tek `IDocumentSession` ile yükler, her birine
  `ProductStock.Commit(qty, orderId)` çağırır, tek `SaveChangesAsync`. Biri yetersizse `SaveChanges` öncesi
  çıkar → hiçbiri yazılmaz.
- **Gerekçe**: Saga'daki `NextIndex` döngüsü + `CommittedItems` LIFO telafisi yalnız kalem-kalem commit
  olduğu için vardı. Atomik batch'te başarısızlık = sıfır commit → geri alınacak şey yok → LIFO ölür.
  Marten unit-of-work tek-BC'de ACID sağlar (dağıtık değil, 2PC gerekmez).
- **Alternatif**: Kalem-kalem korunup yalnız orkestratör silinsin (koreografik revert). Reddedildi:
  revert event zinciri saga karmaşıklığını BC'lere dağıtır; atomik batch onu tümden yok eder.

## D2 — Watchdog tamamen kaldırılır

- **Karar**: `CheckoutTimedOut` + `ScheduleAsync` watchdog YOK. Takılma güvencesi = Wolverine durable
  mesaj + retry merdiveni + DLQ (088) + sipariş durumu (`Pending`) sorgulanabilirliği.
- **Gerekçe**: Watchdog saga'nın "her bekleyişin timeout dalı" ihtiyacındandı (ADR §3). Koreografide merkezi
  bekleyen state yok; her adım durable mesaj, teknik hata retry+DLQ ile görünür. `Pending` sipariş zombi
  değil, sorgulanabilir gerçek. Kullanıcı kararı: ekstra timer tutma.
- **Alternatif**: Order'da hafif `Process/` self-timeout. Reddedildi (kullanıcı): durable+DLQ yeterli, state geri gelmesin.
- **Kabul edilen borç**: "Ödendi ama stok yok"ta otomatik iade yok (bugünkü davranış = iptal + log, manuel).
  Yeni ADR'da açık borç; otomatik iade ayrı feature.

## D3 — Tüm yeni event'ler fanout

- **Karar**: `StockCommitRequested`, `StockCommitted`, `StockCommitFailed` fanout exchange ile yayılır —
  `StockCommitRequested` tek tüketicili (Stock) olsa bile.
- **Gerekçe**: Repo integration-event deseni fanout; binding'i tüketici kurar. Tutarlılık + ileride ikinci
  tüketici (ör. bildirim/analitik) binding eklemekle katılır, yayıncı değişmez. Yön açık (conventions dikiş kuralı).
- **Alternatif**: `StockCommitRequested` direct queue (tek hedef). Reddedildi: tutarsızlık + genişlemeye kapalı.

## D4 — StockCommitFailed sebep kodu Stock'tan taşınır

- **Karar**: `StockCommitFailed.ReasonCode` = Stock'un resource sabiti (`STOCK_INSUFFICIENT`). Order bunu
  `Order.Cancel(reason)`'a verir + manuel-iade uyarısı loglar.
- **Gerekçe**: Hata sahipliği kaynak BC'de (conventions: her servis kendi kodlarına sahip). Order kodu
  yeniden yorumlamaz, taşır + log'a yazar. Generic kod tekrarı bilinçli (BC izolasyonu > DRY).

## D5 — İş hatası vs teknik hata ayrımı (ADR §5d korunur)

- **Karar**: Stok yetersiz = **iş hatası** → `StockCommitFailed` event → Order Cancel. Stock tüketicisinde
  geçici patlama (DB/RabbitMQ) = **teknik hata** → Wolverine retry merdiveni → tükenirse DLQ. `StockCommitFailed`
  teknik hatada YAYILMAZ (event yalnız iş-hatası taşır).
- **Gerekçe**: Retry kararı iş kuralıdır; yetersiz stok tekrar denese de yetersiz (retry anlamsız), geçici
  hata denenebilir. Event'i yalnız iş-sonucuna ayırmak Order tarafını basit tutar.

## D6 — Dosya yerleşimi: kaynak-adlı `*Consumers`, `Saga/` değil

- **Karar**: Tüketiciler BC kökünde, tükettikleri event'in **kaynak BC'si + Consumers** adıyla:
  Order/`StockConsumers` + değişen `PaymentConsumers`; Stock/`OrderConsumers`; Basket/`OrderConsumers`.
  `Saga/` klasörleri (orkestratör-güdümlü) silinir.
- **Gerekçe**: conventions — `Saga/` = başka BC'nin orkestratörünün broker komutu; orkestratör gidince
  `Saga/` anlamını yitirir. Kaynak-adlı `*Consumers` deseni mevcut `PaymentConsumers`/`CatalogConsumers` ile aynı.
- **Wolverine TUZAĞI**: `*Consumers` (çoğul) ad-sonekiyle keşfedilmez → her biri için
  `opts.Discovery.IncludeType(typeof(X))` ZORUNLU (yoksa event sessiz yutulur; memory: wolverine-eventhandler-includetype).

## D7 — Governance: İlke I v1.11.0 örneği uykuya düşer

- **Karar**: Anayasa İlke I v1.11.0 "orkestre saga broker komut/yanıtı" kanalının canlı örneği 049
  Checkout.Orchestrator'dır; bu feature'la uykuya düşer. İlke KALDIRILMAZ; PATCH amendment ile örnek
  "uykuda" işaretlenir (telemetri v1.12.0 + capability-link v1.12.1 emsali). Eski
  `adr-checkout-saga-orchestration.md` status → "yerini aldı (092 choreography)".
- **Gerekçe**: İlke içeriği hâlâ geçerli (gelecekte orkestre saga doğabilir); yalnız somut örnek kalkar.
  Anayasa + ADR bayat kalmasın diye aynı PR'da hizalanır (İlke VII ruhu belgeye de uygulanır).
