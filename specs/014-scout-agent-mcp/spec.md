# Feature Specification: ScoutAgent (Kâşif) — MCP Niyet-Routing Sohbet

**Feature Branch**: `014-scout-agent-mcp`

**Created**: 2026-11-17

**Status**: Draft

**Input**: ScoutAgent'ı dirilt — LLM serbest metni anlar, doğru BC'nin MCP query tool'unu seçer, cevabı tek sohbet akışında kart olarak döner. Yazma yok; agent yalnız MCP konuşur.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Doğal dille kitap keşfi (Priority: P1)

Ziyaretçi sohbete "Sabahattin Ali'nin kitaplarını listeler misin" gibi serbest cümle yazar; sistem niyeti anlar, kitap aramasını çalıştırır ve eşleşen kitapları aynı sohbette kart olarak gösterir. Düz anahtar-kelime yazması (yalnız "Sabahattin Ali") gerekmez; cümle de çalışır.

**Why this priority**: Bugün kırık olan tam davranış — tam cümle sonuç getirmiyor, yalnız çıplak sorgu getiriyor. Keşfin kalbi; tek başına değer veren MVP.

**Independent Test**: Sohbete doğal cümleyle yazar/kitap adı yaz → eşleşen kitaplar dönsün. Anonim kullanıcı için de çalışmalı (login gerekmez).

**Acceptance Scenarios**:

1. **Given** anonim ziyaretçi, **When** "Sabahattin Ali'nin kitaplarını listeler misin" yazar, **Then** Sabahattin Ali'nin kitapları sohbet akışında kart olarak listelenir.
2. **Given** ziyaretçi, **When** katalogda olmayan bir ad yazar, **Then** sistem dürüstçe "bulunamadı" der, uydurma sonuç üretmez.
3. **Given** ziyaretçi, **When** belirli bir kitabın detayını sorar, **Then** o kitabın künyesi (ad/yazar/fiyat/kapak) kart olarak döner.

---

### User Story 2 - Çok-alanlı niyet yönlendirme (Priority: P2)

Kullanıcı aynı sohbette farklı alanlarda istek yapar — "sepetimde ne var", "siparişlerim", "kitaplığım", "adreslerim", "bu kitabın yorumları", "arama geçmişimi göster". Sistem her isteği doğru alana yönlendirir ve cevabı aynı akışta uygun kartla gösterir. Kişisel alanlar (sepet/sipariş/kitaplık/adres/arama-geçmişi) login ister; anonimde nazikçe girişe yönlendirir. Arama geçmişi SİLME sohbette yapılmaz — agent kullanıcıyı geçmiş ekranına yönlendirir (okuma=sohbet, yazma=tık).

**Why this priority**: Agent'ı sıradan aramadan ayıran şey — tek sohbet yüzeyinden çok alanı okur. P1'den sonra gelir çünkü keşif (P1) tek başına değerli.

**Independent Test**: Login kullanıcı sırayla sepet/sipariş/kitaplık/adres/yorum sorar → her biri doğru veriyle doğru kartta döner; anonim kişisel sorularda girişe yönlendirilir.

**Acceptance Scenarios**:

1. **Given** login kullanıcı, **When** "sepetimde ne var" yazar, **Then** kendi sepet kalemleri kart olarak döner.
2. **Given** login kullanıcı, **When** "siparişlerim" / "kitaplığım" / "adreslerim" sorar, **Then** yalnız kendi kayıtları ilgili kartta döner.
3. **Given** anonim kullanıcı, **When** "sepetimde ne var" sorar, **Then** sistem işlemi yapmaz, girişe nazikçe yönlendirir (hata/çöp cevap değil).
4. **Given** kullanıcı bir kitabın yorumlarını sorar, **When** istek işlenir, **Then** o kitabın yorumları (anonim erişilebilir) döner.
5. **Given** login kullanıcı, **When** "arama geçmişimi göster" yazar, **Then** tekilleştirilmiş arama geçmişi sohbette kart olarak döner (yalnız kendi).
6. **Given** login kullanıcı, **When** "arama geçmişimi sil" yazar, **Then** agent silme yapmaz, geçmiş ekranına yönlendirir (sohbette yazma yok).

---

### User Story 3 - Sonuç sayısı ve devam (Priority: P3)

Arama 20'den fazla sonuç verdiğinde sistem toplam sayıyı gösterir ("40 sonuç · 1–20"); kullanıcı "devamını getir" deyince sonraki 20 aynı akışa eklenir. Toplam sayı tool verisinden birebir gösterilir (LLM saymaz).

