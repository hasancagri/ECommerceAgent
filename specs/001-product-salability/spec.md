# Feature Specification: Satışa-Uygunluk Modeli + Kitap Künyesi Genişlemesi

**Feature Branch**: `001-product-salability`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "Catalog domain yerleşimi: satışa-çıkma modeli + kitap künyesi genişlemesi (yalnız domain; dış yüzey yok)."

**Kademe**: Küçük — tek aggregate (Product) içinde kalır; yeni tablo/kontrat/event yok, dış yüzey yok. Yalnız `spec.md` + `tasks.md`.

## Bağlam

Bugünkü domain'de kitap ancak fiyatla doğabiliyor (fiyat>0 doğum invariant'ı, 097: fiyat sonradan
değişmez) ve en az bir yazar zorunlu. Oysa katalogda fiyatı belirlenmemiş ya da yazar bilgisi
olmayan kitaplar da yaşayabilmeli: **katalogda görünür olmak ile satın alınabilir olmak ayrı
kavramlardır.** Bu dilim o ayrımı domain'e koyar: satışa-uygunluk, tek tanımlı türetilmiş bir durum
olur; ileride ürün detay sayfası, sepet ve diğer yüzeyler bu tek tanımı okur.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Eksik künyeli kitap katalogda yaşayabilir (Priority: P1)

Kitap fiyatsız ve/veya yazarsız doğabilir; katalogda var olur ama satışa uygun sayılmaz. Eksik
tamamlanınca (fiyat atandı, yazar bağlandı) kitap kendiliğinden satışa uygun hale gelir — ayrı bir
"yayınla" düğmesi yoktur.

**Why this priority**: Ayrımın özü bu; diğer her şey bunun üstüne kurulur.

**Independent Test**: Saf domain testleri — fiyatsız/yazarsız doğum + kriter tamamlanınca
satışa-uygunluğun açılması.

**Acceptance Scenarios**:

1. **Given** ad+ISBN'li ama fiyatsız kitap, **When** oluşturulur, **Then** kitap doğar; satışa-uygunluk KAPALI.
2. **Given** yazarsız kitap, **When** yazar ataması boş listeyle yapılır, **Then** kabul edilir; satışa-uygunluk KAPALI.
3. **Given** fiyatsız doğan ve ≥1 yazarı olan kitap, **When** geçerli (>0) fiyat atanır, **Then** satışa-uygunluk AÇIK olur.
4. **Given** fiyatlı kitap, **When** 0 ya da negatif fiyat atanmak istenir, **Then** reddedilir (mevcut fiyat değişmez).
5. **Given** fiyatlı kitap, **When** fiyat yeni bir >0 değere değiştirilir, **Then** kabul edilir (fiyat serbest değişir — 097 revizesi).

---

### User Story 2 - Satışa-uygunluk tek tanımdan okunur (Priority: P2)

Satışa-uygunluk kuralı (fiyat>0 VE stok>0 VE ≥1 yazar) domain'de TEK yerde tanımlıdır. Stok
kriteri, stok kavramı sisteme dönene dek değerlendirme dışıdır (boş geçer) — tanımda bugünden yaşar.

**Why this priority**: Kuralın kopyalanıp tutarsızlaşmasını baştan engeller; gelecekteki tüm
yüzeyler (detay sayfası, sepet) bu tanıma bağlanacak.

**Independent Test**: Kriter kombinasyonlarının (fiyat var/yok × yazar var/yok) doğruluk tablosu
domain testiyle doğrulanır.

**Acceptance Scenarios**:

1. **Given** fiyat>0 ve ≥1 yazar, **When** satışa-uygunluk sorgulanır, **Then** AÇIK (stok ayağı boş geçer).
2. **Given** fiyat yok YA DA yazar yok, **When** sorgulanır, **Then** KAPALI.

---

### User Story 3 - Künye genişler: cilt tipi + popülerlik sayaçları (Priority: P3)

Kitap künyesine cilt tipi (Karton Kapak/Ciltli/…) ve popülerlik sayaçları (puan, puan adedi,
satın alma adedi, okuyacağım/okuyorum/okudum, favori) eklenir. Sayaçlar dış kaynaktan taşınan
anlık görüntüdür (bizim canlı verimiz değil); keşif/sıralama sinyalidir, satışa-uygunluk kararına girmez.

