# Feature Specification: Kâşif Tipli Niyet-Çıkarma (iki-faz kitap arama)

**Feature Branch**: `feat/021-content-discovery-v11`

**Created**: 2026-11-24

**Status**: Draft

**Input**: ScoutAgent kitap aramada iki-faz tipli niyet-çıkarma — ham sohbet sorgusu önce tipli
"niyet-slot" objesine çıkarılır (LLM yalnız slotlar), sonra sunucu deterministik yorumlar + arar.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Bir yazarın eserlerini isteme (Priority: P1)

Kullanıcı sohbete "Mo Yan'ın kitapları" yazar; sonuçta **yalnız Mo Yan'ın yazdığı** kitaplar gelir —
adı benzeyen alakasız kitaplar ("Yan Yana", "Yan Lianke") GELMEZ.

**Why this priority**: Bugünkü en görünür hata. Yazar adı serbest metne düşüp arama kirleniyor;
kullanıcının en temel beklentisi ("bu yazarın kitapları") karşılanmıyor.

**Independent Test**: "Mo Yan'ın kitaplarını listeler misin" sorulur; dönen kartların TÜMÜ Mo Yan
yazımı mı diye bakılır. Tek başına değer üretir (yazar araması güvenilir olur).

**Acceptance Scenarios**:

1. **Given** katalogda Mo Yan'ın N kitabı var, **When** kullanıcı "Mo Yan'ın kitapları" der,
   **Then** dönen kartların hepsi Mo Yan yazımıdır (adı benzeyen başka yazar/başlık gelmez).
2. **Given** yazar adı farklı biçimde ("MO YAN", "mo yan"), **When** aranır, **Then** aynı sonuç
   (büyük/küçük harf duyarsız eşleşme).
3. **Given** yazar katalogda yok, **When** aranır, **Then** dürüst "sonuç bulunamadı" (uydurma kart yok).

---

### User Story 2 - "Hakkında" ile "yazarlık" ayrımı (Priority: P2)

