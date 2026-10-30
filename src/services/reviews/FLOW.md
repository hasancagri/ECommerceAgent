# Reviews — Domain Süreci (EventStorming)

**BC ne yapar:** Satın-alma şartıyla ürün yorumu toplar; yorum HEMEN görünür doğar. Özet (ortalama +
adet) okuma anında (get_reviews) hesaplanır. Moderasyon yok (kullanıcı serbest yazar).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟩 read-model · 🟥 hotspot ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟧 **Sipariş tamamlandı** ⇦ Order → 🟪 **satın-alma kanıtı birikir** → 🟩 **kanıt    `(OrderConsumers ← OrderCompleted`
   read-model'i** — yorum yalnız satın-alanlar tarafından yazılır; kanıt lokal           ` → PurchasedProduct)`
   read-model'den (OrderCompleted event-fed), yoksa RED.
2. 🟦 Yorum gönder _(planned)_ — kullanıcı × ürün için TEK yorum; uygulama önce kontrol
   eder, son sözü Marten unique index söyler (yarış kaybedeni nazik hata). _(MCP girişi
   söküldü; REST first-party'de yeniden açılacak — aggregate davranışı durur.)_
3. 🟨 **Yorum doğar (Visible)** — moderasyon yok, HEMEN görünür; puan 1-5 tam, metin     `(Review.Create)`
   ≤2000, ad zorunlu; görünen ad token claim'inden, istek gövdesinden ASLA.
4. 🟩 Ürün özeti okuma anında hesaplanır _(planned)_ — ortalama + adet görünür yorumlardan
   sayılır; dışa fanout YOK (vitrin özeti event'i söküldü). Okuma yüzeyi MCP'yle birlikte
   söküldü, REST first-party'de yeniden açılacak.

## Domain kuralları (süreci yöneten değişmezler)

- **Satın-alma şart (fail-closed).** Kanıt kanalı erişilemezse yorum reddedilir; "kanıt yok" sayılmaz.
- **Yorum Visible doğar, moderasyon yok.** Kullanıcı serbest yazar (küfür dahil); içerik gizleme yüzeyi
  yok. `ReviewStatus.Hidden` şu an yazılmaz — görünürlük kavramı gelecek (spam/kullanıcı-silme) için durur.
- **Ham ad saklanır, yüzeye Masked() çıkar** (`ReviewerName`) — maske görüntüleme kuralı, veri değil.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat, sipariş, ödeme yok. LLM/agent-framework Reviews'ta YOK — AI moderasyonu söküldü
(ayrı worker + moderasyon istek/karar event'leri 088'de kaldırıldı).
