# Feature Specification: Arama Geçmişi Görüntüleme

**Feature Branch**: `013-search-history`

**Created**: 2026-11-16

**Status**: Draft

**Input**: Login kullanıcı kendi arama geçmişini ayrı bir sayfada görür (tekilleştirilmiş, sayfalı); öğeye tıklayıp tekrar arayabilir, tek öğeyi silebilir, tümünü temizleyebilir. Kaynak: 012'de biriken arama kayıtları.

## Clarifications

### Session 2026-11-16

- Q: Silme/temizleme kaydı gerçekten mi siler yoksa yalnız gizler mi? → A: Gerçek sil (hard — kayıtlar fiziksel silinir; ML sinyali de gider, kullanıcı niyetine saygı).
- Q: Yeni Personalization geçmiş uçlarının yetkisi? → A: Login zorunlu (anonim 401) + kendi verisi (userId token'dan); ayrı RBAC scope YOK.
- Q: Geçmiş öğesine tıklayınca tekrar-arama nerede sonuçlanır? → A: Mevcut arama/keşif ekranına yönlendir ve sorguyu orada çalıştır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Geçmişi görüntüle (Priority: P1)

Login kullanıcı "Arama Geçmişim" sayfasını açar; daha önce yaptığı aramaları benzersiz sorgu olarak, en son arandığı zamanla, en yeni üstte ve sayfalı görür. Yalnız kendi geçmişini görür.

**Why this priority**: Özelliğin çekirdeği — kullanıcının "Sohbet geçmişimde gözükmüyor" ihtiyacını tek başına karşılar. MVP.

**Independent Test**: Login kullanıcı birkaç arama yaptıktan sonra sayfayı açar → aramaları tekilleştirilmiş + sıralı + sayfalı görür; başka kullanıcının geçmişi görünmez.

**Acceptance Scenarios**:

1. **Given** login kullanıcı daha önce "dostoyevski"yi 3 kez ve "kafka"yı 1 kez aramış, **When** Arama Geçmişim sayfasını açar, **Then** 2 öğe görür ("dostoyevski" + "kafka"), her biri en son arama zamanıyla, en yeni üstte.
2. **Given** kullanıcının hiç araması yok, **When** sayfayı açar, **Then** boş-durum mesajı görür (hata değil).
3. **Given** anonim (login değil) ziyaretçi, **When** sayfaya erişmeye çalışır, **Then** geçmiş gösterilmez (login'e yönlendirilir).
4. **Given** kullanıcının 25 benzersiz araması var ve sayfa boyu 20, **When** sayfayı açar, **Then** ilk 20 öğeyi + sonraki-sayfa yolunu görür.

---

### User Story 2 - Geçmişten tekrar ara (Priority: P2)

Kullanıcı geçmiş öğesine tıklar; o sorgu (ve kaydedilen modu) ile arama yeniden çalışır ve sonuçları görür.

**Why this priority**: Geçmişi eyleme dönüştürür; görüntülemeden sonraki en yüksek değer.

**Independent Test**: Geçmiş öğesine tıkla → arama sonuç ekranı o sorgunun sonuçlarıyla açılır.

**Acceptance Scenarios**:

1. **Given** geçmişte "kafka" (keyword) öğesi var, **When** kullanıcı ona tıklar, **Then** "kafka" keyword araması çalışır ve sonuçlar gösterilir.
2. **Given** kullanıcı bir geçmiş öğesinden tekrar arar, **When** arama tamamlanır, **Then** bu yeni bir arama olarak kaydedilir; geçmiş listesi o sorguyu YENİ satır eklemeden en üste taşır (recency güncellenir — kısırdöngü/şişme yok).

---

### User Story 3 - Geçmişi sil (Priority: P3)

Kullanıcı tek bir geçmiş öğesini kaldırabilir ya da tüm geçmişini temizleyebilir.

**Why this priority**: Gizlilik/kontrol; görüntüleme ve tekrar-aramadan sonra gelir.

**Independent Test**: Bir öğeyi sil → listeden kalkar; tümünü temizle → liste boşalır; ikisi de yalnız kendi geçmişini etkiler.

**Acceptance Scenarios**:

1. **Given** geçmişte "dostoyevski" (5 ham arama = 1 öğe) var, **When** kullanıcı o öğeyi siler, **Then** öğe listeden kalkar ve o sorguya ait TÜM kayıtlar silinir (tekrar aranana dek geri gelmez).
2. **Given** kullanıcının geçmişi dolu, **When** "tümünü temizle" der ve onaylar, **Then** kullanıcının tüm geçmişi silinir; başka kullanıcıların geçmişi etkilenmez.
3. **Given** iki farklı kullanıcı, **When** biri geçmişini temizler, **Then** diğerinin geçmişi değişmez.

---

### Edge Cases

- Aynı sorgu farklı büyük/küçük harf veya Türkçe İ/ı ile ("Dostoyevski" vs "dostoyevski") → tek öğe sayılır (büyük/küçük ve Türkçe-duyarlı normalize ile tekilleştirme).
- Aynı sorgu farklı modlarda (keyword/hybrid) arandı → tek öğe; gösterilen/tekrar-çalışan mod = en son aramanın modu.
- "Tümünü temizle" sonrası sayfa → boş-durum.
- Tek öğe silme ile eşzamanlı yeni arama (aynı sorgu) → yeni kayıt geçerli; silme yalnız silme anındaki kayıtları kaldırır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem login kullanıcıya kendi arama geçmişini listelemelidir; kayıtlar yalnız istek sahibinin kimliğine (token) göre süzülür — başka kullanıcının geçmişi ASLA döndürülmez.
- **FR-002**: Liste benzersiz sorgu metnine göre TEKİLLEŞTİRİLMİŞ olmalı; her öğe sorgu + en son arama zamanı + en son modu taşır; en yeni üstte sıralanır.
- **FR-003**: Tekilleştirme büyük/küçük harf ve Türkçe İ/ı duyarsız olmalı (aynı sorgunun varyantları tek öğe).
- **FR-004**: Liste SAYFALI olmalı (sayfa boyu + sonraki sayfa); bir kullanıcının tüm geçmişi gezilebilir.
- **FR-005**: Geçmiş erişimi login GEREKTİRİR — anonim istek reddedilir (401); ayrı bir RBAC scope aranmaz, authenticated olmak + kendi `userId`'si yeterlidir.
- **FR-006**: Kullanıcı bir geçmiş öğesine tıklayınca mevcut arama/keşif ekranına yönlendirilir ve o sorgu + mod orada yeniden çalıştırılır; sonuçlar normal arama gibi gösterilir (ayrı sonuç görünümü eklenmez).
- **FR-007**: Kullanıcı tek bir geçmiş öğesini silebilmeli; silme o benzersiz sorguya ait TÜM ham kayıtları KALICI (hard) olarak kaldırır ve yalnız o kullanıcıyı etkiler.
- **FR-008**: Kullanıcı tüm geçmişini tek işlemle KALICI temizleyebilmeli (onay sonrası); yalnız kendi kayıtlarını siler. Silinen kayıt geri getirilemez (yeniden aranırsa yeni kayıt oluşur).
- **FR-009**: Boş geçmiş hata değildir — boş-durum olarak sunulur.

### Key Entities *(include if feature involves data)*

- **Arama kaydı (mevcut, 012)**: Bir login aramanın ham, değişmez izi — kullanıcı, sorgu, mod, sonuç sayısı, zaman. 013 bunu OKUR ve (yeni) KALICI SİLEBİLİR. **İnvariant revizyonu:** 012'deki "append-only, silme YOK" kuralı, kullanıcı-tetikli HARD silme/temizleme ile gevşetilir (yalnız sahibi kendi kayıtlarını fiziksel siler; sistem yine içerik mutasyonu yapmaz — yalnız ekleme/silme).
- **Geçmiş öğesi (türetilmiş görünüm)**: Benzersiz sorgunun sunum birimi — normalize sorgu + en son zaman + en son mod + (sil için) o sorgunun kayıt kümesi. Kalıcı değil; okuma anında ham kayıtlardan hesaplanır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Login kullanıcı geçmiş sayfasını açtığında kendi benzersiz aramalarını saniye-altı görür.
- **SC-002**: Bir kullanıcı başka bir kullanıcının geçmişini hiçbir koşulda göremez/silemez (yetki sızıntısı = 0).
- **SC-003**: Aynı sorgunun N tekrarı listede 1 öğe olarak görünür (tekilleştirme doğruluğu %100).
- **SC-004**: Geçmiş öğesinden tekrar arama, sonucu normal aramayla aynı sürede döndürür ve listeyi şişirmez (yeni benzersiz öğe sayısı artmaz).
- **SC-005**: Silme/temizleme sonrası ilgili öğeler listede kalıcı olarak kalkar (yeniden aranmadıkça).

## Assumptions

- 012 (`UserSearchRecord`, `personalization.mt_doc_user_search_record`) merge edilmiş/erişilebilir kabul edilir; 013 onun üstüne kurulur (bu dal 012'den çıkar).
- Geçmiş erişimi login gerektirir (anonim 401); kimlik mevcut OIDC/JWT'den çözülür (006/012 deseni) — kullanıcı yalnız kendi `userId`'siyle süzülür; ek RBAC scope yok.
- Silme/temizleme HARD'dır (kayıt fiziksel gider); ML/zevk sinyali korunmaz — kullanıcı niyetine saygı önceliklidir.
- Varsayılan sayfa boyu makul bir sabittir (ör. 20); kullanıcı ayarı kapsam dışı.
- Tekilleştirme normalizasyonu mevcut Türkçe-duyarlı ad normalizasyonu mantığıyla hizalanır.
- Geçmiş öğesinden tekrar-arama, mevcut arama yüzeyini (Catalog arama ucu) kullanır; ayrı arama mantığı eklenmez.
- Kapsam dışı: öneri/ML/zevk profili, başka kullanıcının geçmişi, anonim geçmiş, geçmiş dışa-aktarma, saklama-süresi politikası.
