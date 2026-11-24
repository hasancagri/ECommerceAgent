# Feature Specification: Taste-vektör kişiselleştirme (Dilim-4)

**Feature Branch**: `feat/028-taste-personalization`

**Created**: 2026-11-24

**Status**: Draft

**Input**: 028 Dilim-4 — kuzey yıldızı. Kullanıcıyı kitap embedding uzayına bir NOKTA (taste-vektör) koy →
kNN = kişisel öneri. Cold-start (0 satın-alma/yorum, ~41 arama); MVP arama-sorgusu merkezli taste + küratörlü fallback.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - "Sana özel" kişisel öneri (Priority: P1)

Login kullanıcı, hiçbir arama yazmadan Kâşif feed'ini açtığında (sorgu yok), geçmiş aramalarından türeyen
**zevk profiline** yakın kitaplar görür. Çok aradığı temalara/ruh haline uygun öneriler, aradığı kitapları
tekrar değil, çeşitli.

**Why this priority**: Kuzey yıldızı — bugün hiç olmayan yetenek. Taşınabilir zevk profili moat'unun ilk ayağı.
Tek başına değer (kişisel keşif).

**Independent Test**: Birkaç (≥3) arama yapmış login kullanıcı feed'i açar → önerilen kitaplar aramalarının
anlam-merkezine yakın (alakasız/rastgele değil); sorgu yazmadan gelir.

**Acceptance Scenarios**:

1. **Given** kullanıcının ≥3 arama kaydı var, **When** "sana özel" feed açılır, **Then** zevk-noktasına kNN
   kitaplar döner (hard filtre geçer, kullanıcının gördüğü/sahip olduğu hariç, çeşitli).
2. **Given** kullanıcının <3 sinyali var (yeni/cold), **When** feed açılır, **Then** **küratörlü** fallback
   (çok-satan/tür) döner — kişisel değil, ama boş değil.
3. **Given** anonim kullanıcı, **When** feed açılır, **Then** küratörlü (kişisel yok; sinyal yok).

---

### User Story 2 - Spesifik sorgu kişiselliği ezer (Priority: P2)

Kullanıcı feed'de değil, belirli bir şey ararsa (yazar/başlık/betimleme), **kişisellik devreye girmez** —
mevcut lexical/semantik yollar çalışır. Kişisel = yalnız sorgu-YOK feed (YouTube-modeli).

**Why this priority**: P1'in sınırını netler; spesifik niyeti zevkle eğmek yanlış (saygısızlık). Mevcut
arama yolları bozulmadan korunur.

**Acceptance Scenarios**:

1. **Given** kullanıcı "Dostoyevski" arar, **When** sonuç döner, **Then** yazar araması (lexical) — taste karışmaz.
2. **Given** feed (sorgu yok), **When** açılır, **Then** taste-kişisel.

---

### User Story 3 - Sinyal biriktikçe profil güçlenir (Priority: P3)

Kullanıcı aradıkça (gelecekte: satın aldıkça/yorum yazdıkça) zevk profili zenginleşir; öneriler zamanla
kullanıcıya daha uygun olur. Satın-alma/yorum sinyali aramadan **daha ağır** sayılır (kablo hazır, veri gelince).

**Why this priority**: Profilin yaşayan olduğunu gösterir; ama P1/P2 olmadan anlamsız. Satın-alma/yorum verisi
şu an 0 → gözlemlenemez, kablo doğrulanır.

**Acceptance Scenarios**:

1. **Given** kullanıcı yeni arama yapar, **When** feed yeniden açılır, **Then** zevk-noktası yeni aramayı yansıtır.

---

### Edge Cases

- Embed'i olmayan ürün öneriye girmez (kNN doğal dışlar).
- Kullanıcının tüm aramaları embed edilemezse (OpenAI hatası) → o oturumda küratörlü fallback (öneri boş kalmaz).
- Zevk-noktasına hard filtre tüm adayları elerse → küratörlü fallback.
- Kullanıcı geçmişini sildiyse (013) → sinyal azalır/sıfırlanır → eşiğin altına düşerse küratörlü.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem login kullanıcının geçmiş arama sorgularından bir **zevk-vektörü** (embedding uzayında
  tek nokta, ağırlıklı merkez) türetMELİ; model kitap embedding'leriyle AYNI olmalı (koordinat kıyaslanabilir).
