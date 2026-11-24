# Research — Kâşif Tipli Niyet-Çıkarma (Faz 0)

Tüm kararlar 2026-11-24 spike'ıyla (canlı, ScoutAgent `/spike-extract` geçici ucu) kanıtlandı.

## D1: Çıkarma mekanizması = MEAI strict structured output
- **Karar:** `IChatClient.GetResponseAsync<T>(messages)` (Microsoft.Extensions.AI 10.7, OpenAI backing)
  ile tipli obje üret. LLM serbest JSON yazmaz; şemaya uyar.
- **Gerekçe:** Mevcut gevşek tool-calling param-doldurma kırılgan (yazar adını query'ye koyuyor, 6/6
  başarısız). Structured output şemayı zorlar → mekanik güvenilir.
- **Alternatifler:** (a) Prompt-sertleştirme saf tool-call'da — denendi, tutmadı. (b) gpt-4o'ya yükselt
  — gerekmedi (mini yetti). (c) Sunucu-tarafı regex çıkarma — kırılgan, LLM niyet-okuması daha esnek.

## D2: Şema şekli = NESTED (gruplu), FLAT değil
- **Karar:** Gruplu nullable alt-objeler (`Text{Query,Author}`, `Publisher{Include,Exclude}`, `Flags{…}`).
- **Gerekçe:** Spike ölçümü — FLAT ~%50 parse-fail (Q2 3/6, Q4 4/6 `ok=false`); NESTED 6/6 parse OK.
  Grup=null "konuşulmadı" sinyali net; model grup-grup daha küçük/temiz karar verir.
- **Alternatif:** FLAT (daha az tip) — ölçülen parse güvenilmezliği nedeniyle red.

## D3: Prompt = uydurma-yasağı + by/about + yayınevi-tanıma (mini yeter)
- **Karar:** Extract system prompt üç kuralı AÇIK içerir: (1) sorguda geçmeyen alanı doldurma (uydurma
  yasağı), (2) "X'in kitapları"→Author / "X hakkında"→Query, (3) yayınevi kalıpları→Publisher.
- **Gerekçe:** Spike v1 prompt → by/about 1/3, yayınevi 1/3, sort/stok uyduruyordu. v2 prompt (bu üç
  kural + örnekler) → hepsi **3/3 tutarlı**, uydurma gitti. gpt-4o gerekmedi.
- **Alternatif:** Few-shot ağır prompt / gpt-4o — gerekmedi, v2 yetti.

## D4: İki faz, iki uç (oto-relay), onay YOK
- **Karar:** `/extract` (query→typed, LLM) + `/search` (typed→kartlar, deterministik). React Tur-1 alıp
  beklemeden Tur-2 relay eder. Arama onayı YOK (geri dönüşsüz değil).
- **Gerekçe:** Faz ayrımı: LLM yalnız çıkarma (güvenilir olduğu iş), yorum+arama kod. Toplam 1 LLM turu
  (bugünkü router+sub-agent 2-zar yerine). Onay sürtünmesi arama UX'ini bozar.
- **Alternatif:** (a) Tek uç echo — React tipi gösterir, relay yok → tek çağrı; kullanıcı (b) oto-relay
  iki-tur seçti (seam ileride etkileşimli (a)'ya hazır). (b) kabul.

## D5: Yorum (Tur-2) mevcut Catalog yorumlayıcılarını reuse eder
- **Karar:** ScoutAgent NestedFilters'ı `search_books` argümanlarına düzler (`FilterInterpreter`, saf);
  taksonomi/sort-metni/binding-metni/ad→id yorumunu Catalog `search_books` ZATEN yapar (reuse).
- **Gerekçe:** DRY değil ama tek-yorum-yeri: Catalog `BookTaxonomy.ExtractFromText` + `ParseSort` +
  `ResolvePublisherIds`/`ResolveAuthorIds` kurulu. ScoutAgent sadece slot→arg eşler.
- **Açık/küçük:** Fiyat/sayfa HINT metni ("300 TL altı"→max=300) sayıya parse — `FilterInterpreter`'da
  saf+test-first (İlke VI). Alternatif: search_books'a priceHint metin param ekleyip Catalog'da parse
  (sort/binding metni deseni gibi) — tek-yorum-yeri tutarlılığı için tercih edilebilir; tasks'ta netleşir.

## D6: Conversation kaydı + pagination korunur
- **Karar:** `/search` turu Conversation'a yazar (017 sidebar/geçmiş bozulmaz); "devamını getir" =
  typed.Page+1 (React son typed'ı taşır, mevcut lastSearch deseni). `/extract` durumsuz.
- **Gerekçe:** Kitap arama ana sohbet kullanımı; kayıt/pagination/sidebar yaşamalı.

## D7: Flaky-apology fallback → gözden geçir
- **Karar:** Deterministik Tur-2'de catalog param'ı artık LLM orkestrasyonu doldurmadığından flaky
  büyük ölçüde yok olur; fallback (searchCalled=False→zorla ara) catalog `/search` yolunda GEREKSİZ.
  Mevcut `/messages` (router+sub-agent) yolu diğer niyetler için dururken fallback orada kalır.
- **Gerekçe:** Çift yol geçiş dönemi: catalog yeni iki-faz yola geçer, gerisi chat'te. Temizlik tasks'ta.

## Kapsam notu
Önce catalog (kanıt zemini). basket/customer gibi aksiyon sub-agent'larına genelleme AYRI dilim —
orada yazma öncesi ONAY (İlke: geri dönüşlü olmayan aksiyon) gerekebilir. Bu feature yalnız catalog arama.
