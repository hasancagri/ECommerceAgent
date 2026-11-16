# Quickstart: MCP Query-Only + ScoutAgent Doğrulama

Host/entegrasyon harness'ı YOK (İlke VI) — doğrulama Aspire'da elle + saf birim testleri.

## Ön koşul
- Ollama kurulu + tool-calling destekli model çekili (`ollama pull <model>`); ScoutAgent config'inde model adı + `num_ctx`.
- `dotnet user-secrets` admin + Google (önceki dilimler) zaten kurulu.
- Aspire: `dotnet run --project src/aspire/AppHost/AppHost.csproj` → scoutDb + scout-agent + 4 BC MCP ucu + gateway ayağa kalkar.
- React: `~/dev/ECommerceWeb` `npm run dev`.

## Birim testleri (saf — test-first, İlke VI)
- `ConversationContext` bağlam kurucu: pencere kırpma + özet başa ekleme + tool-sonucu dışlama (`tests/ScoutAgent.Api.Tests`).
- `Conversation` aggregate: `Start`/`AddUserMessage`/`AddAssistantMessage`/`ApplySummary` davranışları (Method_Condition_Outcome).
- `dotnet test` yeşil.

## Elle uçtan uca (Aspire + React chat)

### US1 — anonim kitap sohbeti
1. Girişsiz, chat'e "Sabahattin Ali kitapları" → `search_books` çağrılır, kart listesi döner.
2. "Zyxwv123 diye kitap" → dürüst "bulunamadı" (uydurma yok).
3. "hava nasıl?" → kapsam dışı nazik cevap, tool çağrılmaz.

### US2 — kişisel niyet + yönlendirme
4. GİRİŞLİ: "adreslerimi getir" → `get_my_addresses`, kendi adresleri (GUID yok). "siparişlerim" → `get_my_orders`. "kitaplığım" → `get_my_library`.
5. GİRİŞSİZ: "adreslerimi getir" → veri YOK, "giriş yap" daveti (iki katman: tool sette yok + uç 401).
6. Başka kullanıcının verisi hiçbir sorguda görünmez (token = kimlik).

### US3 — kartlar
7. DiscoverWelcome aktif kart tıkla → chat kartın sorusuyla açılır, cevap gelir.
8. "yakında" kart (ruh hali/benzer kitap) pasif/rozetli, tıkla → sohbet başlatmaz.

### US4 — geçmiş + hafıza
9. Girişli iki sohbet → Sohbet Geçmişi'nde ikisi listeli (yalnız kendi). Birini aç → devam mesajı önceki bağlama uygun.
10. 30+ mesajlık sohbet → baştaki yazar atfı hatırlanır (özet katmanı); cevap süresi makul.

### Dayanıklılık
11. Ollama'yı durdur → chat "şu an hizmet veremiyorum" (503); gezinme/login/listeler çalışır.

## MCP ucu izole doğrulama (opsiyonel)
- Catalog `/mcp` anonim: MCP inspector / `curl` ile `search_books` tool listesi + çağrı.
- Customer/Library/Order `/mcp`: token'sız kişisel tool → 401; geçerli token → kendi verisi.
