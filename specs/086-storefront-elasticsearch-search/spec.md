# Feature Specification: Storefront Elasticsearch Search

**Feature Branch**: `086-storefront-elasticsearch-search`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Storefront arama yüzeyini Elasticsearch'e taşı. Müşteri Claude Desktop'tan query_storefront MCP tool'uyla metin araması yapar. CQRS+Event Sourcing: Postgres ince event-log/gerçek-kaynak, Elasticsearch yeniden-kurulabilir sorgu projeksiyonu. LLM düz Elasticsearch DSL üretir (guard ertelendi). UserPurchase → Library BC."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Müşteri serbest metinle kitap arar (Priority: P1)

Müşteri kendi AI istemcisiyle (Claude Desktop) doğal dilde kitap arar: "korku temalı,
50 TL altı, puanı iyi kitaplar". Sistem eşleşen satılabilir kitapları ilgi sırasına göre
döner; metin eşleşme + anlamsal yakınlık + fiyat/puan süzme tek sorguda birleşir.

**Why this priority**: Uygulamanın kalbi metin-bazlı ürün keşfi. Bu olmadan müşteri yüzeyi yok.

**Independent Test**: Index'te örnek kitap kümesiyle, bir metin isteği gönderilip dönen
sonuçların filtre+alaka kriterlerini karşıladığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** index'te satılabilir kitaplar var, **When** müşteri "korku 50 TL altı iyi puan" arar, **Then** yalnız fiyatı 50 altı + puanı eşik üstü + konusu korkuya yakın kitaplar alaka sırasıyla döner.
2. **Given** müşteri yazım hatası yapar ("hari potter"), **When** arar, **Then** doğru kitap yine bulunur (fuzzy tolerans).
3. **Given** bir kitaba benzer istenir, **When** "şuna benzer kitaplar" denir, **Then** anlamsal komşu kitaplar döner.
4. **Given** hiçbir kitap eşleşmez, **When** arar, **Then** sistem dürüstçe "bulunamadı" der, uydurma sonuç üretmez.

---

### User Story 2 - Kaynak değişimi vitrine yansır (Priority: P1)

Catalog/Stock/Reviews/Discount'ta bir ürün değişince (fiyat, stok, puan özeti, indirim)
arama vitrinindeki o kitap güncellenir. Müşteri güncel bilgiyle arar.

**Why this priority**: Bayat vitrin yanlış sonuç = güvensiz arama. Değişim akışı P1.

**Independent Test**: İlgili integration event yayınlanıp, kısa süre sonra aramada o kitabın
alanının (ör. stok) güncellendiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** bir kitap vitrinde, **When** StockChanged (mutlak değer) gelir, **Then** aramada stok alanı yeni değere döner.
2. **Given** aynı event tekrar gelir (at-least-once), **When** işlenir, **Then** sonuç değişmez (idempotent).
3. **Given** iki değişim ters sırada gelir, **When** işlenir, **Then** vitrin son geçerli hâli gösterir (eski üstüne yazmaz).

---

### User Story 3 - Arama vitrini yeniden kurulabilir (Priority: P2)

Operatör arama index'ini sıfırdan yeniden kurabilir (şema değişimi, bozulma, taşıma)
— mevcut veriyi kaybetmeden.

**Why this priority**: Event Sourcing'in asıl kazancı; index silinse bile veri kurtarılır.
Prod'a çıkmadan gerekli ama ilk müşteri değerinden sonra.

**Independent Test**: Index silinip yeniden-kurma tetiklenir; vitrin önceki içerikle geri gelir.

**Acceptance Scenarios**:

1. **Given** event-log dolu, **When** index yeniden-kurma tetiklenir, **Then** tüm kitaplar akış oynatılarak geri yüklenir.
2. **Given** sistem ilk kez kuruluyor (event-log boş), **When** backfill çalışır, **Then** kaynak BC'lerden mevcut kitaplar bir kez doldurulur.

---

### Edge Cases

