# Feature Specification: Katalog Verisi Zenginleştirme (OpenAI, offline)

**Feature Branch**: `008-catalog-enrichment`

**Created**: 2026-11-15

**Status**: Draft

**Input**: Kullanıcının serbest metinle (yazarak/sesle) kitap araması yapabilmesi için, import kaynağı Excel katalogunu OpenAI ile offline zenginleştirmek: her kitaba arama-dostu bir açıklama + serbest etiketler eklemek. Veri zengin olmadan metin araması zayıf kalıyor.

**Artefakt kademesi**: Tam akış (kullanıcı talebi). Ancak bu dilim **.NET solution dışı** bir offline araçtır (Python, repo-dışı veri) — yeni BC/aggregate/tablo/endpoint/integration-event YOK. Anayasa'nın BC/aggregate ilkeleri (I–III, VI) doğrudan devreye girmez; geçerli olan disiplin: **veri LLM'den uydurularak türetilmez** (İlke VIII ruhu — insan-yüzeye yanlış/uydurma değer sızmaz).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Katalogu zenginleştir (Priority: P1)

Operatör, Kitapyurdu Excel katalogunu (23.600 kitap) OpenAI ile işler. Çıktı: orijinalin bozulmadığı **zenginleştirilmiş bir kopya Excel** — her kitabın açıklaması arama-dostu hale gelmiş, yanına serbest etiket kolonu eklenmiş. Import bu kopyayı aynen okuyabilir.

**Why this priority**: Tek teslim edilebilir çekirdek bu. Olmadan arama için zengin içerik yok; sonraki tüm arama işi (FTS/semantik) bu veriye dayanır.

**Independent Test**: Küçük bir örnek (ör. 20 kitap) üstünde çalıştır; çıktı Excel'de her satırda OpenAI açıklaması + etiketler dolu, orijinal 27 sütun + "Kaynak Açıklama" korunmuş mu bak.

**Acceptance Scenarios**:

1. **Given** kaynak Excel'de açıklaması olan bir kitap, **When** zenginleştirme çalışır, **Then** "Açıklama" kolonu OpenAI'nin ürettiği arama-dostu metinle dolar ve orijinal metin "Kaynak Açıklama" kolonunda saklanır.
2. **Given** bir kitap, **When** zenginleştirme çalışır, **Then** "Etiketler" kolonu o kitaba uygun serbest etiketlerle (tema/alt-tür/ruh hali) dolar; tür/kategori etiketi ÜRETİLMEZ.
3. **Given** orijinal Excel, **When** zenginleştirme biter, **Then** orijinal dosya değişmeden kalır, çıktı ayrı bir kopya dosyadır.

---

### User Story 2 - Güvenli yeniden-çalıştırma (Priority: P2)

Operatör işi yarıda kesebilir ya da sonradan tekrar çalıştırabilir; süreç kaldığı yerden devam eder, zaten zenginleştirilmiş kitapları yeniden işlemez (OpenAI maliyeti tekrar doğmaz). Kataloğa sonradan eklenen yeni kitaplar sadece eksik olanları işler.

**Why this priority**: 23.600 kayıt uzun + paralı iş; çökme/kesinti kaçınılmaz. Resume olmadan her deneme baştan maliyet demek.

**Independent Test**: 50 kitaplık koşuyu ortasında durdur, tekrar başlat; ikinci koşunun ilk 25'i atladığını (cache'ten) ve yalnız kalanları işlediğini say.

**Acceptance Scenarios**:

1. **Given** yarım kalmış bir koşu, **When** süreç tekrar başlatılır, **Then** tamamlanmış kitaplar atlanır, yalnız eksikler işlenir.
2. **Given** geçici OpenAI/ağ hatası, **When** bir kitapta hata olur, **Then** o kitap kalıcı cache'e yazılmaz, sonraki koşuda yeniden denenir (kısmi çöp kalmaz).

---

### User Story 3 - Uydurma yok (grounding + kaçış) (Priority: P2)

Zenginleştirme hiçbir kitapta bilgi uydurmaz. OpenAI kitabı gerçekten biliyorsa kendi bilgisiyle zengin açıklama yazar; bilmiyorsa yalnız kaynak açıklamayı temel alır; ikisi de yoksa uydurmak yerine eldeki az bilgiyle yetinir.

