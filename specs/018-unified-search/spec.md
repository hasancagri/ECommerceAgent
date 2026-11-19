# Feature Specification: Unified Catalog Search

**Feature Branch**: `018-unified-search`

**Created**: 2026-11-18

**Status**: Draft

**Input**: User description: Catalog arama birleştirme — tek arama girişi, tek motor. Basit ve kompleks tüm aramalar tek endpoint + tek SearchRequest; naive bellekte-filtre yolu ölür. Clarify sonrası: tek motor = pg_search BM25 (semantik/pgvector ÇIKARILDI); kategori filtresi dahil, yazar-facet ertelendi; saf-filtre gözatmada varsayılan sıra popülerlik.

## Clarifications

### Session 2026-11-18

- Q: Semantik (anlam) araması bu feature'da kurulsun mu? → A: Hayır — çıkarılır; tek motor BM25. Semantik, bilinçli olarak ayrı/sonraki spec.
- Q: Mevcut semantik kod (pgvector embedding tablosu, sorgu-embedding üretimi, RRF füzyon, benzer-kitap) ne olsun? → A: Sökülür (096 deseni — gerektiğinde ayrı spec'le döner).
- Q: Kategori filtresi / yazar-facet dahil mi? → A: Kategori filtresi DAHİL; yazar-listeleme facet'i (agregasyon) ERTELENDİ.
- Q: Metin yok + sıralama yok (saf filtreli gözatma) varsayılan sıra? → A: Popülerlik (satın-alma sayısı azalan).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tek arama girişi: basit ve kompleks aynı yere (Priority: P1)

Müşteri ister "Dostoyevski'nin kitapları" gibi basit bir ifade yazsın, ister "Can Yayınları'nın 100-300 TL arası, karton kapak, az sayfalı, satıştaki Dostoyevski kitapları" gibi çok kısıtlı bir cümle — ikisi de aynı arama yüzeyine gider ve tutarlı, alakalı, sayfalı sonuç döner.

**Why this priority**: Ürünün keşif kalbi. Bugün iki kopuk yol var (naive bellekte-filtre + ayrı hibrit); bu story ikisini tek yola indirir ve tüm sorgu türlerini tek davranışla karşılar. Tek başına çalışır MVP.

**Independent Test**: Basit yazar sorgusu + çok-kısıtlı filtreli sorgu aynı uçtan geçirilir; ikisi de doğru, sıralı, sayfalı sonuç döner ve beklenen kitapları içerir.

**Acceptance Scenarios**:

1. **Given** katalogda Dostoyevski kitapları var, **When** müşteri "Dostoyevski kitapları" arar, **Then** yazarın kitapları alaka sırasıyla sayfalı döner.
2. **Given** çeşitli yayınevi/fiyat/cilt/sayfa/stok/kategori kitaplar var, **When** müşteri "Can Yayınları 100-300 TL karton az-sayfa satışta Dostoyevski" arar, **Then** yalnız tüm kısıtları sağlayan kitaplar döner.
3. **Given** iki sorgu da aynı uca gider, **When** herhangi bir filtre kombinasyonu verilir, **Then** sistem ayrı bir "basit" vs "kompleks" kod yolu kullanmaz (tek yüzey, tek sonuç biçimi).

---

### User Story 2 - Ölçekte doğru sonuç (milyonlarca kayıt) (Priority: P1)

Katalog milyonlarca satıra büyüdüğünde arama hızlı ve doğru kalır; aday kümesini belleğe yükleyip süzen bugünkü yaklaşımın çökmesi engellenir.

**Why this priority**: Mevcut bellekte-filtre yolu demo ölçeğinde çalışır, milyonlarda çöker (bellek + tam-tarama). Ölçek, birleştirmenin ana gerekçesi.

**Independent Test**: Büyük veri kümesinde (ör. ≥1M kayıt) filtreli + sıralı sorgu çalıştırılır; yanıt süresi ve bellek kullanımı sabit kalır (aday kümesi büyümez), yalnız istenen sayfa döner.

**Acceptance Scenarios**:

1. **Given** katalog ≥1M kitap, **When** filtreli + sıralı arama yapılır, **Then** yalnız sayfa (ör. 10-20 sonuç) döner ve yanıt süresi kabul sınırları içinde kalır.
2. **Given** çok eşleşmeli geniş terim, **When** arama yapılır, **Then** sistem tüm eşleşmeleri belleğe yüklemez; sıralama + sayfalama veri katmanında yapılır.

---

### Edge Cases

- Boş/yalnız-boşluk sorgu → hata değil; boş sonuç (ya da filtre-only sonuç) döner.
- Metin yok + sıralama yok (saf filtreli gözatma) → sonuçlar **popülerlik** (satın-alma sayısı azalan) sırasıyla döner.
- Türkçe noktasız ı / büyük-küçük harf ("tarih" vs "tarıh", "İ" vs "ı") → tutarlı eşleşir; bugünkü davranıştan GERİLEME olmaz.
- Filtreyle eşleşen hiçbir kitap yok → boş sonuç, net "sonuç yok" durumu.
- Dolgu/soru kelimeleri ("… listeler misin", "bana … önerir misin") → aramayı kirletmez.
- Çelişen sıralama isteği (hem alaka hem fiyat) → tek mod seçilir, ikisi birden uygulanmaz.
- Bir kitabın stok/indirim kopyası henüz güncellenmemiş (eventual consistency) → arama çalışır; "satışta/indirimli" filtresi kopyanın o anki değerini yansıtır (anlık otorite değil).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, basit (yalnız metin) ve kompleks (metin + çoklu filtre + sıralama) tüm müşteri aramalarını TEK arama yüzeyinden karşılamalıdır.
- **FR-002**: Arama isteği şu bileşenleri opsiyonel taşıyabilmelidir: serbest metin, filtreler (yayınevi dahil/hariç, fiyat aralığı, cilt tipi, sayfa aralığı, kategori, yalnız-satışta, yalnız-indirimli), sıralama, sayfalama.
- **FR-003**: Boş sorgu hata değildir; sistem boş ya da yalnız-filtre sonucu döndürür.
- **FR-004**: Metin verildiğinde sonuçlar alaka (relevance) sırasıyla; açık sıralama (fiyat/yenilik/popülerlik) verildiğinde o sıralamayla dönmelidir. Metin ve açık sıralama YOKSA varsayılan sıra popülerliktir. Aynı sorguda hem alaka hem alan-sıralaması UYGULANMAZ; mod tekildir.
- **FR-005**: Filtreleme ve sıralama veri katmanında (arama indeksi üzerinde) yapılmalı; sistem tüm eşleşen kayıtları belleğe yükleyip süzMEMELİDİR.
- **FR-006**: Sistem yalnız istenen sayfayı (sınırlı sonuç + toplam sayı) döndürmelidir.
- **FR-007**: Sistem kelime (full-text) aramasını alaka-sıralı yapmalı ve yapısal filtre/sıralama/kategoriyi aynı tek motor üzerinden karşılamalıdır (tek arama altyapısı).
- **FR-008**: Arama Türkçe'yi doğru ele almalı (noktasız ı, büyük-küçük harf katlama); birleştirme sonrası arama kalitesi bugünkü davranıştan GERİLEMEMELİDİR.
- **FR-009**: Dolgu/soru kelimeleri sonucu kirletmemeli (doğal cümle sorgularında alakasız eşleşme üretmemeli).
- **FR-010**: ISBN/kimlikle tek kitap getirme (detay nokta-getirme) bu arama yüzeyinin KAPSAMINDA DEĞİLDİR; ayrı tekil-getirme yolu olarak kalır.
- **FR-011**: Bugünkü naive bellekte-filtre arama yolu kaldırılmalı; tüm çağıranlar (müşteri yüzeyi + sohbet agent'ı) birleşik yüzeye yönlendirilmelidir.
- **FR-012**: Doğal-dil niyetini arama isteğine çeviren katman (agent) hangi modun (alaka vs sıralama) uygulanacağını belirlemelidir; birleşik yüzey niyet-çözümleme değil, istek-yürütme yapar.
- **FR-013**: Mevcut semantik/anlam arama altyapısı (gömülü vektör deposu, sorgu-anı vektör üretimi, sıra-füzyon, benzer-kitap) bu feature kapsamında kaldırılmalıdır; kalıntı ölü kod/tablo/config bırakılmaz.

### Out of Scope

- **Semantik / anlam-tabanlı arama** (vektör/embedding): bilinçli olarak ertelendi; gerektiğinde ayrı spec ile döner.
- **Yazar-listeleme facet'i** ("kategorideki yazarları listele" gibi agregasyon): sonraki dilim/spec. (Kategoriye göre KİTAP filtreleme bu feature'da var.)
- **Benzer-kitap** ("buna benzer") önerisi: semantikle birlikte ertelendi.

### Key Entities *(include if feature involves data)*

- **Arama İsteği (SearchRequest)**: Bir müşteri aramasının niyetini taşır — serbest metin (ops.), filtre kümesi (ops.: yayınevi, fiyat, cilt, sayfa, kategori, satışta, indirimli), sıralama (ops.), sayfalama. Basit/kompleks ayrımı yalnız dolu alanlarla ifade edilir.
- **Arama Sonucu Künyesi**: Keşif kartı görünümü — kitap kimliği/ISBN, ad, kapak, liste fiyatı, indirim oranı, etiketler. Tüm arama türleri aynı künyeyi döner.
- **Kitap (arama görünümü)**: Aranabilir alanlar (ad, açıklama, etiket, yazar) + filtrelenebilir/sıralanabilir alanlar (fiyat, sayfa, yayın tarihi, cilt tipi, yayınevi, kategori, stok durumu kopyası, indirim oranı kopyası, popülerlik/satın-alma sayısı).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Basit yazar sorgusu ve çok-kısıtlı filtreli sorgu, tek ve aynı arama yüzeyinden geçer (iki ayrı kod yolu kalmaz) — kod incelemesi + davranış testiyle doğrulanır.
- **SC-002**: ≥1.000.000 kayıtlık katalogda filtreli+sıralı arama, aday kümesini belleğe yüklemeden yalnız sayfayı döndürür; yanıt süresi kayıt sayısı 10×'e çıktığında anlamlı biçimde bozulmaz.
- **SC-003**: Arama sonuçlarının %95'i kabul edilen gecikme içinde döner (kullanıcı sonucu anında görür).
- **SC-004**: Türkçe noktasız-ı içeren aramalar (ör. "tarih") doğru kitapları getirir; birleştirme öncesi çalışan hiçbir arama birleştirme sonrası sonuç kaybetmez (sıfır regresyon).
- **SC-005**: Tüm arama türleri aynı sonuç künyesini döndürür (tutarlı keşif kartı).
- **SC-006**: Semantik altyapı sökümü sonrası sistem derlenir, açılır ve tüm aramalar çalışır; ölü semantik kod/tablo/config kalmaz.

## Assumptions

- Arama yalnız Catalog BC verisi üzerinde çalışır; müşteri-görünür kitap/stok/fiyat/indirim/kategori okumaları Catalog'un lokal (kopya dahil) modelinden karşılanır.
- "Satışta" ve "indirimli" filtreleri, Catalog'daki stok ve indirim KOPYALARINA dayanır (stok ve indirim BC'lerinden event'le beslenir); eventual-consistent — anlık otorite değil, kabul edilen bir bayatlık penceresi var.
- Doğal-dil niyet çözümlemesi sohbet agent'ında yapılır; birleşik arama yüzeyi yapılandırılmış bir istek alır.
- Arama indeksi Catalog'un mevcut belge deposu üzerinde yerinde kurulur (ayrı bir arama deposu/senkron hattı eklenmez).
- **Kesme sırası (bağımlılık):** Türkçe dil işleme (noktasız-ı katlama) ve filtre/sıralama/kategori alanlarının indekse hızlı-alan olarak eklenmesi, naive yolun kaldırılmasından ÖNCE devreye alınmalıdır; aksi halde FR-008 (sıfır regresyon) ihlal olur.
- Bu özellik yeni bir aggregate/tablo/entegrasyon event'i/BC EKLEMEZ; Catalog içinde arama yürütme yolunu + indeksleme yüzeyini değiştirir ve mevcut semantik altyapıyı söker.
- Semantik/vektör altyapının sökülmesi geri-dönüşü ayrı spec gerektirir; bu feature onu kalıcı kaldırır (veri kaybı kabul — embedding verisi offline yeniden üretilebilir).