- Stok eventi, ürün vitrinde daha oluşmadan gelirse → vitrin tutarlı kalmalı (oluştur-veya-güncelle).
- Müşteri kapsam-dışı şey isterse ("siparişim nerede") → arama yüzeyi üstlenmez, dürüstçe yönlendirir.
- Anlamsal arama için embedding üretilemezse (dış servis hatası) → metin aramaya düşer, çökmemeli.
- Satılamaz/silinmiş kitap (IsDeleted, fiyatsız) → arama sonucunda görünmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem müşterinin serbest metin isteğini kabul edip eşleşen satılabilir kitapları alaka sırasıyla döndürmeli (metin eşleşme + anlamsal yakınlık + fiyat/puan/kategori süzme birleşik).
- **FR-002**: Sistem yazım hatası/yakın-eşleşmeyi tolere etmeli (fuzzy).
- **FR-003**: Sistem **belirli bir referans kitaba** anlamsal olarak benzer kitapları döndürebilmeli (FR-001'in genel aramasından ayrı: girdi = bir ürün, çıktı = anlamsal komşuları).
- **FR-004**: Sistem kaynak BC'lerden (Catalog/Stock/Reviews/Discount) gelen değişiklik olaylarını ürün başına kalıcı, sıralı bir akışa (gerçek-kaynak) eklemeli.
- **FR-005**: Sistem bu akışı katlayarak arama vitrinini (projeksiyon) türetmeli; vitrin kaybolursa akıştan yeniden kurulabilmeli.
- **FR-006**: Vitrin güncellemesi idempotent olmalı (tekrarlanan olay sonucu değiştirmez) ve olay sırasını korumalı (eski değer yeniyi ezmez).
- **FR-007**: İlk kurulumda (akış boşken) sistem mevcut kitapları kaynak BC'lerden bir kerelik dolduruabilmeli (soğuk başlangıç backfill).
- **FR-008**: Satılamaz kitaplar (silinmiş/fiyatsız) arama sonuçlarında görünmemeli.
- **FR-009**: Sistem eşleşme yoksa veya istek kapsam-dışıysa dürüst dönüş yapmalı; sonuç uydurmamalı. Her sorgu denemesi (başarılı/red/hata) iz bırakmalı.
- **FR-010**: Müşteri sorgu yüzeyi tek MCP tool (`query_storefront`) üzerinden sunulmalı; müşteri yüzeyinde başka arama ucu olmamalı.
- **FR-011**: `UserPurchase` (kullanıcının aldığı kitaplar) kaydı arama servisinden çıkarılıp Library BC'ye taşınmalı.

### Key Entities *(include if feature involves data)*

- **Ürün olay akışı**: Ürün başına (kimlik = ProductId), kaynak BC olaylarının sıralı, kalıcı listesi. Gerçek-kaynak.
- **Arama vitrin dokümanı**: Akışın katlanmış hâli (kimlik = ISBN). Alanlar: ad, açıklama, yazarlar, yayınevi, kategori, fiyat, efektif fiyat, stok, puan ortalaması/sayısı, specs, aile kodu, görsel, eklenme, indirim, anlamsal vektör. Arama/süzme/sıralama buradan.
- **Sorgu izi**: Her arama denemesi — çalıştı/reddedildi/hata, satır sayısı, süre. Denetim.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Müşteri tipik bir arama isteğine 1 saniyeden kısa sürede sonuç alır.
- **SC-002**: Yazım hatalı aramaların en az %90'ı doğru kitabı ilk sayfada döndürür.
- **SC-003**: Kaynak değişimi (stok/fiyat) aramada 5 saniye içinde görünür (eventual consistency penceresi).
- **SC-004**: Arama index'i sıfırdan yeniden kurulduğunda veri kaybı %0 (akıştaki tüm ürünler geri gelir).
- **SC-005**: Eşleşme olmayan isteklerin %100'ü dürüst "bulunamadı" döner; uydurma kitap oranı %0.

## Assumptions

- Müşteri kendi AI istemcisini (Claude Desktop) kullanır; MCP tek müşteri yüzeyidir (mevcut agent-only yön).
- Gerçek stok checkout'ta okunur; arama vitrini yaklaşık/bayat-toleranslıdır (satış kararı vermez).
- Anlamsal arama için embedding üretimi mevcut OpenAI entegrasyonuyla yapılır.
- Integration event sözleşmeleri (ProductChanged/StockChanged/ReviewSummaryChanged/ProductDiscountChanged) mevcuttur ve değişmez.
- JSON guard (LLM üretilen sorgunun güvenlik denetimi) bu spec'in KAPSAMI DIŞINDA — ayrı iş olarak sonra ele alınır. Bu spec müşteri yüzeyini kullanıcının güvendiği bir ortamda (kendi istemcisi) varsayar; prod sertleştirme guard işine bağlı.
- Excel import auto-publish KAPSAM DIŞI (Catalog 083 ayrı iş).
