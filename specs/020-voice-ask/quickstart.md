# Quickstart — Sesle Kâşif'e Soru (doğrulama)

Host-gerekli (ses/HTTP/OpenAI) → Aspire'da elle doğrulanır (İlke VI saf-domain dışı). Ön koşul: Aspire ayakta (`dotnet run --project src/aspire/AppHost/AppHost.csproj`), ScoutAgent + gateway + Catalog çalışır; `scout-openai-key` user-secret dolu; ECommerceWeb dev (`cd ~/dev/ECommerceWeb && npm run dev`, :5173).

## Senaryo 1 — Sesle soru (US1, MVP)
1. :5173 aç, keşfet sayfasında kal (giriş gerekmez).
2. Üst header'da mikrofon ikonu görünür (mobil + masaüstü).
3. Mikrofona bas → izin ver → kayıt şeridi (süre sayacı + iptal + bitir).
4. "Dostoyevski kitapları" de → bitir.
5. Birkaç saniye "çeviriyor" → metin `/sohbet` composer'ına düşer (henüz GÖNDERİLMEDİ).
6. Metni gör → gönder → Kâşif kartlarla yanıtlar.
**Beklenen**: metin doğru düşer, otomatik gönderilmez, gönderince normal sohbet yanıtı.

## Senaryo 2 — Başka sayfadan (US1-3)
1. Kitaplık (`/kitapligim`) ya da sepet sayfasında ol.
2. Sesle soru sor → bitir.
3. Sohbet yüzeyi açılır, metin orada composer'da (gönderilmemiş).
**Beklenen**: sayfadan sayfaya elle gitmeye gerek yok; ses her müşteri sayfasından çalışır.

## Senaryo 3 — Düzelt + gönder (US1-4)
1. Sesle sor, transcribe yanlış/eksik gelsin (ör. gürültü).
2. Composer'da metni düzelt → gönder.
**Beklenen**: düzeltilmiş metin gider; yanlış transcribe göndermeden düzeltilebilir.

## Senaryo 4 — İptal (US2)
1. Mikrofona bas, konuşmaya başla, iptale bas.
**Beklenen**: kayıt atılır, çevirme yapılmaz, composer değişmez, mikrofon boşta döner. (Mic ışığı söner — track kapandı.)

## Senaryo 5 — Maks süre (US2-2)
1. Mikrofona bas, ~60sn+ konuş.
**Beklenen**: süre sınırında otomatik durur, o ana kadarki ses çevrilir (kullanıcı kaybolmaz).

## Senaryo 6 — İzin yok / hata (US3)
1. Tarayıcı mikrofon iznini reddet → mikrofona bas.
**Beklenen**: nazik "mikrofon erişimi kapalı" uyarısı; yazma yolu çalışır.
2. (Sağlayıcı hata simülasyonu — key'i geçici boz) → kaydı bitir.
**Beklenen**: "şu an çeviremedim, yazarak dene"; composer bozulmaz. Sessiz başarısızlık YOK.

## Senaryo 7 — Kota (FR-010)
1. Anon olarak `TranscribeAnonPerDay` sınırını aş.
**Beklenen**: 429 + nazik "biraz sonra tekrar dene". Sohbet kotasından BAĞIMSIZ (sohbet hâlâ çalışır).

## Senaryo 8 — İlke VIII (SC-005)
1. Tarayıcı DevTools → Network: transcribe isteği yalnız BİZİM uca (`/api/chat/v1/transcribe`), OpenAI'ye DOĞRUDAN istek YOK.
2. Yanıt gövdesi + konsol + sunucu log: OpenAI key HİÇBİR yerde görünmez.
**Beklenen**: istemci OpenAI'ye hiç dokunmaz; key yalnız sunucuda.

## Giriş/admin dışlama (kapsam)
- `/giris` (AuthLayout) ve admin ekranlarında mikrofon GÖRÜNMEZ.
