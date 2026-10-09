# Feature Specification: ADR'leri Repo İçine Taşı (dev-anında kural/gerekçe kontrolü)

**Feature Branch**: `089-adr-repo-home`

**Created**: 2026-10-08

**Status**: Draft

**Input**: User description: ADR'ler (mimari karar gerekçesi) bugün Obsidian vault'ta yaşıyor. Vault
bağlayıcı değil (gerçek-kaynak sırası: kod + `CLAUDE.md` > memory > vault), bu yüzden geliştirme anında
kural/gerekçe kaynak-of-truth olarak kontrol edilemiyor. 10 ADR repo içine taşınsın; dev/ajan kod yazarken
repo'dan okuyabilsin.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Geliştirme anında gerekçeyi repodan oku (Priority: P1)

Geliştirici (veya kod yazan AI ajan) bir kararın sınırına dayandığında ("neden BC-per-service?", "sır neden
ekrana basılmaz?") gerekçeyi **repo içinden** (grep/okuma) anında bulur — vault'a, harici not defterine
çıkmadan. Gerekçe artık kaynak-of-truth katmanında (repo dosyası), dev akışının içinde.

**Why this priority**: Taşımanın tek sebebi bu. Vault dışarıda + bağlayıcı değil olduğundan bugün
imkânsız; feature'ın bütün değeri burada.

**Independent Test**: 10 kararın herhangi biri için gerekçe, repo kök dizininde bir aramayla (dosya yolu
`docs/adr/`) bulunabiliyor; vault'a hiç gidilmiyor.

**Acceptance Scenarios**:

1. **Given** dev kod yazıyor ve bir mimari karara dayandı, **When** repo içinde ilgili kararı arar,
   **Then** `docs/adr/` altında Durum/Bağlam/Karar/Sonuçlar bölümlü bir ADR dosyası bulur.
2. **Given** vault erişilemez (harici), **When** dev gerekçe arar, **Then** sonuç yine repo içinden gelir.

---

### User Story 2 - Mevcut atıflar gerçek dosyaya çözülür (Priority: P2)

`CLAUDE.md`, `docs/conventions.md` ve `specs/*` bugün ADR slug'larına atıf veriyor. Taşıma sonrası bu
atıflar **repo içi gerçek bir ADR dosyasına** çözülür — kaynak-of-truth dosya artık bağlayıcı-olmayan not
defterine işaret etmez.

**Why this priority**: Atıfların boşluğa/dış-nota işaret etmesi (kötü koku + ölü işaretçi) bu taşımayla
kapanır; ama P1 değer çekirdeği değil.

**Independent Test**: `CLAUDE.md` + `conventions.md`'deki ADR atıfları takip edilince repo içi dosyaya
varır; slug tabanlı `specs/*` atıfları da aynı dosyalara karşılık gelir.

**Acceptance Scenarios**:

1. **Given** `conventions.md` bir kuralın gerekçesine atıf veriyor, **When** atıf takip edilir,
   **Then** `docs/adr/` içindeki ADR'ye varılır (vault'a değil).

---

### User Story 3 - Kararları tek indeksten tara (Priority: P3)

Yeni gelen (veya ajan) tüm mimari kararları + durumlarını (Kabul/Superseded) **tek bir indeksten** bir
bakışta görür.

**Why this priority**: Keşif kolaylığı; çekirdek değer değil, taranabilirlik artırıcı.

**Acceptance Scenarios**:

1. **Given** okuyucu kararları görmek istiyor, **When** `docs/adr/` indeksini açar, **Then** her ADR'yi
   slug + başlık + durum ile tek listede görür.

---

### Edge Cases

- Terk edilmiş pratiği anlatan ADR (ör. moderasyon söküldü) — silinmez, **tarihsel** durum damgasıyla kalır.
- `specs/049` bugün var-olmayan bir slug'a (`adr-checkout-orchestrator-standalone-049`) atıf veriyor —
  taşımada ya doğru slug'a düzeltilir ya da ölü-atıf olarak not edilir (karar `/speckit-plan`).
- Kuralı taşınabilir olan ADR (ör. BC izolasyonu = İLKE I) — kural `constitution`/`conventions`'ta kalır;
  ADR kuralı kopyalamaz, link verir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Mevcut 10 vault ADR'si repo içine `docs/adr/` altına taşınır; her ADR'nin Durum/Bağlam/
  Karar/Sonuçlar içeriği korunur.
- **FR-002**: ADR dosya adları (slug) korunur — `specs/*` bu slug'lara zaten atıf verdiğinden yeniden
  numaralama/rename yapılmaz.
- **FR-003**: `docs/adr/` bir indeks dosyası içerir (her ADR: slug · başlık · durum).
- **FR-004**: ADR'ler arası ve ADR→doküman bağlantıları repo-içi biçime çevrilir (harici not-defteri
  wikilink sözdizimi repo'da kalmaz).
- **FR-005**: `CLAUDE.md` + `docs/conventions.md`'deki ADR slug atıfları repo-içi ADR yoluna yönlendirilir.
- **FR-006**: ADR, `conventions`/`constitution`'daki taşınabilir kural metnini tam kopyalamaz; kurala
  link verir (çiftleme guard'ı — ADR = karar+neden, conventions = kural).
- **FR-007**: Repo içi ADR katmanı, kod yazan ajanın dev anında danışacağı şekilde tanınır kılınır
  (`CLAUDE.md`/`conventions`'tan katman olarak işaret edilir).
- **FR-008**: Taşıma sonrası vault'tan ADR karar-kayıt dosyaları kaldırılır; vault'taki öğrenme notları
  (`adr-nedir`, `karar-dokuman-katmanlari`) kalır ve "ADR nerede yaşar" bölümü repo yoluna güncellenir.

### Key Entities

- **ADR (Mimari Karar Kaydı)**: tek karar = tek dosya; alanlar Durum, Bağlam, Karar, Sonuçlar; kimlik =
  slug. Bağlayıcı-olmayan gerekçe; repo içinde yaşar.
- **ADR İndeksi**: tüm ADR'lerin tek katalog görünümü (slug + başlık + durum).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: ADR slug'larına yapılan mevcut repo atıflarının %100'ü repo içi bir dosyaya çözülür
  (0 ölü işaretçi; `specs/049` istisnası planda çözülür).
- **SC-002**: 10 kararın herhangi birinin gerekçesi, harici not defteri açılmadan yalnız repo içinden
  bulunabilir.
- **SC-003**: 10 kararın tamamı durumlarıyla tek indeks görünümünde listelenir.
- **SC-004**: 0 çiftlenmiş kural tanımı — her taşınabilir kuralın tek evi var; ADR kopya değil link tutar.

## Assumptions

- Hedef "kullanıcı" = geliştiriciler + kod yazan AI ajan; kullanım anı = geliştirme (dev-time).
- `docs/adr/`, `docs/conventions.md` ile aynı kökte yer alır (doküman katmanı komşuluğu).
- Vault serbest-biçim not + öğrenme için kalır; bağlayıcı kural/karar artık vault'ta tutulmaz.
- Yeniden numaralama yok (slug'lar korunur); git branch/commit stratejisi spec kapsamı dışı (plan kararı).
- Taşınan ADR'lerin kuralları zaten `constitution`/`conventions`'ta mevcutsa orada kalır; ADR link verir.