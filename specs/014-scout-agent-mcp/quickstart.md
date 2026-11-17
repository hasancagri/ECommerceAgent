# Quickstart — ScoutAgent (Kâşif) doğrulama

Host ayağa kaldırılmaz test (unit) + canlı Aspire doğrulaması. Saf domain test-first (İlke VI);
MCP/LLM/Marten yolları Aspire'da elle doğrulanır.

## Ön koşul

- OpenAI API key → user-secret / AppHost param (`ScoutOptions:ApiKey`). Git'e GİRMEZ.
- Aspire AppHost'tan başlat (`dotnet run --project src/aspire/AppHost/AppHost.csproj`).

## Unit (test-first, dotnet test)

- `Conversation.Start` boş userId → Error; dolu → ilk mesajla sohbet.
- `AddUserMessage` boş metin → Error.
- `ApplySummary` upTo geri gitmez.
- `ConversationContext.Build` → `[özet]+[son N]` doğru kesim.
- `search_books` sıralaması: yazar-eşleşmeleri başlık-eşleşmelerinden ÖNCE; grup içi PurchaseCount.

## Canlı senaryo (Aspire + React)

1. **US1 anon kitap keşfi:** çıkış yap → "Sabahattin Ali'nin kitaplarını listeler misin" → Sabahattin
   Ali kitapları kart olarak gelir (çıplak "Sabahattin Ali" ile AYNI sonuç). Bug kapalı.
2. **US1 boş:** "Zxqw kitapları" → "bulunamadı", uydurma yok.
3. **US2 routing (login):** "sepetimde ne var" → sepet kartı · "siparişlerim" → sipariş kartı ·
   "kitaplığım"/"adreslerim" → ilgili kart. Hepsi aynı sohbet akışında, yalnız kendi verisi.
4. **US2 anon kişisel:** çıkışta "sepetimde ne var" → "giriş gerekli" (çöp/hata değil).
5. **US3 sayfalama:** 20+ sonuçlu arama → altlık "40 sonuç · 1–20" → "devamını getir" → 21–40 eklenir,
   1–20 tekrarlanmaz. Çıkışta da çalışır (istemci-transkript).
6. **US4 arama kaydı:** login arama yap → arama geçmişinde (013) belirir. Çıkışta arama yap → geçmişe
   HİÇ yazılmaz.
7. **Dayanıklılık:** bir BC'yi durdur → o alan "şu an kullanılamaz", diğer sorular çalışır.
8. **Sır:** OpenAI key hiçbir yanıtta/log'da görünmez; GUID'ler kullanıcıya gösterilmez.

## Done

- Unit yeşil · 8 canlı senaryo geçer · SC-001..006 karşılanır (spec).
