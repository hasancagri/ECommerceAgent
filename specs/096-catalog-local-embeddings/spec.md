# Feature Specification: Catalog local embedding altyapısı (kitap vektörleri)

**Feature Branch**: `096-catalog-local-embeddings`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "catalogDb'ye pgvector + product_embedding yan tablosu; Catalog içi
reconcile süreci embedding'i LOCAL modelle (Ollama) üretir. ARAMA BU SPEC'İN DIŞI (kullanıcı
netleştirmesi: 'şimdilik arama olmayacak' — hibrit arama ve danışman agent sonraki spec'ler).
Broker yok, seed'e dokunulmaz; v1 yalnız product_embedding; 095 canlı doğrulaması bu feature'ın
sonunda aynı Aspire koşusunda."

## Bağlam

Kişiselleştirilmiş keşif kuzey-yıldızının omurgası embedding uzayı (arama + benzer-kitap + zevk
vektörü hep buradan beslenecek). Bu feature O OMURGAYI döşer: her kitabın künye metni local embedding
modeliyle (Ollama) vektörleşir ve kataloğun kendi veritabanında yaşar. Tüketicisi (arama/öneri/agent)
SONRAKİ spec'lerde — burada yalnız üretim + güncel tutma + doğrulama var.
**Artefakt kademesi: Tam** (yeni tablo + yeni arka plan süreci + yeni dış bağımlılık [Ollama]).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Katalog kendiliğinden vektörleşir (Priority: P1)

Sistem açıldıktan sonra arka plandaki süreç, vektörü olmayan ya da metni değişmiş kitapları bulur,
local embedding modeliyle TOPLU vektörleştirir ve kendi veritabanındaki yan tabloya yazar. Seed'e ve
kitap yazma yollarına dokunulmaz; vektörleşme kendi hızında ilerler (eventual).

**Why this priority**: Feature'ın kendisi — embedding omurgası.

**Independent Test**: Temiz açılış + süreç tamamlanınca 11.438 kitabın tamamının vektör kaydı var;
ikinci koşu hiçbir kaydı yeniden üretmez.

**Acceptance Scenarios**:

1. **Given** seed'le dolu katalog ve boş vektör tablosu, **When** süreç koşar, **Then** tüm kitaplar
   için vektör + içerik-hash + model kimliği kaydı oluşur; ilerleme/bitiş loglardan izlenebilir.
2. **Given** vektörü güncel kitap, **When** süreç yeniden koşar, **Then** yeniden vektörleştirme
   YAPILMAZ (hash eşleşmesi — idempotent, boşa model çağrısı yok).
3. **Given** metni değişen kitap, **When** sonraki tarama koşar, **Then** yalnız o kitabın vektörü
   tazelenir.
4. **Given** model kimliği değişti (yeni embedding modeli), **When** süreç koşar, **Then** tüm
   kayıtlar bayat sayılır ve toplu tazelenir — sistem bu sırada normal çalışır.

---

### User Story 2 - Ollama yokken sistem yara almaz (Priority: P2)

Local embedding servisi (Ollama) kapalı/erişilemezken sistem açılır, seed dolar, hiçbir akış
bloklanmaz; süreç hatayı loglayıp sonraki döngüde kendiliğinden toparlar.

**Why this priority**: Dev makinesinde Ollama her zaman açık olmayacak — dayanıklılık şart; ama US1
olmadan anlamı yok.

**Independent Test**: Ollama kapalıyken temiz açılış → sistem yeşil, vektör tablosu boş; Ollama
açılınca süreç müdahalesiz dolum yapar.

**Acceptance Scenarios**:

1. **Given** Ollama KAPALI, **When** sistem açılır, **Then** açılış/seed/diğer BC akışları etkilenmez;
   süreç erişim hatasını loglar, çökmez.
2. **Given** Ollama sonradan açıldı, **When** sıradaki döngü koşar, **Then** dolum kaldığı yerden
   müdahalesiz devam eder.
3. **Given** dolum yarıda kesildi (host restart), **When** sistem yeniden açılır, **Then** süreç
   eksikten devam eder (yeniden başlatılabilirlik).

---

### User Story 3 - 095+096 canlı doğrulama kapanışı (Priority: P3)

Temiz veritabanlarıyla tam sistem açılışında 095'in ertelenen canlı doğrulaması (seed sayımları,
event alanları, kapak hattı) ve 096 doğrulaması (vektör doluşu, idempotensi) TEK koşuda yapılır.

**Why this priority**: İki feature'ın kanıt kapanışı; işlev eklemez, "bitti"yi mühürler.

**Independent Test**: specs/095 quickstart §2-§6 + bu feature'ın quickstart'ı uçtan uca yeşil.

**Acceptance Scenarios**:

1. **Given** temiz DB'ler + Ollama açık, **When** sistem açılır ve süreçler tamamlanır, **Then**
   095 sayımları (11.438 ürün, 31+~180 kategori, kapak dolumu, event alanları) ve 096 vektör doluşu
   (%100 + ikinci koşu 0 üretim) birlikte doğrulanır.

---

### Edge Cases

