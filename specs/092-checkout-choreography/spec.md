# Feature Specification: Checkout Orchestration → Choreography

**Feature Branch**: `092-checkout-choreography`

**Created**: 2026-10-19

**Status**: Draft

**Input**: User description: "Checkout: orchestration'dan choreography'ye geçiş. Checkout.Orchestrator saga servisi sökülür, yerine event-tepki zinciri gelir. Watchdog tamamen kaldırılır. Detaylı plan: plans/bu-notlar-zerinden-sana-snazzy-feather.md"

**Kademe:** Tam — yeni integration event'ler, bir BC + DB (checkout/checkoutDb) silinir, servisler-arası
kontrat (CheckoutMessages komut/reply) değişir. Küçük/trivial kademe kriterleri birden çok kez bozuluyor.

## Arka plan (neden)

Checkout bugün ayrı bir `Checkout.Orchestrator` BC'sinde state'li bir saga (`CheckoutProcess`) ile
koşuyor: `StartCheckout` ile doğar, stok commit'ini **kalem-kalem** sürer (`NextIndex` döngüsü),
başarısızlıkta `CommittedItems` yığınından **LIFO telafi** yapar, `watchdog` ile takılmayı yakalar.

Oysa ödeme 077 hosted-CF ile saga'dan **ÖNCE** kapatılıyor (dağıtık işlemin zor kısmı çözülmüş).
Saga'ya kalan iş az-adımlı, ileri-akışlı ve her adım tek bir BC içinde atomik: stok düş → order confirm
→ sepet sil. Saga'nın tüm karmaşıklığının tek kaynağı stok commit'in kalem-kalem yapılması. Stok commit
tek **atomik batch**'e çevrilince döngü + LIFO + fazlar + watchdog gereksizleşir. Bu profil ADR
`adr-checkout-saga-orchestration.md` §2 tablosundaki "az adım, basit ileri akış → **choreography**"
satırına ve §8'deki "ayrı orkestrasyon servisi = anti-pattern" tespitine birebir uyuyor.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ödenmiş sipariş uçtan uca tamamlanır (Priority: P1)

Müşteri ödemeyi tamamladıktan (hosted-CF callback başarılı) sonra siparişi, merkezi bir orkestratör
olmadan, yalnız servisler-arası event tepkileriyle tamamlanır: stok düşer, sipariş onaylanır, sepet
temizlenir, satın-alma kaydı (Reviews uygunluğu + Library geçmişi) oluşur.

**Why this priority**: Checkout'un mutlu yolu uygulamanın gelir anıdır; davranış bugünküyle birebir
aynı kalmalı (regresyon yok). Koreografi mutlu yolda saga'yla aynı sonucu üretmezse geçiş reddedilir.

**Independent Test**: Aspire tam-stack üstünde Claude Desktop MCP'den sepete ürün ekle → `start_payment`
→ PG callback'i başarıyla taklit et. Doğrula: stok adedi düştü, sipariş `Confirmed`, sepet silindi,
Reviews/Library sinyalleri geldi. `Checkout.Orchestrator` servisi dashboard'da yok.

**Acceptance Scenarios**:

1. **Given** Pending bir sipariş + yeterli stok, **When** `PaymentSucceeded` yayılır,
   **Then** tüm kalemlerin stoğu düşer, sipariş `Confirmed` olur, `OrderCompleted` yayılır, sepet silinir.
2. **Given** `OrderCompleted` yayıldı, **When** Reviews ve Library tüketir,
   **Then** satın-alma uygunluğu + satın-alma geçmişi bugünküyle aynı şekilde kaydedilir.
3. **Given** sipariş zaten `Confirmed`, **When** `StockCommitted` tekrar teslim edilir (at-least-once),
   **Then** sipariş yeniden onaylanmaz, `OrderCompleted` yeniden yayılmaz (idempotent).

---

### User Story 2 - Stok yetersizse sipariş atomik iptal edilir (Priority: P1)

Ödeme alındıktan sonra stok yetmezse, hiçbir kalem kısmen düşmeden sipariş iptal edilir; sepet korunur.
Telafi için geri-alınacak kısmi düşüm oluşmaz (ya hep ya hiç).