**Why this priority**: Keşfi tamamlar ama P1/P2 onsuz da kullanışlı. Anonimde de çalışır (bağlam istemci tarafında taşınır).

**Independent Test**: 20'den çok sonuçlu bir arama yap → toplam sayı görünsün → "devamını getir" → sonraki 20 eklensin, tekrar göstermesin.

**Acceptance Scenarios**:

1. **Given** 40 sonuçlu arama, **When** sonuç döner, **Then** toplam "40" ve gösterilen aralık "1–20" doğru görünür.
2. **Given** ilk sayfa gösterildi, **When** "devamını getir" yazılır, **Then** 21–40 aynı akışa eklenir, 1–20 tekrarlanmaz.
3. **Given** anonim kullanıcı ilk sayfayı gördü, **When** "devamını getir" yazar, **Then** devam login zorlamadan gelir.

---

### User Story 4 - Login arama kaydı korunur (Priority: P3)

Login kullanıcının çalışan dolu-sorgu araması arama geçmişine (013) işlenmeye devam eder; anonim arama kaydolmaz. Arama sohbet/agent yoluna taşınsa da bu davranış birebir korunur.

**Why this priority**: Regresyon koruması — 012/013 yatırımı sohbet geçişinde kör kalmamalı. Kullanıcıya görünür yeni değer katmaz, o yüzden P3.

**Independent Test**: Login kullanıcı sohbetten arama yap → arama geçmişinde görünsün; anonim arama yap → geçmişe hiç yazılmasın.

**Acceptance Scenarios**:

1. **Given** login kullanıcı, **When** sohbetten dolu-sorgu arama yapar, **Then** sorgu arama geçmişinde (013) belirir.
2. **Given** anonim kullanıcı, **When** arama yapar, **Then** hiçbir arama kaydı oluşmaz.

---

### Edge Cases

