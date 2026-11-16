# Feature Specification: Katalog Etiket Import (BookTag)

**Feature Branch**: `009-catalog-tag-import`

**Created**: 2026-11-16

**Status**: Draft

**Input**: User description: 008'de üretilen `Kitapyurdu_Katalog_v3_enriched.xlsx`'in `Etiketler` sütununu Catalog'a aktar; `Product`'a kitaba-ait etiket listesi bağla, normalize-eşleşmeye hazır hale getir.

> **Kademe (conventions):** Tam akış — yeni VO (`BookTag`), `Product`'a yeni alan, mevcut import sürecinin genişletilmesi. Domain-TDD (İlke VI) `BookTag` + `Product` etiket davranışına uygulanır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Zenginleştirilmiş katalog etiketleriyle içeri alınır (Priority: P1) 🎯 MVP

Admin, 008'in zenginleştirdiği Excel'i (`_v3_enriched.xlsx`) mevcut import yolundan yükler. Dry-run raporu kaç satırda etiket olduğunu gösterir; admin onaylar; ürünler doğarken her biri **kendi etiket listesini** taşır (zengin açıklama zaten `Açıklama` sütunundan geliyor). Böylece katalog, tema/ruh-hali/alt-tür etiketleriyle keşfe hazır temel kazanır.

**Why this priority**: Etiketlerin Product'a oturması, sonraki keşif/arama diliminin ön koşulu. Bu olmadan 008'in ürettiği etiketler Excel'de atıl kalır.

**Independent Test**: Boş katalog + `_v3_enriched.xlsx` → dry-run (etiket sayacı) → onay → ürünleri oku: etiketli satırların ürünlerinde etiket listesi dolu, `Etiketler` boş satırlarda ürün etiketsiz ama hatasız doğmuş.

**Acceptance Scenarios**:

1. **Given** `Etiketler` hücresi `distopya, gözetim, karanlık gelecek` olan bir satır, **When** import onaylanır, **Then** o ürün tam 3 etiket taşır, her biri normalize edilmiş.
2. **Given** `Etiketler` hücresi boş bir satır, **When** import onaylanır, **Then** ürün etiketsiz doğar (hata yok, iş durmaz).
3. **Given** dry-run koşulur, **When** rapor görülür, **Then** "etiketli satır" sayacı rapora yansır; hiçbir şey yazılmaz.

---

### User Story 2 - Aynı kavram tek etiket gibi gruplanır (Priority: P2)

Farklı satırlarda `Distopya`, `distopya`, `DİSTOPYA` yazılmış olabilir. Sistem bunları aynı normalize-edilmiş etikete indirger; böylece ileride "distopya içeren kitaplar" sorgusu üçünü de tek kümede toplar. Depolama kitaba özel kalır (ortak etiket kaydı yok), ama eşleşme değere göre çalışır.

**Why this priority**: Gruplama olmadan etiketler serbest-metin gürültüsü olur; keşfin değeri normalize-eşleşmeden gelir. P1'den sonra çünkü önce etiketin Product'a oturması gerekir.

**Independent Test**: Üç satıra `Distopya`/`distopya`/`DİSTOPYA` koy → import → üç ürünün etiketinin normalize değeri birebir aynı; Türkçe İ/ı farkı eşleşmeyi bozmuyor.

**Acceptance Scenarios**:

1. **Given** `Distopya` ve `DISTOPYA` iki ayrı satırda, **When** import edilir, **Then** iki ürünün ilgili etiketi aynı normalize değere (`distopya`) sahip (büyük/küçük harf farkı yutulur).
2. **Given** `karanlık` etiketli bir satır, **When** import edilir, **Then** ürün hem `karanlık` hem ASCII ikizi `karanlik` taşır (her iki yazımla aranabilir).
3. **Given** bir satırda `gözetim, gözetim` (tekrar), **When** import edilir, **Then** ürün bu etiketi bir kez taşır (yinelenen ayıklanır; ASCII ikizi `gozetim` ayrı).

---

### Edge Cases

