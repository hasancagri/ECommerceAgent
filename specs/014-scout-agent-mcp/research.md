# Research — ScoutAgent (Kâşif) MCP Routing

Clarify oturumu brainstorm'da yapıldı; NEEDS CLARIFICATION yok. Kararlar + gerekçe:

## D1 — Taşıma: MCP vs REST function-tool

- **Karar:** Agent BC'lere YALNIZ MCP ile erişir.
- **Gerekçe:** Kullanıcı kararı — agent kimliği = MCP tüketicisi; REST'le çağıran sıradan orkestratör olur. İlke I MCP'yi meşru context-arası kanal sayar; conventions "MCP yalnız agent tüketir".
- **Alternatif (red):** REST/gRPC function-tool — routing davranışı aynı ama kullanıcı MCP istedi; CLAUDE.md operasyonel yasağı ADR'yle tersine çevrilir (anayasa ihlali yok).

## D2 — Catalog arama uzlaşması: ILIKE vs BM25 hibrit

- **Karar:** `search_books` tool'u 007'nin ILIKE yazar+başlık yolunu tutar (yazar-önce sıralı).
- **Gerekçe:** Bug'ın kökü — BM25 (011) yalnız Name/Description/Tags arar, yazarı aramaz; "Sabahattin Ali" tesadüfen çalışıyordu. ILIKE yazar-eşleşmesi doğru sonucu verir.
- **Alternatif (red):** `search_books`→hibrit BM25 sarmala — yazar case'ini çözmez. Hibrit konu/semantik aramaya ayrı kalır (bilinçli iki yol).

## D3 — Niyet routing mekanizması

- **Karar:** LLM tool-calling (`UseFunctionInvocation`) + açıklayıcı tool description'ları + sistem prompt. Model niyeti anlar, tool seçer; netleştirme sormadan arar.
- **Gerekçe:** 007 as-built; T042 canlı geçti. Routing kalitesi tool description + prompt'tan gelir, ayrı sınıflandırıcı gerekmez.
- **Alternatif (red):** Kural/regex niyet sınıflandırıcı — kırılgan, çok-alan ayrımı zayıf, sohbet değil.

## D4 — Anon sayfalama + devamlılık

- **Karar:** Anon arama login zorlamaz; "devamını getir" istemci-transkriptle çalışır (tarayıcı son turları POST'ta yollar). Login → sunucuda `Conversation`.
- **Gerekçe:** Keşif birinci sınıf olmalı; login zorlamak yanlış. İstemci-transkript bu repo'nun bilinen stateless-continuity deseni.
- **Alternatif (red):** Sayfalama login-only — anon keşfi sakatlar.

## D5 — Sayfa izi

- **Karar:** Cevap metni sayfa-izi taşır ("… 1. sayfa"); transkriptte durur, "devam" → page+1. Ayrı durum tablosu yok.
- **Gerekçe:** En az parça; tool sonuçları (items) transkripte girmez ama metin girer. Hem anon hem login'de çalışır.
- **Alternatif (red):** Konuşma-başı "son sorgu+sayfa" durum kaydı — fazladan parça, anon'da zaten istemci taşır.

## D6 — Toplam sayı kaynağı

- **Karar:** Kart altlığı toplam sayıyı tool `Total` alanından deterministik gösterir; LLM saymaz.
- **Gerekçe:** MCP kural #1 "kendi hesabınla değer üretme"; LLM sayımı güvenilmez.

## D7 — Arama kaydı (012/013)

- **Karar:** Yalnız login+dolu-sorgu `UserSearched` yayar; anon sessiz. Yayın REST arama ucundan `search_books` tool yoluna taşınır.
- **Gerekçe:** Arama sohbete taşınınca eski yayın noktası kör kalır; invariant birebir korunur. Toplu/anon analitik bilinçli dışarıda.

## D8 — LLM

- **Karar:** Bu dilim bulut OpenAI gpt-4o-mini (007 as-built). Key Options'ta (user-secret/AppHost param), git'e girmez.
- **Gerekçe:** Kullanıcı onayı; local-LLM kuzey-yıldız ayrı dilim.

## D9 — Tool seti + auth-gating

- **Karar:** 8 query tool. Anon-açık: `search_books`, `get_book`, `get_book_reviews`. Login: `get_basket`, `get_my_orders`, `get_my_library`, `get_my_addresses`, `get_search_history`. Yazma tool YOK.
- **Gerekçe:** keşif=sohbet(oku), aksiyon=tık(yaz). Anon kişisel tool'u HİÇ görmez (model 403 görmez — deneyim katmanı).

## D11 — Arama geçmişi okuma tool'u (REST-only kararı revize)

- **Karar:** Personalization'a `get_search_history` MCP tool'u (login-okuma) eklenir. D7'nin "Personalization REST-only" kararı KISMEN tersine: okuma tool'u evet, yazma hayır.
- **Gerekçe:** Tutarlılık — diğer tüm kişisel okumalar sohbette; kullanıcı "geçmişimi göster" der, tool olmazsa duvara toslar. 013 REST yüzeyi (GET+DELETE) aynen durur; silme sohbette yok, agent ekrana yönlendirir (okuma=sohbet, yazma=tık).
- **Alternatif (red):** REST-only tut, agent "geçmiş ekranına bak" desin — diğer alanlarla tutarsız, kötü sohbet deneyimi.

## D10 — Reviews/Basket okuma yüzeyi yeni

- **Karar:** Basket `get_basket` + Reviews `get_book_reviews` sıfırdan query slice (master'da okuma slice'ı yok).
- **Gerekçe:** Söküm bunların dış yüzeyini kaldırmıştı; aggregate/read-model duruyor, üstüne query+MCP sarmalayıcı yazılır.