**Why this priority**: Alanların domain'de evi olmalı; davranış kuralı yok, düşük risk.

**Independent Test**: Alanların atanıp okunabildiği, geçersiz değerlerin reddedildiği domain testleri.

**Acceptance Scenarios**:

1. **Given** kitap, **When** cilt tipi ve sayaçlar atanır, **Then** künyede okunur.
2. **Given** kitap, **When** negatif sayaç ya da 0-5 dışı puan atanmak istenir, **Then** reddedilir.

---

### Edge Cases

- Fiyatı sonradan silme/sıfırlama yolu YOK — fiyat bir kez atandıktan sonra yalnız >0 değere değişir ("fiyatsızlaşma" doğmaz).
- Yazarlı kitabın yazar listesi boş listeyle değiştirilebilir — tam-değiştirme semantiği korunur; sonuç satışa-uygunluğun kapanmasıdır (tutarlı).
- Sayaçlar 0 kabul eder (negatif reddedilir); hiç puanlanmamış kitap geçerlidir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Kitap fiyatsız oluşturulabilmeli; fiyat verilirse >0 olmalı (0/negatifle doğum reddedilir).
- **FR-002**: Fiyat sonradan atanabilmeli ve serbest değiştirilebilmeli; atanan her değer >0 (097 "doğumda sabit" kararı bu spec'le revize edilir).
- **FR-003**: Kitap yazarsız oluşturulabilmeli; yazar ataması boş listeyi kabul etmeli (tam-değiştirme semantiği sürer).
- **FR-004**: Satışa-uygunluk türetilmiş durum olmalı ve TEK tanımdan okunmalı: fiyat>0 VE stok>0 VE ≥1 yazar. Hiçbir yerde kopyalanmaz.
- **FR-005**: Stok kriteri tanımda yer almalı ama stok kavramı sisteme dönene dek değerlendirme dışı kalmalı (bugünkü davranış: fiyat+yazar belirler).
- **FR-006**: Künyeye cilt tipi eklenebilmeli (opsiyonel — her kitapta olmayabilir).
- **FR-007**: Künyeye popülerlik sayaçları eklenebilmeli: puan (0-5), puan adedi, satın alma adedi, okuyacağım/okuyorum/okudum, favori. Negatif değer reddedilir; sayaçlar satışa-uygunluk kararına GİRMEZ.
- **FR-008**: Katalogda görünürlük satışa-uygunluktan bağımsız kalmalı — "ürün var = keşfedilebilir" kuralı (091) sürer; bu dilim hiçbir görünürlük/filtre davranışı eklemez.

### Key Entities

- **Ürün (Product)**: mevcut kitap künyesi + YENİ: opsiyonel fiyat (sonradan atanabilir/değişir), opsiyonel yazar listesi, cilt tipi, popülerlik sayaçları, türetilmiş satışa-uygunluk durumu.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Fiyat/yazar kombinasyonlarının tamamı (var/yok × var/yok = 4 durum) beklenen satışa-uygunluk sonucunu verir; kural tek tanımdan okunur.
- **SC-002**: Fiyatsız/yazarsız doğum, fiyat atama/değiştirme ve sayaç atama davranışlarının tümü domain testleriyle (test-first) yeşildir; mevcut tüm testler geçmeye devam eder.
- **SC-003**: Geçersiz girdilerin (fiyat ≤0, negatif sayaç, puan aralık dışı) %100'ü reddedilir ve mevcut durumu değiştirmez.

## Assumptions

- Dış kontrat değişmez: `ProductChangedEvent` alanlarına dokunulmaz; satışa-uygunluk event'e bu dilimde GİRMEZ — "satışa çıktı" event'i/bildirimi ileriki dilim.
- Cilt tipi sabit küme olarak modellenir (Karton Kapak, Ciltli, Kendinden Kapak, Plastik + belirsiz).
- Popülerlik sayaçları Product üstünde düz alanlardır (yan doküman değil) — kullanıcının düz-kod tercihi.
- Veri girişi yolları, admin yüzeyi, vitrin/arama/detay-sayfası davranışı, Stock dönüşü bu dilimin DIŞINDA.
- Fiyat değişimi serbesttir (bir-kez kısıtı yok).