- `Etiketler` hücresinde baştan/sondan boşluk, çift virgül, boş parça (`a,, b,`) → boş/whitespace parçalar atılır; geçerli olanlar alınır.
- Çok uzun/anlamsız tek etiket → serbest metin olduğu için kabul edilir (kapalı küme yok); yalnız boş olan reddedilir.
- `Etiketler` sütunu dosyada hiç yoksa → import eski davranışıyla çalışır, hiçbir ürün etiket almaz (geriye-uyumlu).
- Zaten var olan ISBN (INSERT-ONLY) → satır atlanır; etiket de yazılmaz (var olan ürün etiketi EZİLMEZ — mevcut invariant korunur).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Import, Excel satırındaki `Etiketler` sütununu okumalı ve virgülle ayrılmış parçalara bölmeli.
- **FR-002**: Her etiket parçası normalize edilmeli: baştan/sondan boşluk temizlenir, küçük-harfe (`lowercase`) indirgenir; sonucu boş olan parça atılır. Türkçe karakterli (ı/ş/ğ/ü/ö/ç) etiketin ASCII-katlanmış ikizi ("karanlık"→"karanlik") AYRI etiket olarak eklenmeli (her iki yazımla aranabilsin — arama köprüsü).
- **FR-003**: Bir üründe aynı normalize değere sahip etiketler tekilleştirilmeli (yinelenen bir kez tutulur).
- **FR-004**: Her ürün kendi etiket listesini taşımalı; etiketler kitaplar-arası ortak bir kayda/varlığa bağlanmamalı (bağımsız depolama).
- **FR-005**: İki ürünün etiketleri, normalize değerleri eşitse, "aynı etiket" olarak karşılaştırılabilmeli — bu, ileride kitaplar-arası gruplamayı (örn. "distopya içeren tüm kitaplar") mümkün kılar.
- **FR-006**: Dry-run raporu, etiketli satır sayısını içermeli; dry-run hiçbir ürün/etiket yazmamalı.
- **FR-007**: Onaylı yazımda etiketler ürün oluşturulurken birlikte yazılmalı; INSERT-ONLY korunur — var olan ISBN atlanır, etiketi değişmez.
- **FR-008**: `Etiketler` boş veya sütun yok olan satırlarda ürün etiketsiz, hatasız doğmalı; etiket eksikliği importu durdurmamalı.
- **FR-009**: `Kaynak Açıklama` sütunu bu dilimde yok sayılmalı (okunmaz, Product'a yazılmaz).

### Key Entities *(include if feature involves data)*

- **BookTag (value object)**: Tek bir etiketi temsil eder; kimliği/yaşam döngüsü yoktur. Taşıdığı tek şey normalize-edilmiş etiket metni. İki BookTag değerleri eşitse eşittir (gruplamanın temeli).
- **Product (mevcut aggregate, genişler)**: Kendi `BookTag` listesini tutar (yalnız okunur dışarıya; değişim ürün davranışından). Etiketler ürünün bir parçası — ayrı aggregate değil.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `_v3_enriched.xlsx` içe alındığında, kaynak etiketli satırların en az %98'inin ürünü ≥1 etiket taşır (008 çıktısıyla uyumlu kapsam).
- **SC-002**: Aynı kavramın farklı yazımları (büyük/küçük harf, Türkçe İ/ı) import sonrası tek normalize değere iner — örnek kümede %100 eşleşme.
- **SC-003**: Bir normalize etiket değeri verildiğinde, onu taşıyan tüm ürünler tek sorguyla toplanabilir (gruplama fiilen çalışır — doğrulama örnek etiketle).
- **SC-004**: `Etiketler` sütunu olmayan eski formatlı Excel aynı import yolundan hatasız geçer (geriye-uyumluluk).

## Assumptions

- **Taze import varsayımı**: Katalog şu an boş (098 seed söküldü; master'da ürün doğuran yol yok). Etiketler INSERT yolundan gelir; var olan ürünleri güncelleme (backfill) yolu bu dilimde YOK — INSERT-ONLY invariant'ı korunur. Var olan ürünlere toplu etiket geçişi gerekirse ayrı dilim.
- **Kaynak dosya**: 008'in ürettiği `_v3_enriched.xlsx` (29 sütun; `Etiketler` virgül+boşluk ayrık). Zengin `Açıklama` zaten mevcut `Açıklama` sütunundan okunur — bu dilimin deltası yalnız etiketler.
- **Normalizasyon kaynağı**: Mevcut `NameNormalization` (İ/ı fold) yeniden kullanılır — yeni normalize mantığı icat edilmez.
- **Etiket serbesttir**: Kapalı küme/taksonomi yok; model ne ürettiyse (008) o alınır. Kategori'den ayrı ve ona dokunmaz (Kategori otoriter).
- **Arama yüzeyi ayrı**: Bu dilim etiketi Product'a oturtur + normalize-eşleşmeye hazırlar; etiketi fiilen kullanan keşif/arama (pg-hibrit) SONRAKİ dilim.