**Why this priority**: Katalogun uzun kuyruğu (bilinmeyen başlıklar) + ileride elle girilecek yeni kitaplar. Uydurulan açıklama/etiket aramayı kirletir ve müşteriye yanlış bilgi gösterir — eksik veriden beterdir.

**Independent Test**: Kaynak açıklaması zengin bir ünlü kitap + kaynak açıklaması olan bilinmeyen bir kitap + kaynak açıklaması boş bir kitap seç; çıktıların sırasıyla (kendi-bilgi), (kaynağa-sadık), (uydurmamış) olduğunu elle doğrula.

**Acceptance Scenarios**:

1. **Given** OpenAI'nin bilmediği bir kitap + dolu kaynak açıklama, **When** zenginleştirilir, **Then** çıktı yalnız kaynak açıklamadaki bilgiyi yeniden ifade eder, yeni olay/karakter/tema EKLEMEZ.
2. **Given** kaynak açıklaması boş + OpenAI'nin bilmediği bir kitap, **When** zenginleştirilir, **Then** uydurulmuş açıklama üretilmez (boş/az kalır, işaretlenir).

---

### Edge Cases

- **Kaynak açıklama boş (~%3,4, ~800 kitap):** metin yok. OpenAI biliyorsa yazar; bilmiyorsa ad/yazar/kategori dışında bilgi olmadığından açıklama boş/az kalır ve "zenginleştirilemedi" olarak işaretlenir (uydurmaz).
- **OpenAI bozuk/şema-dışı JSON döndürür:** kayıt başarısız sayılır, kalıcı cache'e girmez, yeniden denenir; tekrarlı başarısızlık rapora düşer.
- **Aynı ISBN birden fazla sayfada:** ISBN anahtarıyla tek kez işlenir.
- **Geçici hata / hız sınırı (429/5xx):** retry; kalıcı tükeniş kayıt bazında rapora.
- **İçerik politikası reddi:** kayıt işaretlenir, atlanır, rapora düşer.
- **Kısmi koşu / maliyet sınırı:** limit'li çalıştırma desteklenir; yarım çıktı tutarlı (işlenen satırlar dolu, kalanlar orijinal).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Süreç, kaynak Excel'deki her kitap için OpenAI ile arama-dostu bir açıklama üretir ve çıktıdaki "Açıklama" kolonuna yazar.
- **FR-002**: Açıklama üretimi **grounding + kaçış** kuralını uygular: kitap gerçekten biliniyorsa model bilgisiyle zengin; bilinmiyorsa yalnız kaynak açıklamayı temel alır; **hiçbir durumda bilgi uydurmaz**.
- **FR-003**: Orijinal Kitapyurdu açıklaması çıktıda ayrı bir "Kaynak Açıklama" kolonunda korunur (iz/denetim/yeniden-çalıştırma).
- **FR-004**: Süreç her kitap için **serbest (açık vokabüler) etiketler** üretir (tema/alt-tür/ruh hali) ve yeni bir "Etiketler" kolonuna yazar.
- **FR-005**: Etiketler **tür/kategori içermez** — kategori Kitapyurdu "Kategori" sütununun otoriter alanıdır, süreç ona dokunmaz, yeniden üretmez.
- **FR-006**: Süreç **benzer-yazar/benzer-kitap** alanı üretmez (bu çalışma-anı semantik işidir, offline veri değil).
- **FR-007**: Çıktı dil olarak **Türkçe**dir.
- **FR-008**: Orijinal Excel değişmez; süreç ayrı bir **kopya** dosya üretir; çıktı orijinalin 27 sütununun tamamını korur, üstüne yeni kolon(lar) ekler.
- **FR-009**: Süreç **resumable**dır: tamamlanan kitaplar kalıcı önbelleğe (ISBN anahtarlı) yazılır, periyodik checkpoint alınır, çıktı atomik yazılır (yarım dosya bırakmaz).
- **FR-010**: Yeniden çalıştırma **artımlıdır**: zaten zenginleştirilmiş kitaplar atlanır, yalnız eksik/yeni kitaplar işlenir.
- **FR-011**: Geçici hatalar yeniden denenir; başarısız kayıt kalıcı önbelleğe yazılmaz (sonraki koşuda yeniden denenir).
- **FR-012**: Süreç **limit/örnek modu** sunar (az kitapla test) ve bir **kapsam raporu** (kaç kitap zenginleşti, kaçı etiketlendi, kaçı zenginleştirilemedi) verir.
- **FR-013**: Çıktı Excel, mevcut import tarafından **değişiklik gerektirmeden** okunabilir kalır (var olan sütun adları/yapısı korunur; yeni kolonları import bu dilimde yok sayar).