- Açıklaması boş kitap (seed'de var: ör. sayfa/dil eksik satırlar): ad+künye metniyle yine
  vektörleşir; satır atlanmaz.
- Çok uzun açıklama (model bağlam sınırı): metin güvenli uzunlukta kırpılır — süreç satırı atlamaz,
  hash kırpılmış metinden hesaplanır (tutarlılık).
- Ollama model henüz çekilmemiş (404): erişim hatası gibi loglanıp sonraki döngüde denenir; operatöre
  log mesajında model adı söylenir.
- Tek kitabın embed çağrısı hata verirse: batch'in kalanı yazılır, hatalı kitap sonraki döngüye kalır
  (zehirli satır dolumu durdurmaz).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Kitap başına bir vektör kaydı tutulur: kitap kimliği (1:1), vektör, embed edilen
  içeriğin hash'i, üreten model kimliği, güncelleme zamanı. Kayıtlar kataloğun KENDİ veritabanında
  yaşar (ayrı arama sunucusu/DB yok).
- **FR-002**: Vektörleştirmeyi Catalog'un kendi arka plan süreci yapar: eksik/bayat kayıtları kendi
  veritabanından sorgular (broker YOK), local modele TOPLU istek atar, sonuçları yazar; döngü
  idempotent, yeniden başlatılabilir ve kendi kendini onarır (US2).
- **FR-003**: Embed edilen metin kitap künyesinden tek yerde tanımlı bileşimle türetilir: ad +
  açıklama + yazar adları + kategori zinciri adları; içerik-hash bu metinden hesaplanır.
- **FR-004**: Embedding üretimi LOCAL çalışır — dış/bulut embedding API'si KULLANILMAZ. Çalışma
  zamanı Ollama (kullanıcı kararı); model ve servis adresi tip'li config'ten gelir.
- **FR-005**: Ollama erişilemezken hiçbir akış (açılış, seed, diğer BC'ler) bloklanmaz; süreç
  loglayıp sonraki döngüde dener (FR-002 döngüsünün parçası).
- **FR-006**: Model kimliği değişince mevcut kayıtlar bayat sayılır; toplu tazeleme aynı döngüden,
  müdahalesiz çalışır.
- **FR-007**: v1'de yalnız kitap vektörlenir — kavram başına ayrı tablo ilkesi (yazar vb. kendi
  spec'iyle gelir); genel/polimorfik embedding tablosu KURULMAZ.
- **FR-008**: Bu spec'te ARAMA YOK: vektörlerin okuma/sorgu yüzeyi (hibrit arama, benzer-kitap,
  danışman agent) sonraki spec'lerin işi. Doğrulama yalnız dolum/tazelik üstünden yapılır.
- **FR-009**: Catalog FLOW.md yeni sürece hizalanır (İlke VII; planned sticky as-built olur — aynı PR).
- **FR-010**: "Arama altyapısı ES'ten Postgres'e katlanıyor; embedding omurgası local modelle" kararı
  ADR'ye yazılır (086'nın tersine dönüşü — Yapma listesindeki Storefront/ES maddesinin kalıcı gerekçesi).

### Key Entities

- **Kitap vektör kaydı**: kitap kimliği (1:1), vektör, içerik-hash, model kimliği, zaman damgası.
- **Embedding metni**: kitap künyesinden türetilen, tek yerde tanımlı birleşik metin (kalıcı değil —
  üretim anında kurulur; hash'i kalıcı).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Temiz açılış + süreç tamamlanınca kitapların %100'ü (11.438) vektör kaydına sahip.
- **SC-002**: İkinci koşu 0 yeniden-üretim yapar (idempotensi — model çağrı sayısı loglardan).
- **SC-003**: Tek kitabın metni değişince yalnız 1 kayıt tazelenir; model değişince tamamı.
- **SC-004**: Ollama kapalı açılışta sistem tamamen çalışır (seed + diğer BC'ler); Ollama açılınca
  dolum müdahalesiz tamamlanır.
- **SC-005**: İlk tam dolum lokal makinede saatler değil dakikalar mertebesinde biter (toplu istek).
- **SC-006**: 095'in ertelenen canlı doğrulama maddeleri (specs/095 tasks T014/T020/T026) bu
  feature'ın kapanışında yeşil.

## Assumptions

- **Ollama dev makinede koşar** (kullanıcı kurulumu); Aspire onu BAŞLATMAZ, dış bağımlılık olarak
  adreslenir. Prod/k8s dağıtımı bu spec'in dışı.
- **Embedding modeli plan aşamasında seçilir** (Türkçe metin kalitesi gözetilerek; ör. çok-dilli bir
  embedding modeli) — spec model adına bağlanmaz, FR-006 değişimi zaten karşılar.
- **Eventual consistency kabul**: vektörler kitaptan sonra gelir; tüketici yüzey olmadığından görünür
  etkisi yok.
- **Kitap yazma yollarına dokunulmaz**: tazelik sinyali event değil içerik-hash karşılaştırması
  (reconcile) — kaçan hiçbir değişiklik kalıcı bayat kalmaz.
- **095 kapanışı buraya bağlı** (SC-006): 095 PR'ı merge'lendi, canlı doğrulaması bilinçli buraya
  ertelendi (tek Aspire koşusu).