**Why this priority**: Rezervasyonsuz (056) + ödeme-önden modelinin kabul edilmiş riski; yanlış
davranış stok kaçağı ya da müşteri mağduriyeti üretir. Atomik batch'in asıl kazancı burada görünür.

**Independent Test**: Sepetteki bir kalemin adedi stoğu aşacak şekilde ayarla → callback başarılı →
sipariş `Cancelled`, HİÇBİR ürünün stoğu değişmemiş, sepet korunmuş.

**Acceptance Scenarios**:

1. **Given** sepette biri stoğu aşan çok kalem, **When** stok commit denenir,
   **Then** hiçbir kalemin stoğu düşmez (tek transaction geri sarılır) ve `StockCommitFailed` yayılır.
2. **Given** `StockCommitFailed` yayıldı, **When** Order tüketir,
   **Then** sipariş `Cancelled` olur (sebep kodu taşınır); müşteri iadesi bu feature'ın kapsamı DIŞI
   (bugünkü davranış: kayıt + manuel), sepet silinmez.
3. **Given** stok commit başarısız oldu, **When** `StockCommitRequested` tekrar teslim edilir,
   **Then** sonuç aynıdır (idempotent); hiç commit edilmediği için telafi/revert gerekmez.

---

### User Story 3 - Takılı adım operatöre görünür ve tekrar denenebilir (Priority: P2)

Watchdog kaldırıldığı için takılan bir checkout adımı (geçici hata) dayanıklı mesajlaşmanın retry
merdiveniyle denenir, tükenirse ölü-mesaj kuyruğunda (DLQ, 088) operatöre görünür ve tekrar denenebilir.

**Why this priority**: Saga'nın watchdog'u gidince "takılma nasıl görülür/çözülür" sorusu açık kalmamalı.
Mevcut DLQ operatör yüzeyi bu güvenceyi zaten taşıyor; bu feature onun checkout akışını kapsadığını doğrular.

**Independent Test**: Stock tüketicisinde geçici hata simüle et → retry merdiveni tükenince mesaj DLQ'da →
`admin_list_dead_letters` ile görün → `admin_replay_dead_letter` ile yeniden sür, akış tamamlanır.

**Acceptance Scenarios**:

1. **Given** bir event tüketicisi geçici hata veriyor, **When** retry merdiveni tükenir,
   **Then** mesaj ilgili BC'nin DLQ'suna düşer ve `admin_list_dead_letters` ile listelenir.
2. **Given** takılı sipariş `Pending` kalmış, **When** operatör sipariş durumunu sorgular,
   **Then** sipariş `Pending` olarak görünür (zombi değil; durum sorgulanabilir).
3. **Given** DLQ'daki mesaj, **When** `admin_replay_dead_letter` çağrılır,
   **Then** akış kaldığı yerden idempotent olarak tamamlanır.

### Edge Cases

- **Çift `PaymentSucceeded`**: Order yalnız `Pending`'den ilerler → ikinci event no-op (mevcut guard).
- **Çift `StockCommitRequested`**: `ProductStock.Commit` orderId-idempotent → batch no-op.
- **Çift `StockCommitted` / `OrderCompleted`**: Order `Status==Confirmed` guard'ı → yeniden yayın yok;
  Reviews/Library tüketicileri zaten idempotent upsert.
- **Sepet zaten boş/yok**: silme idempotent (bugünkü `session.Delete` deseni) → `Ok`.
- **Kısmi stok (biri yeter biri yetmez)**: tek transaction → hiçbiri düşmez; `StockCommitFailed`.
- **`OrderCompleted` sonrası sepet silme düşerse**: sipariş `Confirmed` KALIR (iş tamam); silme retry/DLQ.
- **Order onayı başarısız ama ödeme çekilmiş**: bugünkü davranış korunur — log + kapsam dışı (iade manuel).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem checkout'u merkezi orkestratör/saga olmadan, servisler-arası integration event
  tepkileriyle (choreography) yürütMELİ. `Checkout.Orchestrator` BC'si ve `checkoutDb` KALDIRILMALI.
