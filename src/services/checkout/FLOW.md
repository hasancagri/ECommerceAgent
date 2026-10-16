# Checkout.Orchestrator — Domain Süreci (EventStorming)

**BC ne yapar:** Ödeme-sonrası akışı **orkestre eder** — stoğu kalem kalem commit eder, siparişi onaylar,
sepeti temizler; stok adımı kesin başarısız olursa telafi eder. Ödeme akışın DIŞINDA öncedendir (hosted-CF
callback), sipariş girişte zaten oluşturulmuştur. Broker-only sağa (kendi iş DB'si yok; state Marten
belgesi); hiçbir BC'nin verisine dokunmaz.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟧⭐ **Ödeme alındı** ⇦ Payment — pivot; hosted-CF ile sağa DIŞINDA, ÖNCEDEN                `(PaymentSucceeded)`
   çekildi. Sipariş ZATEN oluşturulmuştur (OrderId dolu gelir).
2. 🟪 **Ödeme alınınca checkout başlar** → 🟦 **Checkout'u başlat** ⇦ Order —                 `(PaymentSucceeded →`
   Order.Api event'i tüketip komutu yayınlar; senkron bekleme yok.                             ` StartCheckout)`
3. 🟨 **Sağa doğar** + 🟪 **watchdog kurulur** — ödeme + sipariş hazır olduğundan             `(CheckoutProcess.Start`
   doğrudan stok commit'e geçilir; telafi mümkün tek faz = CommittingStock.                    ` → CheckoutTimedOut)`
4. 🟦 **Stok kalemini düş** ⇨ Stock → 🟧 **Stok kalemi düşüldü** — kalem kalem döngü,         `(CommitStockCommand →`
   her başarı sonrakini tetikler. 056: commit = doğrudan OnHand düşümü (rezervasyon             ` StockCommitted)`
   yok) — stok yetersizliği İLK KEZ burada yakalanır, telafiye geçer.
5. 🟪 **Tüm kalemler düşünce onayla** → 🟦 **Siparişi onayla** ⇨ Order — Charge               `(StockCommitted →`
   adımı YOK (ödeme öncedendir); Phase=Confirming.                                             ` ConfirmOrderCommand)`
6. 🟧 **Sipariş onaylandı** → 🟦 **Sepeti temizle** ⇨ Basket — onay kalıcı başarısız          `(OrderConfirmed →`
   ama ödeme çekildi → iptal ETME, logla + bitir (manuel müdahale).                            ` ClearBasketCommand)`
7. 🟧 **Sepet temizlendi** → 🟪 **süreç tamamlanır** — başarı da hata da tamamlar;            `(BasketCleared →`
   sepet temizlenemese bile sipariş Confirmed KALIR (FR-018).                                  ` MarkCompleted)`
8. 🟪 **Telafi** (yalnız CommittingStock'ta): 🟦 **Commit'i geri sar** ⇨ Stock →              `(StockCommitReverted →`
   🟧 **Stok geri alındı** — commit edilmiş kalemler LIFO tersten revert;                      ` RevertCommitStockCommand`
   kalmayınca 🟦 **Siparişi iptal et** ⇨ Order.                                                ` / CancelOrderCommand)`
9. 🟪 **Watchdog güvenceye alır** — CommittingStock'ta takılırsa telafi; Confirming           `(CheckoutTimedOut`
   + sonrası iptal ETMEZ (ödeme alındı, belirsizlik), bitirir.                                 ` → CheckoutPhases)`

## Domain kuralları (süreci yöneten değişmezler)

- **Pivot = ödeme, sağa DIŞINDA geçildi (hosted-CF callback anı).** Sağa içi TÜM adımlar (Commit/Confirm/Clear) pivot-SONRASI; para dış PG'de, iade/void kapsam dışı — telafi yalnız stok geri-alır + sipariş durumunu düzeltir.
- **Broker-only, senkron çağrı YOK (İlke I).** Her adım RabbitMQ komut/yanıtı; state Marten belgesi (`Id`=CheckoutId), her adım atomik persist → restart'a dayanır (FR-020).
- **Geçici hata sağada değil.** Erişilemezlik → hedef BC fırlatır, Wolverine komutu yeniden dener; sağa yalnız KESİN sonucu görür (başarı → ilerle, kalıcı hata → telafi). `ErrorClass` ayrımı sözleşmede (Transient/Permanent).
- **Idempotency.** Her komut `{CheckoutId}:{step}` anahtarı taşır; tekrar teslim yan etki üretmez.
- **Bayat-mesaj guard.** `Phase` etiketi telafi başlayınca (Compensating) geç gelen commit yanıtını no-op'lar; tamamlanmış sağaya geç watchdog sessizce düşer (FR-026).

## Sınır (bu BC'nin dokunmadığı)

Sipariş oluşturma + durumu Order BC'nin; stok commit/revert Stock BC'nin; ödeme çekimi Payment +
PaymentGateway'in (sağa öncesi, callback ile); sepet içeriği Basket BC'nin. Orchestrator hiçbirinin
DB'sine/aggregate'ine dokunmaz — yalnız komut yayar, yanıt bekler, sonraki adımı sürer. Ürün/fiyat bilmez,
para hesaplamaz (tutar girişte gelir).