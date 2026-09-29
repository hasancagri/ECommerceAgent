# Checkout.Orchestrator — Domain Süreci

**BC ne yapar:** Ödeme-sonrası akışı **orkestre eder** — stoğu kalem kalem commit eder, siparişi onaylar,
sepeti temizler; stok adımı kesin başarısız olursa telafi eder. Ödeme akışın DIŞINDA öncedendir (hosted-CF
callback), sipariş girişte zaten oluşturulmuştur. Broker-only sağa (kendi iş DB'si yok; state Marten
belgesi); hiçbir BC'nin verisine dokunmaz.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Giriş: checkout başlar, senkron bekleme yok.** Ödeme hosted-CF ile     `(StartCheckout)`
   ÖNCEDEN çekildi → Order.Api `PaymentSucceeded`'i tüketip `StartCheckout`
   yayınlar; sipariş ZATEN oluşturulmuştur (OrderId dolu gelir).
2. **Sağa doğar + watchdog kurulur.** Doğrudan stok commit'e geçilir       `(CheckoutProcess.Start`
   (ödeme + sipariş hazır); telafi mümkün faz = CommittingStock.            ` → CheckoutTimedOut)`
3. **Kalemler tek tek commit edilir (döngü).** Her başarı sonraki kalemi   `(CommitStockCommand →`
   tetikler. 056: commit = doğrudan OnHand düşümü (rezervasyon yok) — stok   ` StockCommitted)`
   yetersizliği İLK KEZ burada yakalanır, telafiye geçer.
4. **Tüm kalemler commit olunca sipariş onaylanır.** Charge adımı YOK       `(StockCommitted →`
   (ödeme öncedendir); Phase=Confirming.                                     ` ConfirmOrderCommand)`
5. **Sipariş onaylandı → sepet temizlenir.** Onay kalıcı başarısız ama      `(OrderConfirmed →`
   ödeme çekildi → iptal ETME, logla + bitir (manuel müdahale).             ` ClearBasketCommand)`
6. **Sepet temizliği süreci bitirir.** Başarı da hata da tamamlar;          `(BasketCleared →`
   sepet temizlenemese bile sipariş Confirmed KALIR (FR-018).               ` MarkCompleted)`
7. **Telafi (yalnız CommittingStock'ta): stok LIFO geri sarılır → iptal.**  `(StockCommitReverted →`
   Commit edilmiş her kalem tersten revert; kalmayınca sipariş iptal.        ` RevertCommitStockCommand/CancelOrderCommand)`
8. **Watchdog güvenceye alır.** CommittingStock'ta takılırsa telafi;        `(CheckoutTimedOut`
   Confirming + sonrası iptal ETMEZ (ödeme alındı, belirsizlik), bitirir.   ` → CheckoutPhases)`

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
