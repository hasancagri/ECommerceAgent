# Feature Specification: Semantik Arama (Dilim-2)

**Feature Branch**: `feat/026-semantic-search`

**Created**: 2026-11-24

**Status**: Draft

**Input**: 026 Dilim-2 — embedding omurgası (025) üstüne ilk tüketici: betimleyici/kavramsal sorguyu
anlam yakınlığıyla eşleyen arama. Rol-ayrımı (semantik=vektör), RRF yok, yalnız ScoutAgent tüketir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Betimleyici sorgu anlamla eşleşir (Priority: P1)

Kullanıcı Kâşif'e kitabın adını/yazarını bilmeden, ne istediğini **betimleyerek** sorar ("umut veren
hafif bir yaz romanı", "savaşın insanı nasıl değiştirdiğini anlatan"). Sistem sorgunun anlamına yakın
kitapları döndürür — sorgudaki kelimeler kitap metninde geçmese bile.

**Why this priority**: Bugünkü kök boşluk. Lexical (BM25) arama betimleyici sorguda 0 döner; bu, keşif
platformunun "ne istediğini tarif et" vaadinin kalbi. Tek başına değer verir.

**Independent Test**: Kâşif'e ortak kelime içermeyen betimleyici bir sorgu yaz; sonuç kartları anlamca
ilgili kitaplar olsun (BM25'in bulamayacağı). Kart boşsa/alakasızsa başarısız.

**Acceptance Scenarios**:

1. **Given** katalog embed edilmiş, **When** kullanıcı "umut veren hafif bir yaz romanı" der,
   **Then** anlamca uyan romanlar döner (sorgu kelimeleri açıklamada geçmese de).
2. **Given** anlamca hiçbir kitap eşiğe girmiyor, **When** sorgu çok uzaksa, **Then** boş sonuç +
   "uygun kitap bulamadım" (uydurma/alakasız doldurma YOK).

---

### User Story 2 - Niyet doğru yola gider (Priority: P2)

Kullanıcı sorgusu Kâşif'te ayrıştırılır: **demir** (başlık/yazar/ISBN/yayınevi/hard filtre) varsa lexical
yol (`search_books`), yoksa + serbest betimleme varsa **semantik yol** (`semantic_search`). Spesifik
istek anlamla eğilmez; betimleyici istek anlamla eşleşir.

**Why this priority**: Yanlış yola giden sorgu ya 0 döner ya alakasız. Routing olmadan P1 yüzeye çıkmaz.

**Independent Test**: "Dostoyevski" → lexical (yazar); "karanlık distopya" → semantik. İki sorgu ayrı
yollara gitsin (log/dönüş tool adından doğrulanır).

**Acceptance Scenarios**:

1. **Given** sorgu "Suç ve Ceza", **When** çıkarım koşar, **Then** demir var → lexical yol seçilir.
2. **Given** sorgu "kayıp ve yas üstüne sakin bir kitap", **When** çıkarım koşar, **Then** demir yok,
   betimleme var → semantik yol seçilir.

---

### User Story 3 - Yapısal filtre anlamla birlikte uygulanır (Priority: P3)

Betimleyici sorguda yapısal kısıt da olabilir ("stokta, 300 sayfadan az, karanlık bir roman"). Sistem
yapısal filtreyi (stokta/dil/fiyat/sayfa/yayınevi) uygular VE kalanı anlamla sıralar.

**Why this priority**: Gerçekçi sorgular karışık; ama P1/P2 olmadan anlamsız. Filtre = uygunluk,
anlam = yakınlık (rol-ayrımı).

**Acceptance Scenarios**:

1. **Given** "stokta karanlık bir roman", **When** semantik yol koşar, **Then** yalnız stoktaki kitaplar,
   anlamca "karanlık roman"a yakınlık sırasıyla döner.

---

### Edge Cases

- Embed'i olmayan ürün (henüz backfill'lenmemiş): kNN'de yoktur — sessizce atlanır, hata değil.
- Eşiğe hiçbir kitap girmezse: boş sonuç (alakasız doldurma yok).
- Yapısal filtre tüm adayları elerse: boş sonuç.
- Anonim kullanıcı: semantik arama serbest; `UserSearched` yayınlanmaz (login şart).
- OpenAI sorgu-embed hatası: nazik hata, sonuç dönmez (arama diğer yolları etkilemez).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem betimleyici bir metin sorgusunu anlam vektörüne çevirip (OpenAI, omurgayla aynı
  model) `product_embedding` üzerinde en-yakın-komşu (kosinüs) aramasıyla eşleştirMELİ.
