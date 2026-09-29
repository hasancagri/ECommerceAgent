# Basket — Domain Süreci

**BC ne yapar:** Kullanıcının kalıcı sepetini + kalemlerini tutar. Sahip login kullanıcı YA DA
anonim ziyaretçi olabilir (057); ikisi aynı modeldir, fark yalnız kimliğin kaynağı. Sepet stok
TUTMAZ ve süre İŞLETMEZ (056); stok gerçeği checkout anındadır.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Kullanıcı ürünü sepete atar.** Sepet yoksa oluşur; aynı Id'li satır  `(AddBasketItem`
   yenisiyle değişir (upsert, adet 1). Stok'a hiçbir çağrı gitmez.        ` → Basket.AddItem)`
2. **Adet mutlak değere getirilir.** `≤0` ise satır çıkar; 5 tavanı      `(SetBasketItemQuantity)`
   aşılamaz.
3. **Satır elle silinir.** Yalnız sepet belgesi değişir.                  `(DeleteBasketItem`
                                                                          ` → Basket.RemoveItem)`
4. **Checkout sepeti boşaltır (hand-off).** Orchestrator pivot-sonrası   `(Saga.CheckoutConsumers`
   broker komutuyla çağırır; sepet silinir (idempotent).                  ` → ClearBasketCommand)`

## Domain kuralları (süreci yöneten değişmezler)

- **Sepet kalıcıdır (056).** Zamana bağlı hiçbir üye/temizlik yok; terk edilmiş sepet süresiz durur.
- **Anonim sahiplik meşrudur (057).** Ekleme/okuma/adet/silme kimlik doğrulama İSTEMEZ; sahip tahmin
  edilemez Guid'dir (token `sub` ya da anonim kimlik). Login-merge yüzeyi 066 sonrası söküldü;
  `Basket.MergeFrom` davranışı domain'de durur, yüzey açılırsa buradan döner.
- **Sepet stok tutmaz (056).** Ekleme/adet/silme Stock'a gitmez; yetersizlik checkout'ta `CommitStock` reddeder, saga telafi eder.
- **Sabit üst sınır otoriter.** Satır adedi `Basket.MaxItemQuantity` (5) üstüne çıkamaz — UI/API/agent farketmez.
- **Fiyat snapshot'tır.** Satır fiyatı ekleme anındaki vitrin fiyatıdır; sepette güncellenmez.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat kaynağı (Catalog/Storefront), OnHand stok otoritesi (Stock),
sipariş/ödeme (Checkout saga) yok. Checkout temizliği saga'nın adımı — Basket başlatmaz.