- **FR-002**: Zevk-vektörü **Personalization** BC'sinde hesaplanıp sahiplenMELİ (kullanıcı verisi, İlke I);
  kitap-kNN **Catalog**'da (product_embedding) yapılMALI.
- **FR-003**: Kişisel öneri, zevk-vektörüne en-yakın kitapları (kNN) döndürMELİ + hard filtre (satılabilir/dil/
  seviye uygun) + kullanıcının zaten gördüğü/aradığı/sahip olduğu kitapları HARİÇ + çeşitlilik (hep aynı dar küme değil).
- **FR-004**: Cross-BC: Catalog öneri yolu zevk-vektörünü Personalization'dan **sanksiyonlu S2S (gRPC)** ile
  almalı; vektör insan-yüzeye/LLM'e render EDİLMEZ (opak sayı dizisi).
- **FR-005**: **Cold-start:** sinyal eşiğin altındaysa (varsayılan <3 arama) kişisel öneri yerine **küratörlü**
  (çok-satan/tür) döndürMELİ; öneri asla boş kalmamalı.
- **FR-006**: Kişisellik YALNIZ sorgu-YOK feed'de devreye girMELİ; kullanıcı spesifik sorgu (yazar/başlık/
  betimleme) yazarsa mevcut lexical/semantik yollar çalışır, taste KARIŞMAZ (YouTube-modeli).
- **FR-007**: Satın-alma/yorum sinyali kablosu hazır olMALI (kitap embedding'i, aramadan yüksek ağırlık) —
  veri gelince taste'e katılır; şu an 0 veri, davranış değişmez.
- **FR-008**: Anonim kullanıcıya kişisel öneri verilMEZ (sinyal yok → küratörlü).

### Key Entities *(include if feature involves data)*

- **Zevk-vektörü (taste)**: kullanıcı başına embedding-uzayı noktası; sinyallerin (arama sorguları; gelecekte
  satın-alma/yorum kitapları) ağırlıklı merkezi. Personalization'da türetilir (on-demand; cache'lenebilir).
- **UserSearchRecord** (mevcut 012): taste'in şu anki tek sinyal kaynağı (sorgu metinleri).
- **product_embedding** (025, Catalog): kNN hedefi; bu feature yalnız okur.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: ≥3 arama yapmış kullanıcının feed'i, aramalarının anlam-merkezine yakın kitaplar döndürür
  (örnek kullanıcıyla: öneriler aranan temalarla ilgili, rastgele değil).
- **SC-002**: Spesifik sorgu (yazar/başlık) feed/taste yoluna GİTMEZ — mevcut arama davranışı %100 korunur (regresyon yok).
- **SC-003**: Cold/anonim kullanıcı feed'i boş DÖNMEZ (küratörlü fallback her zaman sonuç verir).
- **SC-004**: Öneri kullanıcının zaten aradığı/gördüğü kitapları tekrarlamaz + tek dar kümeye sıkışmaz (çeşitlilik).

## Assumptions

- Embedding omurgası (025) + semantik arama (026) canlı; Catalog kNN deseni mevcut.
- Sinyal gerçeği cold-start: ~41 arama (sorgu metni), 0 satın-alma/yorum. MVP yalnız arama-sorgusu sinyaliyle çalışır.
- `UserSearchRecord` sorgu METNİ tutar (tıklanan kitap değil) → taste = sorgu-embedding merkezi; tıklama-loglama AYRI/sonra.
- MVP **tek centroid** (çamurlu-merkez K-Means zevk-kümeleri faz-2 Python, ileride); **decay yok** (düz ortalama).
- Taste **on-demand** hesaplanır (feed-load'da; cache'lenebilir); artımlı güncelleme sonra.
- Feed yüzeyi ScoutAgent "sana özel" ucu/tool; özel **React ekranı AYRI dilim**.
- Kapsam: Personalization taste hesap + gRPC sun · Catalog `recommend_books` · ScoutAgent feed. DIŞINDA:
  tıklama-loglama, K-Means kümeler, decay, React keşif ekranı, satın-alma/yorum ağırlık kalibrasyonu (veri gelince).
