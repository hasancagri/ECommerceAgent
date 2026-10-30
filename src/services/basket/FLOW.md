# Basket — Domain Süreci (EventStorming)

**BC ne yapar:** Kullanıcının kalıcı sepetini + kalemlerini tutar. Sahip login kullanıcı YA DA
anonim ziyaretçi olabilir (057); ikisi aynı modeldir, fark yalnız kimliğin kaynağı. Sepet süre
İŞLETMEZ (056); stok kavramı yok (dijital PDF pivotu — Stock BC söküldü).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟦 **Ürünü sepete at** → 🟨 **Sepet doğar/güncellenir** — kullanıcı atar; sepet           `(Basket.AddItem)`
   yoksa oluşur, aynı Id'li satır yenisiyle değişir (upsert, adet 1). Stok'a
   hiçbir çağrı gitmez.
2. 🟦 **Adedi mutlak değere getir** — `≤0` ise satır çıkar; 5 tavanı aşılamaz.               `(Basket.SetItem)`
3. 🟦 **Satırı elle sil** — yalnız sepet belgesi değişir.                                     `(Basket.RemoveItem)`

   _(Yazma giriş noktaları MCP'den kalktı — add/adet/sil tool'ları söküldü; aggregate davranışı
   durur, giriş REST first-party'de yeniden açılacak.)_
4. 🟪 **Sipariş tamamlanınca sepet boşalır (hand-off)** → 🟦 **Sepeti temizle** ⇦ Order —    `(OrderConsumers`
   Order `OrderCompleted` fanout'unu tüketir (092 koreografi); onaylanan siparişin            ` ⇦ OrderCompleted)`
   kullanıcısının sepeti silinir (idempotent; sepet yoksa da no-op).

## Domain kuralları (süreci yöneten değişmezler)

- **Sepet kalıcıdır (056).** Zamana bağlı hiçbir üye/temizlik yok; terk edilmiş sepet süresiz durur.
- **Anonim sahiplik meşrudur (057).** Ekleme/okuma/adet/silme kimlik doğrulama İSTEMEZ; sahip tahmin
  edilemez Guid'dir (token `sub` ya da anonim kimlik). Login-merge yüzeyi 066 sonrası söküldü;
  `Basket.MergeFrom` davranışı domain'de durur, yüzey açılırsa buradan döner.
- **Sepet stok tutmaz.** Dijital (PDF) pivotla stok kavramı tamamen kalktı (Stock BC söküldü); sepet hiçbir envanter kontrolü yapmaz.
- **Sabit üst sınır otoriter.** Satır adedi `Basket.MaxItemQuantity` (5) üstüne çıkamaz — UI/API/agent farketmez.
- **Fiyat snapshot'tır.** Satır fiyatı ekleme anındaki vitrin fiyatıdır; sepette güncellenmez.
- **Temizlik yalnız onaylı siparişte (092).** Sepet `OrderCompleted` (Confirm pivotu sonrası) ile silinir; iptal/başarısız siparişte sepet KORUNUR.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat kaynağı (Catalog), sipariş/ödeme yok. Checkout temizliği event tepkisidir (092 koreografi) — Basket başlatmaz, OrderCompleted'i tüketir.
