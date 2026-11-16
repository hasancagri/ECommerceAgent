# Feature Specification: Hibrit Arama (BM25 + Semantik + RRF)

**Feature Branch**: `011-hybrid-search`

**Created**: 2026-11-16

**Status**: Draft

**Input**: Müşteri hem doğru kelimeyle hem anlamla kitap bulabilsin. Catalog verisi (008 açıklaması + 009 etiketleri) üstünde üç bağımsız arama ayağı: kelime-tabanlı alaka sıralaması, anlam-tabanlı benzerlik, ve ikisinin füzyonu.

> **Kademe (conventions):** Tam akış — yeni arama altyapısı (BM25 index + vektör deposu), offline embedding aracı, yeni sorgu yüzeyi. Arama mantığı çoğu SQL/pg (host-gerekli, canlı doğrulanır — İlke VI saf domain dışı); saf birimler (RRF skoru, parametre validasyonu) test-first.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kelimeyle isabetli arama (Priority: P1) 🎯 MVP

Müşteri arama kutusuna kelime(ler) yazar ("Orwell distopya", "1984", "savaş ve barış") ve en alakalı kitapları **alaka sırasıyla** görür. Sıralama kelime-sıklığının doymasını, nadir kelimenin ağırlığını ve metin uzunluğunu hesaba katar — spesifik/nadir terim yazan isabetli sonuç alır. Embedding gerektirmez; 008/009 verisi (ad + açıklama + etiket) üstünde çalışır.

**Why this priority**: Arama kutusunun olmazsa olmazı; en hızlı canlıya çıkan, embedding'e bağımsız ayak. Yazar adı/başlık/nadir terim aramasını doğru sıralar.

**Independent Test**: Bilinen sorgular ("Orwell", "distopya", "1984") → beklenen kitap(lar) ilk sıralarda; alakasız kitaplar altta; boş/anlamsız sorgu güvenli boş sonuç.

**Acceptance Scenarios**:

1. **Given** "Orwell" aranır, **When** sonuç gelir, **Then** Orwell kitapları üst sıralarda (yazar adı yüksek alaka).
2. **Given** "distopya" (nadir etiket/kelime) aranır, **When** sonuç gelir, **Then** o temayı taşıyan kitaplar, kelimeyi bir kez geçen uzun açıklamalardan önce sıralanır.
3. **Given** boş veya yalnız boşluk sorgu, **When** aranır, **Then** hata değil boş/anlamlı sonuç döner.
4. **Given** Türkçe İ/ı farklı yazım ("istanbul" vs "İstanbul"), **When** aranır, **Then** ikisi aynı sonucu bulur (büyük/küçük + İ/ı duyarsız).

---

### User Story 2 - Anlamla arama ve benzer kitaplar (Priority: P2)

Müşteri kelimeleri birebir bilmeden niyetini yazar ("yalnızlığı anlatan sıcak bir roman", "geleceğe dair karanlık bir uyarı") ve anlamca yakın kitapları görür — aranan kelimeler kitapta geçmese bile. Ayrıca bir kitabın detayında "benzer kitaplar" anlam komşuluğundan gelir.

**Why this priority**: Kelime örtüşmediğinde BM25'in göremediği anlam-yakınlığını yakalar; keşif derinleşir. BM25'ten sonra çünkü embedding üretimi (offline, bir kez) ön hazırlık ister.

**Independent Test**: Kelime-dışı niyet sorgusu ("kayıp ve keder üstüne") → tematik olarak uygun kitaplar döner; ünlü bir kitabın "benzer"leri aynı tema/tür kümesinden.

**Acceptance Scenarios**:

1. **Given** "yalnızlığı anlatan sıcak bir roman" (bu kelimeler kitapta geçmez), **When** aranır, **Then** tematik olarak yalnızlık/insan ilişkileri kitapları döner.
2. **Given** bilinen bir kitap, **When** "benzer kitaplar" istenir, **Then** komşular aynı tema/tür ailesinden (rastgele değil).
3. **Given** embedding'i olmayan (yeni/işlenmemiş) kitap, **When** semantik aranır, **Then** o kitap semantik sonuçta yoktur ama sistem hata vermez.

---

### User Story 3 - Hibrit sonuç (füzyon) (Priority: P3)

Müşteri tek arama kutusuna ne yazarsa yazsın (kesin kelime veya anlam) iyi sonuç alır: sistem kelime-tabanlı ve anlam-tabanlı iki sıralamayı **tek listede birleştirir**. Böylece "yazar adı + his" karışık sorgularda her iki tarafın gücü toplanır; birinin kaçırdığını öteki yakalar.

**Why this priority**: En iyi deneyim ama iki ayağın (US1+US2) üstüne kurulur; onlar olmadan birleştirilecek bir şey yok.

**Independent Test**: Hem kelime-ağırlıklı ("Orwell") hem anlam-ağırlıklı ("karanlık gelecek uyarısı") sorguların ikisi de hibrit sonuçta iyi; yalnız-BM25 veya yalnız-vektörün zayıf kaldığı sorguda hibrit daha iyi.

**Acceptance Scenarios**:

1. **Given** "Orwell karanlık gelecek" (yazar adı + tema), **When** hibrit aranır, **Then** hem Orwell kitapları (kelime) hem distopik eserler (anlam) üst sıralarda birleşir.
2. **Given** iki ayakta da üst sıralarda olan bir kitap, **When** füzyon uygulanır, **Then** o kitap hibrit sonuçta en üstlerde (iki listedeki konumu toplanır).
3. **Given** yalnız bir ayakta çıkan kitap, **When** füzyon uygulanır, **Then** elenmez ama tek-liste katkısıyla sıralanır.

---

### Edge Cases

