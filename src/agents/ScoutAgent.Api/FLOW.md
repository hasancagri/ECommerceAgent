> Lejant: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟩 read-model · 🩷 dış sistem ·
> 👤 aktör · 🟥 hotspot · ⇦/⇨ BC-arası dikiş (kaynak/hedef BC). Satır sonu backtick `(…)` = koda köprü.
> Backtick'siz + _(planned)_ = henüz kodu yok. Süreç değişince bu dosya aynı PR'da güncellenir.

# ScoutAgent (Kâşif) — iki-faz tipli kitap-keşfi süreci

**BC ne yapar:** Kullanıcının serbest metnini alır, LLM ile TİPLİ niyete (slot objesi) çıkarır, sunucu
deterministik yorumlayıp Catalog `search_books` MCP tool'uyla arar, kartlarla döner. Sohbet YALNIZ kitap
keşfi (keşif=sohbet, aksiyon=tık — sepet/sipariş/yorum REST+UI'da). Yazma YOK (query-only MCP). Login
kullanıcının transkriptini kendi deposunda tutar; anon transkript istemcide. (023: /messages router+
sub-agent EMEKLİ — tek yol iki-faz.)

## Süreç

1. 👤 kullanıcı sorgu yazar → 🟦 **ExtractIntent** (POST `/api/chat/v1/extract`). **020-voice:** metin     `(ExtractIntent.Handle)`
   SESLE de gelebilir — 🩷 ses OpenAI AudioClient'la çevrilir (POST `/transcribe`, stateless, diske        `(Transcribe.Handle)`
   yazmaz), composer'a düşer; OTOMATİK GÖNDERME YOK.
2. 🩷 LLM (OpenAI gpt-4o-mini) strict **structured output** (`GetResponseAsync<BookSearchFilters>`) niyeti
   TİPLİ NESTED slot objesine çıkarır: model yalnız kullanıcının kelimelerini doğru slota koyar (çeviri/
   enum/id YOK; uydurma yasağı — sorguda açıkça yoksa slot null). 🟪 Bağlam: React önceki tipli filtreyi
   `previous` olarak taşır → LLM daraltma/sıfırlama kararını verir (çok-tur). Hata → çıplak-query fallback.
3. 🟪 React tipi alır, beklemeden 🟦 **SearchTyped**'e relay eder (POST `/search`; oto-relay, ONAY YOK —     `(SearchTyped.Handle)`
   arama geri dönüşsüz değil). "anladım" chip'i gösterim amaçlı (gate değil).
4. 🟪 DETERMİNİSTİK (LLM YOK): `FilterInterpreter` slotları `search_books` argümanlarına düzler            `(FilterInterpreter.ToSearchArgs)`
   (PriceHint/PageHint metin→sayı burada; sort/binding/yazar/yayınevi metni Catalog yorumlar).
5. 🩷 Catalog `search_books` çağrılır ⇨ Catalog — yazar adı `author` filtresine (jsonb `?|`, KELİME-
   sınırı eşleşme) → "X'in kitapları" YALNIZ X'in eserleri (BM25 token kirliliği biter). "X hakkında" =
   konu → query'de kalır.
6. 🟧 Cevap `{text, items}` — `text` ŞABLON ("'X' için N kitap" / sayfalamada "Sonraki sayfa — a–b"),       `(SearchTyped.Handle → ChatResponse)`
   `items` tool dönüşünden BİREBİR (fiyat/kapak/künye uydurma YOK). React tool adına göre kart render eder.
7. 🟪 Login ise user mesajı (ham sorgu) + assistant mesajı (`text` + `items` opak JSON kopyası            `(Conversation.AddAssistantMessage)`
   `ItemsJson`) `Conversation`'a eklenir → tekrar açılınca kartlar AYNEN (017 reload). Anon = istemci saklar.