- **FR-002**: Sonuçlar kosinüs-mesafe **eşiğinin** altındakilerle sınırlı olMALI (eşik yapılandırılabilir);
  eşiğin dışındakiler dönMEMELİ (alakasız doldurma yok).
- **FR-003**: Semantik arama AYRI bir yüzey (`semantic_search`) olMALI; mevcut lexical/filtre araması
  (`search_books` / REST `/products/search`) DEĞİŞMEMELİ.
- **FR-004**: ScoutAgent niyet-çıkarımı, sorguda **demir** (başlık/yazar/ISBN/yayınevi/hard filtre) YOKsa
  ve serbest betimleme VARsa semantik yolu seçMELİ; aksi halde lexical yol.
- **FR-005**: Semantik sonuçlara yapısal hard filtre (stokta/dil/fiyat/sayfa/yayınevi) uygulanaBİLMELİ;
  filtre uygunluğu belirler, vektör yakınlığı sıralar. Lexical (BM25) metin eşleşmesi KARIŞMAMALI (RRF yok).
- **FR-006**: Sonuç sırası vektör yakınlığına (kosinüs mesafesi artan) göre olMALI; sonuç künyeleri
  mevcut arama kart şekliyle AYNI olMALI (React değişmeden render eder).
- **FR-007**: Login kullanıcıda semantik arama `UserSearched` (mode="semantic") yayınlaMALI; yayın
  ayrık + hata-yutar (arama sonucunu etkilemez). Anonim yayınlamaz.
- **FR-008**: Embed'i olmayan ürün sonuçta görünMEMELİ (kNN doğal dışlar); eksiklik hata üretmeMELİ.

### Key Entities

- **product_embedding** (025'ten): ürün başına anlam vektörü (`vector(1536)`). Bu feature yalnız OKUR
  (yazım 025 worker'ında). kNN kaynağı.
- **Semantik sorgu**: kullanıcının serbest betimleyici metni; anlık embed edilir, saklanmaz (cache yok).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bugün BM25'te 0 dönen betimleyici sorguların en az %80'i anlamca ilgili ≥1 kitap döndürür
  (canlı örnek sorgu kümesiyle doğrulanır).
- **SC-002**: Spesifik sorgu (başlık/yazar) semantik yola GİTMEZ — demir içeren sorguların %100'ü lexical
  yola yönlenir (routing testi).
- **SC-003**: Yapısal filtreli semantik sorguda dönen her kitap filtreyi karşılar (ör. "stokta" → hepsi stokta).
- **SC-004**: Alakasız sonuç yok: eşik dışı kitap dönmez; anlamca uzak sorgu boş döner (kullanıcı
  "aradığım bu değil" demez — örnek kümede kalibre).

## Assumptions

- Embedding omurgası (025) canlı ve katalog büyük oranda embed edilmiş (eksikler worker'la kapanır).
- Eşik (cosine-distance cutoff) başlangıç 0.68 (067 Türkçe↔İngilizce kalibrasyonu); canlı ayarlanır.
- Filtre+kNN sırası: yapısal pre-filter → kNN aday (067 deseni); canlı ölçümle ters çevrilebilir.
- Sorgu-embed cache YOK (her semantik arama 1 OpenAI çağrısı); tekrar-sorgu cache'i backlog.
- Yüzey yalnız ScoutAgent (agent-içi MCP); dış/REST semantik ucu yok.
- Kapsam yalnız Catalog `semantic_search` + ScoutAgent routing. similar_books V2 (Dilim-3) ve
  taste-vektör kişiselleştirme (Dilim-4) bu spec'in DIŞINDA.