- **FR-002**: Order, `PaymentSucceeded`'i tükettiğinde stok commit'i talep eden tek bir event
  (`StockCommitRequested`: OrderId, UserId, Items) yayınlaMALI; artık `StartCheckout` yayınlaMAMALI.
- **FR-003**: Stock, `StockCommitRequested`'i tükettiğinde siparişin TÜM kalemlerinin stoğunu **tek
  transaction içinde atomik** düşürMELİ; bir kalem bile yetersizse hiçbiri düşMEMELİ (ya hep ya hiç).
- **FR-004**: Stock, başarıda `StockCommitted`(OrderId), yetersizlikte `StockCommitFailed`(OrderId,
  ReasonCode) yayınlaMALI.
- **FR-005**: Order, `StockCommitted`'i tükettiğinde siparişi `Confirm` edip `OrderCompleted` fanout'unu
  (Reviews + Library + Basket) yayınlaMALI (mevcut pivot davranışı korunur).
- **FR-006**: Order, `StockCommitFailed`'i tükettiğinde siparişi `Cancel` etMELİ (sebep kodu taşınır).
  Müşteri iadesi bu feature kapsamı DIŞI; bugünkü "kayıt + manuel" davranışı korunur.
- **FR-007**: Basket, `OrderCompleted`'i tüketip ilgili kullanıcının sepetini silMELİ (idempotent).
- **FR-008**: Tüm event tüketimleri at-least-once teslimat altında idempotent olMALI (çift teslimat yan
  etki üretmez): stok commit orderId-idempotent, order confirm/cancel durum-guard'lı.
- **FR-009**: Watchdog/timeout mekanizması KALDIRILMALI. Takılma güvencesi dayanıklı mesajlaşma +
  retry + DLQ (088) + sipariş durumu sorgulanabilirliği ile sağlanMALI.