8. 🟪 **Sayfalama:** React son tipli filtreyi tutar; 👤 "devamını getir" → React `/extract` ATLAR,
   `Filters.Page+1` ile doğrudan `/search` (niyet-eşleme React'te, ek-çekim toleranslı).
9. 🟪 **Arama sinyali:** login + dolu-sorguda `search_books` yolu `UserSearched` yayar ⇨ Personalization
   (012/013). Anon → sessiz.
9b. 🟦 **Sana özel feed (028):** 👤 sorgu-YOK feed (login) → GET `/feed` → ⇦ Personalization `get_my_taste`  `(Feed.Handle)`
    (taste vektör+signalCount, token-forward) → signalCount<3/boş ise küratörlü değilse ⇨ Catalog
    `recommend_books(taste)` → kartlar. Deterministik (LLM yok). Vektör opak (loglanmaz, İlke VIII). Anon → 401.
10. 🟦 **Geçmiş yönetimi (017):** 👤 sohbetini yeniden adlandırır → **RenameConversation**                   `(Conversation.Rename)`
    (PATCH `/conversations/{id}`; boş red, UpdatedTime DOKUNULMAZ) ya da siler → **DeleteConversation**     `(DeleteConversation.Handle)`
    (DELETE, HARD). Sahiplik-korumalı (başkasınınki 404). Anon muadili istemcide.

## Domain kuralları (süreci yöneten değişmezler)

- **Çıkarma LLM, arama deterministik.** Tur-1 (`/extract`) TEK LLM işi = niyeti tipli slota koymak. Tur-2
  (`/search`) LLM YOK — slot→arg→MCP. Toplam 1 LLM turu (eski router+sub-agent 2-zar değil).
- **Yazarlık vs hakkında.** Yazar adı query'ye GİRMEZ, `author` filtresine (token kelime-sınırı, substring
  çarpışması yok). "X'in kitapları" = yazarlık (author); "X hakkında" = konu (query). Ayrım slotlamada.
- **Uydurma yasağı.** Sorguda açıkça geçmeyen filtre (sort/fiyat/stok/cilt) slot'a KOYULMAZ (null/false).
- **Yalnız MCP, okuma-only.** Catalog'a yalnız `search_books` query tool'uyla erişir (REST/gRPC değil).
  Yazma YOK — aksiyon REST+UI.
- **Items birebir.** Tool sonucu değişmeden döner. 017: kartlar assistant mesajına `ItemsJson` SAKLANIR
  (reload) AMA modele giden bağlama GİRMEZ (saklama ≠ bağlam).
- **Rename UpdatedTime'a dokunmaz.** Elle ad "etkinlik" değil → sidebar sırası korunur.
- **Devamlılık + sahiplik.** Login → kendi kaydı; anon → istemci-transkript. Kullanıcı yalnız KENDİ
  sohbetini görür/sürdürür/adlandırır/siler.
- **Arama kaydı yalnız login.** `UserSearched` yalnız login+dolu-sorguda; anon kaydolmaz.

## Sınır (bu BC'nin dokunmadığı)

- BC verisini DEĞİŞTİRMEZ (yalnız query-only MCP okur). Kendi tek kalıcı verisi = login sohbeti.
- Catalog-dışı sohbet niyetleri YOK (sepet/sipariş/yorum = REST+UI; 023'te /messages sub-agent'larıyla söküldü).
- Dış agent / DCR / consent yüzeyi YOK — yalnız first-party React tüketir.
- Konsept/semantik arama ("Rus klasikleri" anlamla) YOK — BM25 token-eşleşme; semantik ayak ayrı dilim.

## Hotspot

- 🟥 (closed: "niyet routing tek düz agent mı, router+sub-agent mi" → 019 router+sub-agent kuruldu, 022
  iki-faz tipli çıkarmaya evrildi, 023'te /messages+router+sub-agent EMEKLİ — tek yol /extract+/search).
- 🟥 (closed: "param kırılganlığı" → tipli structured output + FilterInterpreter; elle-JSON hack söküldü).
- 🟥 (closed: "bulut mu local mi" → bulut gpt-4o-mini yalnız Tur-1 çıkarmada; local-LLM ayrı dilim).
- 🟥 (open: "konsept/semantik arama" → 'Rus klasikleri'/'Stoacı felsefe' token-eşleşmeyle bulunmaz;
  semantik ayak (096 söküldü) yeni spec'le döner — closed olunca bu satır güncellenir).