- Çok uzun / çok kısa sorgu → güvenli sonuç (hata yok); aşırı uzun sorgu makul biçimde kesilir.
- Sonuç bulunamayan sorgu → boş liste + anlamlı "sonuç yok" (hata değil).
- Embedding henüz üretilmemiş katalog bölümü → semantik/hibrit o kitapları atlar, BM25 yine çalışır.
- Türkçe karakter/aksan farkları (İ/ı, ş/s, ç/c) → eşleşmeyi bozmaz.
- Anonim kullanıcı → arama açık (keşif login istemez).

## Requirements *(mandatory)*

### Functional Requirements

**US1 — BM25 keyword**

- **FR-001**: Sistem, ürün metni (ad + açıklama + etiket) üstünde kelime araması yapıp sonuçları **alaka skoruna** göre sıralamalı (kelime sıklığı doyması + nadirlik ağırlığı + uzunluk normalizasyonu).
- **FR-002**: Arama Türkçe-duyarsız olmalı: büyük/küçük harf ve İ/ı ayrımı sonucu değiştirmemeli.
- **FR-003**: Boş/anlamsız sorgu hata üretmemeli; boş veya anlamlı sonuç dönmeli.
- **FR-004**: Sonuçlar sayfalanabilmeli (limit + sıralama korunur).

**US2 — Semantik**

- **FR-005**: Her ürün için metin-anlamını temsil eden bir vektör **offline** üretilip saklanmalı (arama anında üretilmez; bir kez hazırlanır).
- **FR-006**: Sistem, serbest-metin niyet sorgusunu anlamca en yakın ürünlere eşlemeli — aranan kelimeler üründe geçmese bile.
- **FR-007**: Bir ürün verildiğinde anlamca en yakın "benzer kitaplar" listelenebilmeli.
- **FR-008**: Vektörü olmayan ürün semantik sonuçta yer almamalı ama sistem hata vermemeli (kısmi kapsam güvenli).

**US3 — RRF füzyon**

- **FR-009**: Sistem, kelime-tabanlı ve anlam-tabanlı iki sıralamayı tek sonuç listesinde birleştirmeli; birleştirme iki listedeki **konuma** dayanmalı (ham skorların ölçek uyumsuzluğundan bağımsız).
- **FR-010**: Her iki listede üst sıralarda olan ürün birleşik sonuçta öne çıkmalı; yalnız bir listede olan elenmemeli.
- **FR-011**: Füzyon yöntemi değiştirilebilir olmalı (ileride farklı birleştirme denenebilir — tasarım bunu kilitlemez).

**Ortak**

- **FR-012**: Arama yalnız Catalog verisi üstünde çalışmalı; başka BC'ye senkron çağrı yapmamalı.
- **FR-013**: Arama anonim kullanıcıya açık olmalı (keşif login gerektirmez).

### Key Entities *(include if feature involves data)*

- **Ürün arama metni**: Ad + açıklama + etiketlerin aranabilir birleşimi (BM25 bunun üstünde skorlar).
- **Ürün vektörü (embedding)**: Ürün metninin anlam temsili; offline üretilir, saklanır; benzerlik/semantik aramanın temeli.
- **Arama sonucu**: Sıralı ürün listesi + alaka/benzerlik sinyali; sayfalanabilir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bilinen kelime sorgularının (yazar adı, tam başlık, nadir terim) en az %90'ında beklenen kitap ilk 10 sonuçta.
- **SC-002**: Kelime-dışı niyet sorgularında (anlam) dönen ilk 10 sonucun en az %70'i konuyla tematik uyumlu (elle değerlendirme örneği).
- **SC-003**: Bir ünlü kitabın "benzer kitaplar" ilk 10'u, tema/tür olarak ilgili (rastgele değil) — örnek kümede doğrulanır.
- **SC-004**: Hibrit sonuç, hem kelime-ağırlıklı hem anlam-ağırlıklı test sorgularında, tek-ayak (yalnız BM25 / yalnız vektör) sonuçtan en az eşit; karışık sorguda belirgin daha iyi.
- **SC-005**: Türkçe İ/ı ve büyük/küçük harf varyasyonları aynı sonucu döndürür (%100 örnek kümede).
- **SC-006**: Tipik arama sonucu kullanıcıya hızlı döner (etkileşimli his — saniyenin altı).

## Assumptions

- **Arama altyapısı**: BM25 (pg_search) + vektör benzerlik (pgvector) tek veritabanında (ParadeDB image). Diğer BC'ler etkilenmez (üst-küme postgres; eklenti catalogDb'ye özel).
- **Embedding üretimi**: OpenAI offline batch aracı (008 deseni; Python, ISBN-anahtarlı resumable cache, `text-embedding-3-small`). Ollama bilinçli ELENDİ (yerel kaynak zorluyor). Vektör üretimi solution-dışı araç; üretim bir kez, sonra DB'ye yüklenir.
- **Veri zemini hazır**: 008 zengin açıklama + 009 etiketleri embed/BM25 metnini besler; yeni veri gerekmez.
- **Türkçe**: `InvariantGlobalization` + mevcut `NameNormalization` fold'u zemini; BM25 Türkçe tokenizer kullanılır.
- **Yüzey**: Arama REST ve/veya ScoutAgent sohbet tool'uyla açılır (keşif anonim). Kesin yüzey plan'da.
- **Kapsam dışı**: Reco/kişiselleştirme motoru (ONNX/MLflow yönü — ayrı), zevk-vektörü, kişisel liste, gösterim logu/CTR, ödeme/sepet. Bu dilim yalnız içerik-tabanlı arama/benzerlik.
- **Kısmi embedding güvenli**: Embedding üretimi uzun sürebilir; BM25 (US1) embedding'den bağımsız tam çalışır, semantik kısmi kapsamda da hata vermez.
