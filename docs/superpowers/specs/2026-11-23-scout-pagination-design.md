# Kâşif sayfalama ("devamını getir") — tasarım

Tarih: 2026-11-23 · Durum: ONAYLANDI (2026-11-23)

## Niyet / neden

Sohbette "devamını getir / sonraki / diğer sonuçlar" dendiğinde sonuçlar bozuluyor:
son aramanın page+1'i gelmesi gerekirken sistem **sıfırdan yeni arama** yapıyor,
filtreleri (publisher/sort/sayfa) kaybediyor, page hep 1 kalıyor. Canlı DIAG kanıtı:
aynı oturumda total 52 → 477 → 2827, yayınevi filtresi düşüyor.

**Kök neden:** `BuildContext` bağlama yalnız asistan **metnini** taşıyor; gerçek arama
args'ı (publisher/page/filtre) LLM'e HİÇ ulaşmıyor. Durumsuz subagent her turda filtreleri
metinden yeniden tahmin ediyor → drift + page=1.

## Hedef

- "devamını getir" = son aramanın AYNI filtreleriyle page+1.
- LLM filtre türetmesini bu yolda tamamen devre dışı bırak → sıfır drift.
- Login + anon ikisinde de çalışsın.

## Hedef-dışı

- Bug 2 (maxPageCount uydurma "sayfa az") ve Bug 3 (çıplak-string düşüşü) — ayrı iş, bu tasarımda yok.
- Önceki/geri sayfa, sayfa-numarası atlama — yalnız ileri "devamını getir".

## Yaklaşımlar

- **A (sunucu-tarafı hafıza + `more_results` tool):** reddedildi — conversation state +
  anon'da state yok; daha ağır.
- **B (yapısal args'ı bağlama geri besle):** reddedildi — LLM hâlâ args kopyalıyor, drift sürer (mini zayıf).
- **C — SEÇİLDİ (client-tarafı page state, chat'e filtre+page taşı):** React son aramanın
  filtre+page'ini saklar; "devamını getir"de bunları chat isteğine ekler; backend LLM-türetmeyi
  ATLAR, verilen filtre+page'i AYNEN `search_books`'a geçirir. Sohbet turu kalır (kart ekli gelir).
  Client-state olduğu için anon'da da çalışır.

## Nasıl (kararlaştırılan)

1. **Backend — filtre echo:** `search_books` cevabının reply-item'ına uygulanan filtreler eklenir
   (CatalogSubAgent parse ettiği `args`'ı collector'a taşır). React saklayabilsin diye.
   Contract additive: `ReplyItem` → opsiyonel `filters` alanı (React `result: unknown` zaten).

2. **Backend — lastSearch girdisi + ifade yakalama:** `SendMessage.Command` opsiyonel
   `LastSearch { filters, page }` alır (React en son aramanın durumunu her mesajda taşır).
   Backend **saf, test edilebilir kural** (İlke VI test-first) kullanıcı mesajını "devamını getir /
   sonraki / diğer sonuç(lar) / daha fazla" türü ifadeye karşı Türkçe-fold ile eşler. Eşleşir VE
   `LastSearch` doluysa: router/LLM ATLANIR; `search_books` doğrudan `filters` + `page+1` ile MCP'den
   çağrılır; kartlar + deterministik kısa metin ("Sonraki sayfa — N–M arası.") üretilir; normal asistan
   turu olarak kaydedilir (login persist / anon client-transkript). Eşleşmezse: `LastSearch` yok sayılır,
   NORMAL LLM akışı. İfade-yakalama React'te DEĞİL backend'de (tek yer, kırılgan regex dağıtılmaz).

3. **React — state + taşıma:** son `search_books` cevabından `{filters, page}` saklanır (sohbet başına);
   HER `sendMessage` gövdesine `lastSearch:{filters, page}` olarak eklenir (varsa). Tetik = kullanıcının
   **"devamını getir" diye YAZMASI** (buton değil — kullanıcı kararı). Dönüşte kartlar yeni tur olarak
   eklenir, saklanan page backend'in döndürdüğü page'e güncellenir.

## Builder'a kalan

- Filtre echo'nun tam şekli (ReplyItem alanı mı, SearchBooksResult wrapper mı) — impl ayrıntısı.
- Continuation (lastSearch eşleşmesi) yolunda başlık üretimi atlanır (yeni sohbet değil zaten).
- Pagination ifade listesi + Türkçe-fold kuralı saf birim olarak test-first yazılır.

## Kararlar (netleşti)

- Tetik = kullanıcının **"devamını getir" diye yazması** (buton değil — kullanıcı kararı).
- İfade-yakalama **backend'de** (saf/test edilebilir), React'te regex yok — benim çağrım.
- React her mesajda `lastSearch {filters,page}` taşır; eşleşme + lastSearch → page+1 deterministik.
