# Feature Specification: İçerik-Temelli Keşif Omurgası (v11 Katalog)

**Feature Branch**: `feat/021-content-discovery-v11`

**Created**: 2026-11-23

**Status**: Draft

**Input**: Kullanıcı-etkileşim temelli benzerlik terk edildi; kitaplar yazma anında LLM
pipeline'ıyla kapalı taksonomiye yerleştirildi (Kitapyurdu_Katalog_v11.xlsx — tur/ton/seviye/konu/
guven, 22.286 satır, 5 yayınevi sayfası, 30 kolon). DB sıfır; temiz import. Kategori + Etiket
eksenleri ölür, yerine taksonomi; WorkKey eser kimliği; stok ikili sinyal; facet arama; similar_books.

**Kademe**: TAM — aggregate alan değişimi + integration event kontrat değişimi + yeni MCP tool +
iki BC'ye dokunan söküm.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - v11 kataloğu temiz yükle (Priority: P1)

Admin v11 Excel'ini yükler; dry-run raporunu görüp onaylar; 22.286 kitap taksonomi alanları
(tür/ton/seviye/konu/güven), eser kimliği (WorkKey) ve stok sinyaliyle doğar. "SATIŞ YOK" satırları
stok 0 ile (katalogda görünür, satılamaz), satıştakiler rastgele 20-50 stokla açılır.

**Why this priority**: Veri omurgası — arama ve benzerlik bu alanlar olmadan çalışamaz; DB sıfır,
sistemin kataloğu yok.

**Independent Test**: Import dry-run → confirm; ürün sayısı, Enriched sayacı, örnek kitapta
taksonomi alanları + WorkKey, tükenmiş kitapta stok 0 doğrulanır.

**Acceptance Scenarios**:

1. **Given** boş katalog + v11 dosyası, **When** admin yükler ve onaylar, **Then** tüm geçerli
   satırlar taksonomi alanları + WorkKey ile doğar; rapor Enriched sayısını gösterir.
2. **Given** "SATIŞ YOK (TÜKENMİŞ)" satırı, **When** import tamamlanır, **Then** ürün katalogda
   görünür, stok adedi 0, satışa uygun değil.
3. **Given** taksonomi kolonunda sözlük-dışı değer, **When** satır işlenir, **Then** değer
   "belirsiz"e düşer, satır hatasız yazılır, iş durmaz.
4. **Given** aynı eserin iki baskısı (aynı yazar + başlık, farklı ISBN), **When** import biter,
   **Then** ikisi aynı WorkKey'i taşır.

---

### User Story 2 - Facet'li arama (Priority: P2)

Kullanıcı (ekran veya Kâşif sohbeti) "mizahi kısa öyküler", "çocuk kitabı olmayan karanlık
romanlar" gibi istekleri tür/ton/seviye/konu filtreleriyle deterministik cevaplar; serbest metin
araması taksonomi değerlerini de tarar.

**Why this priority**: Taksonominin okuma-anı karşılığı; enrichment'in kullanıcıya değdiği ilk yüzey.

**Independent Test**: REST `/products/search` ve MCP `search_books` ile tur/ton/konu filtreleri +
serbest metin kombinasyonları; kategori filtresi artık yok.

**Acceptance Scenarios**:

1. **Given** yüklü katalog, **When** tur=roman + ton=karanlık filtrelenir, **Then** yalnız o
   değerleri taşıyan kitaplar döner (metin içi tesadüfi eşleşme değil, alan eşleşmesi).
2. **Given** çoklu konu seçimi (ör. aşk/romantizm + aile), **When** arama koşar, **Then** herhangi
   birini taşıyan kitaplar döner (OR).
3. **Given** sözlük-dışı filtre değeri, **When** istek gelir, **Then** istek anlaşılır hatayla reddedilir.
4. **Given** serbest metin "mizahi", **When** arama koşar, **Then** ton değeri mizahi/absürt olan
   kitaplar da eşleşir (açıklamasında kelime geçmese bile).
5. **Given** stok filtresi istenir, **When** "stokta olanlar" seçilir, **Then** yalnız stok>0 döner.

---

### User Story 3 - Benzer kitap önerisi (Priority: P3)

Kâşif sohbetinde "şuna benzer kitap" isteği: hedef kitabın konu+ton örtüşmesinden benzerler
bulunur; aynı eserin diğer baskıları listeye giremez, her eser bir kez görünür, stokta olanlar önce.

**Why this priority**: Keşif deneyiminin yeni yeteneği; arama ve import üstüne kurulur.

