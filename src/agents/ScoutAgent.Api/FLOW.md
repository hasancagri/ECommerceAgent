> Lejant: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟩 read-model · 🩷 dış sistem ·
> 👤 aktör · 🟥 hotspot · ⇦/⇨ BC-arası dikiş (kaynak/hedef BC). Satır sonu backtick `(…)` = koda köprü.
> Backtick'siz + _(planned)_ = henüz kodu yok. Süreç değişince bu dosya aynı PR'da güncellenir.

# ScoutAgent (Kâşif) — sohbet + niyet-routing süreci

**BC ne yapar:** Kullanıcının serbest metnini alır, LLM ile niyeti anlar, doğru BC'nin **MCP query
tool'unu** seçip çağırır, cevabı sohbet kartlarıyla döner. Agent YALNIZ MCP konuşur (first-party,
agent-içi — dış/BYO yüzey YOK). Yazma YAPMAZ (okuma-only tool'lar); aksiyon REST+UI'da (keşif=sohbet,
aksiyon=tık). Login kullanıcının transkriptini kendi deposunda tutar; anon transkript istemcide.

## Süreç

1. 👤 kullanıcı chat'e mesaj yazar → 🟦 **SendMessage** (POST `/api/chat/v1/messages`;        `(SendMessage.Handle)`
   mesaj-güdümlü, `?q=` URL YOK).
2. 🟪 Login ise `Conversation` yüklenir, bağlam `[özet]+[son N]` kurulur; anon ise istemci-transkript
   gövdeden gelir (oturumluk, sunucuda saklanmaz).
3. 🟪 Araç kutusu auth'a göre süzülür — anon = yalnız açık tool'lar (`search_books`/`get_book`/
   `get_book_reviews`); login = hepsi. Model 403 görmez (deneyim katmanı).
4. 🩷 LLM (OpenAI gpt-4o-mini, `UseFunctionInvocation`) niyeti anlar → doğru MCP tool'u seçer; kitap/
   yazar/konu geçince SORMADAN `search_books` çağırır (netleştirme yok, önce ara).
5. 🩷 İlgili BC `/mcp` tool'u çağrılır ⇨ Catalog/Basket/Order/Library/Customer/Reviews/Personalization —
   kişisel tool'da kullanıcı token'ı taşınır (gerçek yetki sınırı uçta). Personalization yalnız
   `get_search_history` (okuma); arama-geçmişi silme sohbette YOK → agent geçmiş ekranına yönlendirir.
6. 🟧 Cevap `{text, items}` — `items` tool dönüşünden BİREBİR (fiyat/kapak/künye uydurma YOK).
   React tool adına göre kart render eder (tek sohbet akışı).
7. 🟪 Login ise user mesajı + assistant mesajı (`text` + gösterilen `items`'ın opak JSON kopyası      `(Conversation.AddAssistantMessage)`
   `ItemsJson`) `Conversation`'a eklenir → sohbet tekrar açılınca kartlar AYNEN gelir (017 reload
   sadakati). Özetleme KAPALI — tam transkript taşınır. Anon = kalıcı kayıt yok (istemci saklar).
8. 🟪 **Sayfalama:** cevap metni sayfa-izi taşır ("… 1. sayfa"); 👤 "devamını getir" → LLM
   `search_books page+1` çağırır → sonraki 20 aynı akışa eklenir (anon'da izi istemci-transkript taşır).
9. 🟪 **Arama sinyali:** login + dolu-sorguda `search_books` yolu `UserSearched` yayar ⇨ Personalization
   (012/013 aynen; yayın REST ucundan buraya TAŞINDI). Anon → sessiz (kaydolmaz).
10. 🟦 **Geçmiş yönetimi (017):** 👤 sohbetini elle yeniden adlandırır → **RenameConversation**           `(Conversation.Rename)`
    (PATCH `/conversations/{id}`; boş red, UpdatedTime DOKUNULMAZ — sidebar sırası sabit) ya da
    kalıcı siler → **DeleteConversation** (DELETE, HARD). İkisi de sahiplik-korumalı (başkasınınki 404).  `(DeleteConversation.Handle)`
    Anon muadili istemcide (sessionStorage; sunucu çağrısı yok).

## Domain kuralları (süreci yöneten değişmezler)

- **Yalnız MCP.** Agent BC'lere REST/gRPC ile DEĞİL, yalnız MCP tool'uyla erişir (agent kimliği budur;
  conventions "MCP yalnız agent tüketir"). Uç listesi config-güdümlü (yeni BC = tek satır).
- **Okuma-only.** Tüm tool'lar query; yazma (sepete-ekle/sipariş/yorum/adres/arama-geçmişi-silme) MCP'de
  YOK — REST+UI. Arama geçmişi okunur (`get_search_history`) ama silinmez; agent silmeyi ekrana yönlendirir.
- **Items birebir.** Tool sonucu değiştirilmeden döner. 017: gösterilen kartlar assistant mesajına
  `ItemsJson` olarak SAKLANIR (reload sadakati) AMA modele giden **bağlama GİRMEZ** — `BuildContext`
  yalnız `Text` okur (zehirlenme yok, maliyet sabit). Saklama ≠ bağlam; iki ayrı endişe.
- **Rename UpdatedTime'a dokunmaz.** Elle yeniden adlandırma "etkinlik" değil → sidebar sırası (son
  aktivite) korunur. LLM'in ilk-tur akıllı başlığı (`SetTitle`) bundan ayrı (UpdatedTime günceller).
- **Devamlılık kaynağı.** Login → ScoutAgent'ın kendi kaydı; anon → istemci-transkript. Agent çerçeve
  state'ine güvenilmez. Kullanıcı yalnız KENDİ sohbetini görür/sürdürür/adlandırır/siler (sahiplik).
- **Arama kaydı yalnız login.** `UserSearched` yalnız login+dolu-sorguda yayılır; anon arama kaydolmaz
  (012/013 invariant'ı korunur, toplu/anon log YOK).

## Sınır (bu BC'nin dokunmadığı)

- BC verisini DEĞİŞTİRMEZ (yalnız query-only MCP okur). Kendi tek kalıcı verisi = login sohbeti.
- Dış agent / DCR / consent yüzeyi YOK — yalnız first-party React tüketir.
- Toplu arama analitiği / sonuçsuz-sorgu paneli YOK (bilinçli dışarıda).

## Hotspot

- 🟥 (closed: "MCP yasağı vs agent kimliği" → first-party agent-içi MCP geri gelir, ADR teardown
  kuralını gerekçeyle tersine çevirir — dış/BYO yüzey HÂLÂ kapalı).
- 🟥 (closed: "anon sayfalama login zorlar mı" → HAYIR; anon istemci-transkriptle tam sayfalanır).
- 🟥 (closed: "sayfa izi nasıl" → cevap metnine iz; ayrı durum tablosu yok).
- 🟥 (closed: "bulut mu local mi" → bu dilim bulut gpt-4o-mini; local-LLM kuzey-yıldız ayrı dilim).