### Key Entities

- **Kitap kaydı (Excel satırı)**: ISBN (tekil anahtar), ad, yazar, kategori (otoriter, dokunulmaz), kaynak açıklama, zenginleştirilmiş açıklama, etiketler.
- **Zenginleştirme önbelleği**: ISBN → {zenginleştirilmiş açıklama, etiketler, durum (başarılı/zenginleştirilemedi/hata)}. Resume + artımlı koşunun kaynağı.
- **Zenginleştirilmiş katalog (çıktı)**: orijinalin kopyası + "Kaynak Açıklama" + "Etiketler" kolonları; "Açıklama" OpenAI metniyle değişmiş.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kaynak açıklaması olan kitapların **≥%99'u** çıktıda dolu bir arama-dostu açıklamaya sahiptir.
- **SC-002**: Kaynak açıklaması olan kitapların **≥%95'i** en az 5 etiket taşır.
- **SC-003**: Bilinmeyen + kaynak-açıklamalı kitaplardan alınan 30'luk örnekte, çıktı açıklaması kaynakta olmayan **hiçbir olay/karakter/tema uydurmaz** (elle doğrulama, 0 uydurma).
- **SC-004**: Kesintiden sonra yeniden başlatma, tamamlanmış kitapların **%0'ını** yeniden işler (OpenAI maliyeti tekrarlanmaz).
- **SC-005**: Orijinal Excel bit-aynı kalır; çıktının 27 orijinal sütunu eksiksizdir; mevcut import çıktıyı hatasız okur.
- **SC-006**: Tüm katalog zenginleştirmesi belirlenen maliyet bütçesi (~$2–3) içinde tamamlanır.

## Assumptions

- Kaynak = `~/Desktop/ECommerce Notları/Kitapyurdu_Katalog_v2.xlsx` (23.600 kitap, 5 yayınevi sayfası, 27 sütun). Doluluk ölçüldü: Açıklama %96,6 (medyan ~805 karakter).
- Zenginleştirilen açıklama mevcut "Açıklama" kolonunun **üstüne** yazılır (gösterim + arama tek metin); böylece import, mapper'ı değişmeden zengin metni okur.
- Etiketler açık/serbest vokabüler; bu dilimde kapalı taksonomi kurulmaz (ileride elle giriş + filtre dilimi kendi kararını verir).
- İki giriş yolu modeli: **Excel toplu = OpenAI**; **ekrandan elle giriş = insan** (bu dilimde ekran yolu KURULMAZ, yalnız sınır notu). Cold-start böyle çözülür: OpenAI yalnız kaynak metni olan kitaplarda çalışır.
- Model: OpenAI gpt-4o-mini, yapısal (JSON) çıktı; API anahtarı ortam/secret'tan.
- Araç `book-crawl/` Python dizininde yaşar; mevcut `fetch_descriptions.py` deseni (resumable cache, checkpoint, atomik yazım, limit/report flag) tekrar kullanılır.

## Dependencies

- OpenAI API erişimi (anahtar).
- Kaynak Excel dosyası (repo-dışı, kullanıcı makinesinde).
- Excel okuma/yazma yeteneği (mevcut Python ortamı + openpyxl kurulu doğrulandı).

## Out of Scope *(bu dilim)*

- Catalog import mapper değişikliği (etiket kolonunu Product'a bağlama).
- Postgres FTS / pg_trgm / pgvector / semantik katman.
- LLM query-understanding (çalışma-anı niyet çözme) ve arama endpoint'i.
- Ekrandan elle kitap girişi (yeni kitap) yüzeyi.
- Kapalı etiket taksonomisi + tür bazlı kesin filtre tasarımı.