Kullanıcı "X'in kitapları" derse X=yazar (yalnız X yazımı); "X hakkında/üzerine" derse X=konu
(X'le ilgili her şey — biyografi, inceleme dahil). Sistem iki niyeti ayırır.

**Why this priority**: Yanlış ayrım ya alakasız sonuç ya aşırı-dar sonuç verir. Keşfin doğruluğu buna bağlı.

**Independent Test**: "Dostoyevski hakkında kitaplar" → karışık (by+about) döner; "Dostoyevski'nin
kitapları" → yalnız Dostoyevski yazımı. İki sorgu yan yana doğrulanır.

**Acceptance Scenarios**:

1. **Given** "Dostoyevski hakkında kitaplar", **When** aranır, **Then** Dostoyevski yazımı + onunla
   ilgili biyografi/inceleme birlikte gelir (yazar filtresi UYGULANMAZ).
2. **Given** "Dostoyevski'nin kitapları", **When** aranır, **Then** yalnız Dostoyevski yazımı gelir.

---

### User Story 3 - Çok-kriterli sorgu + uydurma yok (Priority: P2)

Kullanıcı tek cümlede birçok kriter verir ("Can Yayınları'ndan ucuzdan pahalıya karanlık distopya
romanları"); her kriter doğru alana düşer; **cümlede olmayan filtre EKLENMEZ**.

**Why this priority**: Karmaşık sorgu ürünün keşif gücü. Yanlış-eşleme ya da uydurma filtre güveni kırar.

**Independent Test**: Çok-kriterli bir sorgu birkaç kez sorulur; yayınevi/sıralama/tür doğru mu +
sorguda geçmeyen sort/stok/indirim uydurulmuş mu diye bakılır.

**Acceptance Scenarios**:

1. **Given** "Can Yayınları'ndan ucuzdan pahalıya karanlık distopya", **When** aranır, **Then**
   yayınevi=Can, sıralama=artan fiyat, tür/ruh-hali metni korunur; başka filtre eklenmez.
2. **Given** "İş Bankası hariç 300 TL altı", **When** aranır, **Then** İş Bankası hariç + fiyat≤300;
   sıralama/stok/indirim UYDURULMAZ (sorguda yok).

---

### User Story 4 - Anladığının şeffaflığı (Priority: P3)

Kullanıcı, sistemin sorgusunu nasıl yorumladığını görebilir (ör. "Yazar: Mo Yan" rozeti) — yanlışsa
sorgusunu yeniden yazar.

**Why this priority**: Güven + hata-kurtarma; ama arama zaten geri dönüşsüz değil, bloklamaz.

**Independent Test**: Bir arama sonrası ekranda anlaşılan filtrelerin gösterilip gösterilmediği bakılır.

**Acceptance Scenarios**:

1. **Given** bir arama yapıldı, **When** sonuç gelir, **Then** sistemin çıkardığı filtreler kullanıcıya
   gösterilir (onay BEKLENMEZ, arama otomatik yapılır).

---

### Edge Cases

- Sorgu hiçbir filtre içermiyorsa (sadece "roman öner") → serbest metin araması, boş slotlar.
- Çıkarma adımı başarısız olursa (model/servis hatası) → kullanıcı anlaşılır hata görür, çökme yok.
- Aynı ad hem yazar hem başlık olabilir ("Tolstoy'un İş Kültür kitapları" — Tolstoy yazar, İş Kültür
  yayınevi) → doğru ayrışır.
- Kapsam-dışı sorgu (hava durumu) → nazik kapsam reddi (chat/router davranışı korunur).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, kitap arama sorgusunu önce yapılandırılmış tipli bir "niyet" objesine çıkarmalı;
  kullanıcının KENDİ kelimeleri ilgili alana yerleşir (çeviri/kanonikleştirme yapılmaz, bu sonraki fazda).
- **FR-002**: Yazar adı, kullanıcı yazarlık ifadesi kullandığında ("X'in kitapları/eserleri", "yazar X")
  ayrı bir yazar alanına yerleşmeli; serbest metne (query) KARIŞMAMALI.
- **FR-003**: "X hakkında/üzerine" (konu) ifadesinde ad yazar alanına DEĞİL serbest metne gitmeli.
- **FR-004**: Sistem, sorguda AÇIKÇA geçmeyen hiçbir filtreyi (sıralama/fiyat/stok/indirim/cilt)
  eklememelidir (uydurma yasağı).
- **FR-005**: Yazar alanı dolu arama, yalnız o yazar(lar)ın eserlerini döndürmeli (ad büyük/küçük harf
  ve boşluk duyarsız eşleşir; eşleşme yoksa boş sonuç).
- **FR-006**: Çıkarılan tipli obje kullanıcı arayüzüne (React) iletilmeli; arayüz bunu gösterebilmeli;
  arama için kullanıcı onayı GEREKMEZ (otomatik ilerler).
- **FR-007**: İkinci faz (yorum + arama) DETERMİNİSTİK olmalı; tipli slotlar mevcut sunucu
  yorumlayıcılarıyla (taksonomi/yayınevi-yazar ad→id/sıralama) çözülür, ek dil-modeli çağrısı gerekmez.
- **FR-008**: Sonuç girişi (kartların üstündeki kısa metin) şablonla üretilebilmeli (zorunlu LLM değil).
- **FR-009**: Katalog dışı niyetler (sepet/yorum/kitaplık vb.) mevcut sohbet/yönlendirme yolunda
  çalışmaya devam etmeli (bu feature yalnız kitap aramayı değiştirir).

### Key Entities *(include if feature involves data)*

- **Niyet-Slot Objesi (tipli)**: Kullanıcı sorgusunun yapılandırılmış hali. Gruplu + her grup opsiyonel
  (grup yok = o konu konuşulmadı): Metin{serbest-sorgu, yazar}, Yayınevi{dahil, hariç}, Fiyat-ifadesi,
  Sıralama-ifadesi, Cilt-ifadesi, Sayfa-ifadesi, Taksonomi, Bayraklar{indirimli, stokta} + sayfalama.
  React↔sunucu arası API kontratı; geniş ama yalnız sunucunun süzebildiği alanlar.
- **Yazar eşleşmesi**: Yazar adı → katalogdaki yazar kimlikleri (normalize eşleşme; mevcut altyapı).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: "Bir yazarın kitapları" aramasında dönen kartların %100'ü o yazara aittir (adı benzeyen
  alakasız sonuç yok) — 10 farklı yazarla doğrulanır.
- **SC-002**: by/about ayrımı 5 örnek sorguda ardışık 3 denemede tutarlı doğru (yazarlık→yalnız yazar,
  hakkında→karışık).
- **SC-003**: Çok-kriterli sorgularda sorguda geçmeyen filtre ekleme (uydurma) oranı %0 — temsilî
  sorgu setinde ardışık denemelerde.
- **SC-004**: Kitap arama turu kullanıcı için ≤1 dil-modeli çağrısı maliyetinde (bugünkü iki-zar
  router+sub-agent yerine tek çıkarma çağrısı).
- **SC-005**: Kullanıcı, sistemin sorgusunu nasıl anladığını her aramada görebilir.

## Assumptions

- Çıkarma adımı yapılandırılmış-çıktı (structured output) üreten bir dil modeliyle yapılır; spike
  mevcut model (gpt-4o-mini) + NESTED şema + uydurma-yasağı/by-about/yayınevi içeren prompt'un zor
  vakalarda tutarlı olduğunu gösterdi (gpt-4o gerekmedi).
- NESTED (gruplu) tipli şema kullanılır; FLAT şema spike'ta ~%50 parse başarısızlığı verdi → kullanılmaz.
- Mevcut arama altyapısı (ParadeDB `SearchAsync`, `SearchRequest`, taksonomi/yayınevi/yazar çözümü)
  yeniden kullanılır; bu feature yeni arama motoru EKLEMEZ.
- Yazar filtresi backend tarafı (SearchRequest.Author + jsonb `?|` AuthorIds) bu oturumda eklendi, hazır.
- Önce catalog (kanıt zemini). Aksiyon sub-agent'larına (sepet/adres) genelleme ayrı dilim; orada
  yazma öncesi ONAY gerekebilir (aramada yok). Bu spec yalnız catalog aramayı kapsar.
- React iki-tur oto-relay yapar (query→extract→typed→search); kullanıcı etkileşimi (düzenle/onayla)
  bu dilimde YOK (ileride eklenebilir, seam hazır).
- Flaky-apology fallback deterministik 2. turda büyük ölçüde gereksizleşir (gözden geçirilir).
