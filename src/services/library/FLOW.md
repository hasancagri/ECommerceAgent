# Library — Domain Süreci (EventStorming)

**BC ne yapar:** Kullanıcının ürünle kalıcı ilgi kayıtlarını tutar; ilk dilim fiyat alarmı —
yaşayan abonelik olarak kurulur, gönderim izini saklar.

> **097 notu (fiyat değişmezliği):** fiyat doğumda sabitlendi — alarm TETİKSİZ UYUR: model/kayıtlar
> durur, `ProductChangedEvent` aboneliği söküldü, `PriceAlarmTriggered` yayıncısız. Aşağıdaki 3-4-5-7
> adımları uykudaki tasarımın kaydıdır (fiyat değişimi geri gelirse aynı modelle uyanır).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🟩 read-model ·
> 👤 aktör · ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 👤 **Kullanıcı** → 🟦 **Fiyat alarmı kur** → 🟨 **Alarm doğar** — e-posta kuruluş           `(PriceAlarm.Create)`
   anında snapshot alınır (doğrulanmaz, boş olabilir); ürün adı + o anki fiyat
   bağlam olarak saklanır.
2. 🟪 **Aynı kullanıcı × ürüne tek alarm** — mevcut kayıt varsa ikinci kurma
   isteği idempotent başarı döner, yeni kayıt yazılmaz.
3. 🟧 Ürün fiyatı değişti ⇦ Catalog → 🟪 değişim dinlenir _(uykuda, 097 — tüketici söküldü;
   tetik kuralı: eski fiyat dolu VE farklı)_
4. 🟧⭐ Bildirim tetiği yayınlandı ⇨ NotificationAgent _(uykuda, 097 — yayıncısı yok; kontrat
   `PriceAlarmTriggered` Shared'da durur)_
5. 🟪 **Alarm tetikte KAPANMAZ** — yaşayan abonelik: kullanıcı kaldırana dek her           `(PriceAlarm — mutator'suz)`
   fiyat değişimi yeni tetik üretir.
6. 👤 **Kullanıcı** → 🟦 **Alarmı kaldır** — hard delete; yaşam döngüsü biter.
   _(kur/kaldır MCP yazma tool'ları söküldü; giriş REST first-party'de yeniden açılacak.)_
7. 🟧 **Bildirim gönderildi** ⇦ NotificationAgent → 🟩 **İz yazılır** —    `(NotificationAgentConsumers ← NotificationSent`
   worker'ın sonucu append-only kayda düşer (sent / no-email / hata özeti).                   ` → NotificationRecord)`
8. 🟧 **Sipariş tamamlandı** ⇦ Order → 🟩 **Kişisel satın-alma kaydı** — kalem            `(OrderConsumers ← OrderCompleted`
   başına kullanıcı+ürün satırı; tekrar teslim/alım aynı satır (idempotent upsert).               ` → UserPurchase)`
   Storefront'tan taşındı (086); kişisel bağlam.

## Domain kuralları (süreci yöneten değişmezler)

- **E-posta snapshot'tır.** Kuruluş anındaki cookie claim değeri; sonradan değişen e-posta alarmı etkilemez.
- **Tetik = gerçek fiyat değişimi** _(uykuda, 097 — fiyat değişimi kavramı söküldü)_.
- **Tek alarm kuralı idempotent.** İkinci kurma isteği hata değil, başarı (düğme durumu bozulmaz).
- **İz append-only.** `NotificationRecord` davranışsız dokümandır; alarm silinse de iz kalır.
- **Satın-alma kaydı idempotent + append-only.** `UserPurchase` read-model'dir (aggregate değil); Id = "{userId}:{productId}" → tekrar alım aynı satır; backfill/revoke yok.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/fiyat gerçeği Catalog'da; mail üretimi/gönderimi `notification-agent` worker'ında
(iletişim yalnız event: `PriceAlarmTriggered` / `NotificationSent`). SMTP/LLM bu BC'de YOK.