- **FR-010**: Kullanılmayan kontratlar KALDIRILMALI: `CheckoutMessages` komut/reply record'ları
  (StartCheckout, CommitStockCommand, StockCommitted-reply, RevertCommitStockCommand, StockCommitReverted,
  ConfirmOrderCommand, OrderConfirmed, CancelOrderCommand, OrderCancelled, ClearBasketCommand,
  BasketCleared), Checkout RabbitMQ queue sabitleri, `ProductStock.RevertCommit` + `STOCK_REVERT_*` kodları.
  (`CheckoutItem`/`OrderAddress`/`ErrorClass` korunur/taşınır; yeni event'ler kullanır.)
- **FR-011**: Yeni integration event'ler `Shared.IntegrationEvents` içine additive eklenMELİ; fanout
  exchange adları `RabbitMqConstants`'ta merkezi olMALI; binding'leri TÜKETİCİ kurMALI (soğuk-açılış dersi).
- **FR-012**: Etkilenen her BC'nin FLOW.md'si (order, stock, basket) yeni event zincirine göre
  güncellenMELİ; `checkout/FLOW.md` silinMELİ (İlke VII + guard).
- **FR-013**: Karar bir ADR ile belgelenMELİ (`adr-checkout-choreography-migration.md`): 028/049
  orchestration kararını tersine çevirir; watchdog feragatı ve "ödendi-ama-stok-yok" iade borcu açıkça yazılır.
  Eski ADR'ın status'ü "yerini aldı" olarak işaretlenMELİ.
- **FR-014**: `CLAUDE.md` BC haritasından `checkout` satırı çıkarılMALI; order/stock/basket satırları ve
  "Yapma listesi"ndeki "049 Checkout.Orchestrator istisna" + "kalan senkron kontrat = checkout gRPC"
  ifadeleri güncellenMELİ. AppHost'tan servis + DB kaydı ve `.slnx`'ten proje kayıtları çıkarılMALI.

### Key Entities

- **Order** (mevcut aggregate): durum geçişleri `Pending → Confirmed | Cancelled`; checkout'un fiili
  durum sahibi. Saga Phase'i YOK — süreç durumu artık sipariş durumunda ve event akışında yaşar.
- **ProductStock** (mevcut aggregate): `Commit(quantity, orderId)` orderId-idempotent kalır; `RevertCommit`
  kaldırılır (telafi yok). Batch commit N aggregate'i tek transaction'da işler.
- **Basket** (mevcut aggregate): `OrderCompleted` sonrası idempotent silinir.
- **StockCommitRequested / StockCommitted / StockCommitFailed** (yeni integration event'ler): checkout
  koreografisinin zincir halkaları; korelasyon anahtarı OrderId.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Mutlu yol checkout sonucu bugünküyle birebir aynı: stok düşer, sipariş `Confirmed`, sepet
  silinir, Reviews/Library sinyalleri gelir — elle uçtan uca senaryoda %100 eşleşir.
- **SC-002**: Stok-yetersiz senaryosunda başarısızlık sonrası kısmen düşmüş stok kalemi sayısı = **0**
  (atomik ya-hep-ya-hiç); sepet korunur.
- **SC-003**: Çalışan servis sayısı **bir azalır** (Checkout.Orchestrator yok); Postgres veritabanı
  sayısı **bir azalır** (checkoutDb yok); sistem Aspire'da hatasız açılır.
- **SC-004**: Takılı bir checkout adımı retry sonrası DLQ'da görünür ve tekrar sürülebilir; takılı sipariş
  durum sorgusunda `Pending` olarak görünür (zombi/kayıp sipariş = 0).
- **SC-005**: Tüm saf domain birim testleri yeşil; batch-commit atomikliği için test-first yazılmış yeni
  testler dahil. `check-claude-spec-links.sh` + `check-flow-links.sh` yeşil.

## Assumptions

- **Ödeme kapsam dışı ve dokunulmaz**: 077 hosted-CF akışı (PaymentIntent, callback, PaymentSucceeded/
  PaymentFailed) aynen kalır. `PaymentFailed → Order.Cancel` yolu zaten koreografik; değişmez.
- **"Ödendi ama stok yok" iadesi kapsam dışı**: bugünkü davranış (iptal + kayıt, iade manuel) korunur;
  otomatik iade ayrı bir backlog/feature. Yeni ADR'da açık borç olarak yazılır.
- **Basket gRPC (`GetBasketItems`) kalır**: Order hâlâ `start_payment` sırasında kullanıyor; bu feature
  yalnız broker checkout yolunu değiştirir, okuma gRPC'sine dokunmaz.
- **checkoutDb prod'da yok** (öğrenme projesi); dev full-reset ile temizlenir, AppHost kaydının çıkışı yeter.
- **Order durumu = süreç görünürlüğü**: ayrı bir read-model/sorgu eklenmez; `Pending/Confirmed/Cancelled`
  takılma teşhisi için yeterli kabul edilir.

## Dependencies & Governance

- **Anayasa İlke I (v1.11.0) gerginliği**: v1.11.0 amendment'ı "orkestre saga broker komut/yanıtı"nı meşru
  kanal olarak eklemiş ve **049 Checkout.Orchestrator'ı canlı örnek** göstermiştir. Bu feature o örneği
  uykuya düşürür (repoda başka orkestre-saga yok). İlke KALDIRILMAZ (telemetri/capability-link emsali:
  istisna örneksiz uykuda kalır) ama anayasa + ADR hizalanmalı — `/speckit-plan` aşamasında bir anayasa
  amendment'ı (PATCH: örnek uykuda) gerekip gerekmediği değerlendirilir. Bu spec kararın kendisini değiştirmez.
- **DLQ operatör yüzeyi (088)**: FR-009'un güvencesi mevcut `admin_*dead_letter*` tool'larına dayanır;
  checkout BC'si gittiği için onun DLQ yüzeyi de gider, kalan BC'lerinki (order/stock/basket) yeterli.
- **İlgili memory/tuzaklar**: Wolverine discovery IncludeType (yeni consumer adları), wolverine-rabbitmq
  wire message-type (elle publish'te prefix'li tam ad), soğuk-açılış binding (tüketici bağlar).