**Independent Test**: MCP `similar_books(productId)` çağrısı; sonuçta hedefin baskıları yok,
WorkKey tekrarı yok, dil/seviye uyumlu.

**Acceptance Scenarios**:

1. **Given** 6 baskılı bir eser hedeflenir, **When** similar_books çağrılır, **Then** sonuçta o
   eserin hiçbir baskısı yoktur ve hiçbir eser iki kez görünmez.
2. **Given** çocuk kitabı hedeflenir, **When** benzerler istenir, **Then** yetişkin kitapları dönmez
   (ve tersi).
3. **Given** hedefin konu/ton'u boş ya da "belirsiz", **When** çağrı gelir, **Then** aynı yazar +
   aynı tür fallback'iyle sonuç döner (boş liste yerine).
4. **Given** benzerlerin bir kısmı tükenmiş, **When** liste dönerse, **Then** stokta olanlar önce gelir.

---

### Edge Cases

- Ton kolonu boş + güven düşük satır: alanlar boş/"belirsiz" kalır, kitap aranabilir kalır (US2
  metin yolu), benzerlikte fallback'e düşer.
- Çok yazarlı kitapta yazar sırası farklı iki baskı: WorkKey yine aynı (yazarlar sıralanır).
- Başlıktaki parantez eki ("(Ciltli)", "(Özel Baskı)"): WorkKey'e girmez.
- Guven değeri virgüllü ("0,3") gelirse parse toleranslı.
- similar_books var olmayan productId: anlaşılır hata (boş sonuç değil).
- Dry-run sonrası onay: sayaçlar gerçek koşudan yeniden üretilir (mevcut davranış korunur).

## Requirements *(mandatory)*

### Functional Requirements

**Söküm (Foundational)**

- **FR-001**: Kategori kavramı katalogdan tamamen kalkar: Category modeli, üründeki kategori
  referansları, kategori araması/filtresi, ürün-değişti duyurusundaki kategori bilgisi.
- **FR-002**: Etiket (BookTag) kavramı kalkar; serbest etiket yerine kapalı taksonomi gelir.
- **FR-003**: Ürün-değişti duyurusu (ProductChangedEvent) kategori alanları olmadan yayımlanmaya
  devam eder (tüketicisi yok; kontrat sadeleşir).

**Taksonomi + WorkKey (Foundational)**

- **FR-004**: Kapalı taksonomi sözlüğü tek yetkili kaynakta tanımlanır: tür (tek), ton (0-3),
  seviye (tek), konu (0-4), güven (0-1). Değerler tasarım belgesindeki (PDF bölüm 19) listelerdir;
  "belirsiz" geçerli değerdir.
- **FR-005**: Ürün taksonomi alanlarını taşır; sözlük-dışı değer ataması "belirsiz"e düşer
  (iş akışı durmaz). Kardinalite aşımı kırpılır (sıralı ilk N).
- **FR-006**: Ürün WorkKey (eser kimliği) taşır: sıralanmış yazar adları + parantez-eki atılmış
  başlıktan normalize üretilir (Türkçe karakter düzleştirme, küçük harf). Kullanıcı yüzeyine çıkmaz.
- **FR-007**: Seviye değerleri çocuk/yetişkin uyum gruplarına ayrılır (benzerlik hard filtresi için).

**Import (US1)**

- **FR-008**: Import v11 düzenini okur: yeni kolonlar tur, ton, seviye, konu (`;`-ayraçlı), guven,
  Stok Durumu; kalkan kolonlar Kategori, İlgili Kategoriler, Etiketler, Ortalama Okuma Süresi.
  Diğer kolon davranışları (fiyat, indirim, sayaçlar, kapak, çevirmen...) değişmez.
- **FR-009**: İki-faz akış (dry-run → onay → yazım) ve INSERT-ONLY davranış korunur.
- **FR-010**: Rapor "Tagged" yerine "Enriched" sayar: taksonomisi atanmış (tur ≠ "belirsiz" veya
  en az bir konu) satır sayısı.
- **FR-011**: "Stok Durumu" değeri "SATIŞ YOK" ile başlıyorsa ürün satış-dışı sinyaliyle duyurulur
  (ProductAdded.InStock=false); aksi halde satışta (true, default — eski kontratla uyumlu additive alan).
- **FR-012**: Stok servisi sinyale göre kayıt açar: satış-dışı → adet 0; satışta → rastgele 20-50.
  Adet değişimi duyurusu (StockChanged) ve katalogdaki adet kopyası kablosu aynen çalışır.
- **FR-013**: `Product.InStock` hesaplanan özelliktir (stok kopyası > 0); ayrı saklanmaz, çelişemez.

