# Reviews — Domain Süreci

**BC ne yapar:** Satın-alma şartıyla ürün yorumu toplar; yorum HEMEN görünür doğar ve vitrin özetini
(ortalama + adet) Storefront'a yayınlar. Moderasyon yok (kullanıcı serbest yazar).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Yorum yalnız satın-alanlar tarafından yazılır.** Kanıt lokal       `(OrderConsumers ← OrderCompleted`
   read-model'den (OrderCompleted event-fed); yoksa RED.                 ` → PurchasedProduct)`
2. **Kullanıcı × ürün için tek yorum.** Uygulama önce kontrol eder,     `(SubmitReview)`
   son sözü Marten unique index söyler (yarış kaybedeni nazik hata).
3. **Yorum Visible durumda doğar** — puan 1-5 tam, metin ≤2000, ad      `(Review.Create)`
   zorunlu; görünen ad token claim'inden, istek gövdesinden ASLA.
4. **Görünen ürün özeti anında yayınlanır.** Ortalama + adet            `(ReviewSummaryChanged)`
   Reviews'ta hesaplanır (tüketici saymaz), Storefront'a fanout.

## Domain kuralları (süreci yöneten değişmezler)

- **Satın-alma şart (fail-closed).** Kanıt kanalı erişilemezse yorum reddedilir; "kanıt yok" sayılmaz.
- **Yorum Visible doğar, moderasyon yok.** Kullanıcı serbest yazar (küfür dahil); içerik gizleme yüzeyi
  yok. `ReviewStatus.Hidden` şu an yazılmaz — görünürlük kavramı gelecek (spam/kullanıcı-silme) için durur.
- **Ham ad saklanır, yüzeye Masked() çıkar** (`ReviewerName`) — maske görüntüleme kuralı, veri değil.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat, sipariş, ödeme yok. LLM/agent-framework Reviews'ta YOK — AI moderasyonu söküldü
(ayrı worker + moderasyon istek/karar event'leri 088'de kaldırıldı).
