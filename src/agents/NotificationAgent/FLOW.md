# NotificationAgent — Domain Süreci

**BC ne yapar:** Library'nin fiyat alarmı tetiğini dinler, maili AI ile kişiselleştirir,
Mail.Mcp'nin `send_mail` tool'u üzerinden gönderir ve sonucu iz için geri yayınlar. DB'siz worker.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Alarm tetiği tüketilir.** Event mail'e yetecek her şeyi taşır      `(PriceAlarmTriggered`
   (alıcı e-posta snapshot dahil); worker başka servise SORMAZ.          ` → PriceAlarmEventHandlers)`
2. **E-postasız tetik gönderimsiz kapanır.** Adres boşsa mail atlanır,  `(PriceAlarmEventHandlers)`
   iz "no-email" ile yine düşer.
3. **Mail TEK agent'la yazılır VE gönderilir.** Türkçe (hitap + ürün    `(MailAgent — send_mail)`
   adı + eski/yeni fiyat; ürün linki YOK — UI söküldü); aynı LLM
   çağrısı Mail.Mcp'nin `send_mail` tool'unu tool-seçimiyle çağırır; imperatif MCP YOK.
4. **Sonuç geri yayınlanır.** Başarı/başarısızlık + kısa detay;         `(→ NotificationSent)`
   izi Library BC yazar.
5. **Hata dayanıklı yönetilir.** Her LLM/MCP/SMTP hatası aynı yol:      `(NotificationException → DLQ)`
   retry 10s/30s/60s sonra error queue.

## Domain kuralları (süreci yöneten değişmezler)

- **DB'siz worker.** Kendi durumu yok; girdi/çıktı yalnız event. Kalıcılık (iz) Library BC'nin sorumluluğu.
- **Fat event, sorgu yok.** Tetikteki veriyle mail üretilir; Catalog/Identity'ye tur atılmaz (BC izolasyonu).
- **Tek hata yolu.** Yaz+gönder tek LLM çağrısı olduğundan her hata (LLM/MCP/SMTP) retry'a gider;
  ayrı "yedek şablon" yolu yok (birleştirme kararı 2026-09-03).
- **Tek agent, Singleton, düz sıra.** Workflow grafiği de compose/send ayrımı da yok; ChatAgent deseni.
- **At-least-once kabulü.** Retry nadir durumda çift mail üretebilir; bilinçli mükerrer sıfır hedefi ihlal edilmez.

## Sınır (bu BC'nin dokunmadığı)

Alarm kaydı/tetiği ve iz kalıcılığı Library'de; SMTP detayı Mail.Mcp'de (worker yalnız tool çağırtır).
Mail.Mcp yalnız bu worker'a kayıtlıdır — başka agent/servis gönderim yapamaz.