**Arama (US2)**

- **FR-014**: Arama tür/ton/seviye/konu çoklu-değer filtreleri alır; aynı alan içinde OR, alanlar
  arası AND; değerler alan-eşleşmesidir (metin-içi tesadüf değil). Sözlük-dışı değer reddedilir.
- **FR-015**: Serbest metin araması taksonomi değerlerini de tarar (etiketlerin yerine).
- **FR-016**: Kategori filtresi arama yüzeylerinden (REST + MCP) kalkar; "stokta olanlar" filtresi eklenir.
- **FR-017**: REST `/products/search` ve MCP `search_books` aynı arama yolunu kullanmaya devam eder;
  MCP tool tanımı sözlük değerlerini agent'a bildirir (doğru değer üretimi için).

**Benzerlik (US3)**

- **FR-018**: Yeni query-only MCP tool `similar_books(productId, count)`: hedefin konu+ton
  örtüşmesiyle benzer bulur (nadir ortak değer daha güçlü sinyal — IDF ağırlıklı).
- **FR-019**: Objektif yakınlık katkısı: aynı yazar > aynı tür > aynı çevirmen (katsayılar
  yapılandırılabilir).
- **FR-020**: Hard filtreler: aynı dil; seviye uyum grubu (çocuk↔çocuk, yetişkin↔yetişkin);
  hedefle aynı WorkKey dışlanır.
- **FR-021**: Sonuçlar eser bazında tekildir (WorkKey başına en iyi baskı); sıralama: stokta
  olan önce → benzerlik skoru → popülerlik.
- **FR-022**: Hedefin konu+ton'u boş/"belirsiz" ise fallback: aynı yazar + aynı tür; o da boşsa
  anlaşılır "benzer bulunamadı" cevabı.
- **FR-023**: Kâşif (ScoutAgent) katalog alt-agent'ı similar_books'u tanır; "şuna benzer" niyetinde
  tool'u çağırır, sonuçlar kart olarak döner (mevcut render sözleşmesi).
- **FR-024**: "Kitap ne anlatıyor / özetle" sorusu CEVAPLANMAZ (kullanıcı kararı 2026-11-23):
  özet/açıklama tool'u bilinçli açılmaz; Kâşif kartı gösterir, detay sayfasına yönlendirir,
  hafızadan özet uydurmaz.

### Key Entities

- **Product**: kitap künyesi + YENİ: Tur, Tons, Seviye, Konular, EnrichmentConfidence, WorkKey;
  ÖLEN: CategoryId, RelatedCategoryIds, Tags. InStock hesaplanan (StockCount>0). IsSalable DEĞİŞMEZ.
- **BookTaxonomy** (sözlük): kapalı değer listeleri + seviye uyum grupları; tek yetkili kaynak.
- **ImportJob**: Tagged → Enriched sayacı; yaşam döngüsü değişmez.
- **ProductAdded** (kontrat): + InStock (additive, default true).
- **StockItem**: davranış değişmez; seed adedi sinyale bağlanır (0 veya 20-50).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: v11 import'u tek onay akışında tamamlanır; satırların ≥%95'i taksonomi alanlı
  (Enriched) doğar; hiçbir satır taksonomi hatası yüzünden iş durdurmaz.
- **SC-002**: "SATIŞ YOK" satırlarının tamamı stok 0 ile doğar; satıştakilerin tamamı stok>0.
- **SC-003**: Tür+ton facet araması yalnız alan-eşleşen kitapları döner (örneklem denetiminde
  yanlış-pozitif 0); kategori parametresi hiçbir yüzeyde kalmaz.
- **SC-004**: similar_books sonuçlarında hedef eserin baskısı 0; eser tekrarı 0; dil/seviye
  ihlali 0 (örneklem denetimi).
- **SC-005**: Tüm mevcut testler + yeni testler yeşil; build temiz.

## Assumptions

- v11 dosyası nihai; kolon başlıkları keşfedilen A-AD düzeniyle birebir.
- similar_books varsayılan sonuç sayısı 5 (PDF "5'erli liste" deseni); üst sınır 20.
- Boost katsayıları (yazar/çevirmen/tür) başlangıç değerleriyle gelir; kalibrasyon canlı denemede.
- "Stokta olanlar" filtresi satılabilirlikten ayrıdır (IsSalable fiyat+yazar da ister).
- Kategori sökümü React tarafını etkilemez (React henüz katalog ekranı bağlamadı); Kâşif prompt'ları
  güncellenir.
- V2 embedding benzerliği ayrı spec (yapma-listesi çizgisi: pgvector bilinçli yeni spec'le döner).