- Bir alt-sistem (BC) erişilemezse: o alan geçici kullanılamaz der, diğer alanlar çalışmaya devam eder (tüm sohbet çökmez).
- Kullanıcı kapsam-dışı konu sorarsa (hava durumu vb.): sistem nazikçe kapsamını söyler, veri uydurmaz.
- Boş/anlamsız mesaj: netleştirme ister ya da kibarca yönlendirir, çöp sonuç dönmez.
- Çok uzun sohbet: eski turlar özetlenir, bağlam maliyeti sınırlı kalır, devamlılık korunur.
- Login kullanıcı başkasının sohbetine/verisine erişmeye çalışır: reddedilir (yalnız kendi).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem kullanıcının serbest-metin mesajından niyeti anlamalı ve doğru alan okumasını seçmeli (katalog arama, kitap detayı, sepet, sipariş, kitaplık, adres, kitap yorumları).
- **FR-002**: Sistem katalog aramasını kitap adı VEYA yazar adıyla yapmalı; doğal cümledeki dolgu/soru kelimeleri sonucu bozmamalı.
- **FR-003**: Sistem sonucu tool dönüşünden birebir göstermeli; fiyat/kapak/künye/sayı uydurmamalı. Sonuç boşsa dürüstçe "bulunamadı" demeli.
- **FR-004**: Tüm agent okumaları salt-okuma olmalı; sepete-ekleme/sipariş/yorum-yazma/adres-değişikliği agent üzerinden YAPILMAMALI (bu aksiyonlar ekran+REST'te kalır).
- **FR-005**: Anonim kullanıcı yalnız açık okumaları yapabilmeli (kitap arama/detay, kitap yorumları); kişisel okumalar (sepet/sipariş/kitaplık/adres/arama-geçmişi) login istemeli.
- **FR-005a**: Kullanıcı sohbetten kendi arama geçmişini (tekilleştirilmiş) okuyabilmeli; arama geçmişini SİLME sohbette YAPILMAMALI — agent geçmiş ekranına yönlendirmeli (okuma=sohbet, yazma=tık).
- **FR-006**: Kişisel okumalar yalnız isteyen kullanıcının kendi verisini dönmeli; başka kullanıcının verisine erişim engellenmeli.
- **FR-007**: Cevaplar tek sohbet akışında, istek türüne uygun kart biçiminde sunulmalı.
- **FR-008**: Sohbet mesaj-güdümlü olmalı; arama sorgusu URL'de taşınmamalı. Arama geçmişinden "tekrar ara" da sohbete mesaj göndermeli.
- **FR-009**: 20'den çok sonuçta sistem toplam sayıyı (tool verisinden) göstermeli; "devamını getir" talebinde sonraki sayfayı aynı akışa eklemeli, önceki sonuçları tekrarlamamalı.
- **FR-010**: Login kullanıcının sohbeti kalıcı saklanmalı (cihazlar-arası sürdürülebilir); anonim sohbet oturumluk olmalı (kalıcı saklama yok), devamlılık istemci tarafından taşınmalı.
- **FR-011**: Uzun sohbette eski turlar özetlenerek bağlam maliyeti sınırlandırılmalı; devamlılık korunmalı.
- **FR-012**: Login kullanıcının çalışan dolu-sorgu araması mevcut arama-geçmişi sinyalini (012/013) tetiklemeye devam etmeli; anonim arama kaydolmamalı.
- **FR-013**: Bir alan okuması başarısız olursa yalnız o alan etkilenmeli; sistem diğer isteklere cevap vermeye devam etmeli.
- **FR-014**: Kapsam-dışı isteklerde sistem veri uydurmadan kapsamını bildirmeli.

### Key Entities *(include if feature involves data)*

- **Sohbet (Conversation)**: Login kullanıcının kalıcı sohbeti — sahip kullanıcı, mesaj dizisi (yalnız metin), rolling özet. Kullanıcı yalnız kendi sohbetini görür/sürdürür.
- **Sohbet mesajı**: Rol (kullanıcı/asistan) + metin + zaman. Hacimli okuma sonuçları mesaja gömülmez.
- **Okuma sonucu (tool result)**: Bir alan okumasının birebir çıktısı (kitap listesi, sepet, sipariş, kitaplık, adres, yorumlar) — sohbette karta dönüşür, değiştirilmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: "{Yazar} kitaplarını listeler misin" türü doğal cümlelerin %95'i, çıplak sorgu kadar isabetli kitap sonucu döndürür (bugün bu oran ~0; cümle sonuç getirmiyor).
- **SC-002**: Kullanıcı tek sohbet yüzeyinden en az 7 ayrı alanı (kitap arama, kitap detayı, sepet, sipariş, kitaplık, adres, yorum, arama-geçmişi) sorabilir ve doğru alana yönlendirilir.
- **SC-003**: Agent hiçbir koşulda kalıcı veri değiştirmez (0 yazma işlemi); tüm agent işlemleri okumadır.
- **SC-004**: Login kullanıcıların sohbetten yaptığı çalışan aramaların %100'ü arama geçmişinde görünür; anonim aramaların %0'ı kaydolur.
- **SC-005**: 20'den çok sonuçlu aramada gösterilen toplam sayı, tool'un döndürdüğü gerçek toplamla birebir eşleşir (LLM kaynaklı sayım hatası 0).
- **SC-006**: Anonim kullanıcı login olmadan kitap arayabilir ve "devamını getir" ile sayfalayabilir.

## Assumptions

- LLM bu dilimde bulut OpenAI gpt-4o-mini'dir (kullanıcı onayı); local-LLM kuzey-yıldızı ayrı dilim. OpenAI API anahtarı mevcuttur.
- Agent BC'lere yalnız MCP query tool'larıyla erişir (first-party, agent-içi). Dış/BYO agent yüzeyi yoktur; bu, CLAUDE.md "MCP geri getirme" kuralını bilinçli tersine çevirir ve ADR ile kayda alınır (teardown çizgisi yalnız dış yüzey için sürer).
- Kapsam yalnız query-only tool'lar: Catalog (search_books, get_book), Basket (get_basket), Order (get_my_orders), Library (get_my_library), Customer (get_my_addresses), Reviews (get_book_reviews), Personalization (get_search_history, login-okuma). Yazma tool'u (arama-geçmişi silme dahil), admin query, toplu/anon arama analitiği kapsam DIŞI — arama-geçmişi silme 013 REST/ekranda kalır.
- React tarafı: ChatWorkspace tek akışta tool başına kart render eder; mevcut ayrı ekranlar (/sepet, /kitapligim, /adreslerim) REST'te kalır (keşif=sohbet, aksiyon=tık).
- 007-mcp-agent branch'indeki ScoutAgent.Api + Catalog/Library/Customer tool'ları başlangıç noktasıdır; söküm-sonrası değişikliklerle (011 hibrit arama, MCP teardown) uzlaştırılır. Basket/Order/Reviews tool'ları + BC `/mcp` uçları yeni yazılır.
- Catalog arama tool'u yazar+başlık ILIKE yolunu kullanır (yazar case'i için doğru); ParadeDB BM25 hibrit konu/semantik aramaya ayrı kalır.
