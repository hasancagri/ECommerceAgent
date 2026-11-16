# Feature Specification: Kullanıcı Arama Kaydı + App-BFF

**Feature Branch**: `012-search-capture-bff`

**Created**: 2026-11-16

**Status**: Draft

**Input**: Login kullanıcı arayüzde arama yaptığında araması kullanıcıya özel kaydedilir. İstemciler (web + ileride mobile) tek bir app-BFF üzerinden arar; BFF aramayı yürütür ve login aramada `UserSearched` sinyali yayınlar; yeni Personalization BC bu sinyali tüketip ham arama logunu izole saklar. Kişiselleştirme/öneri/ML bu dilimde YOK — yalnız veri temeli.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Login kullanıcının araması kaydedilir (Priority: P1) 🎯 MVP

Giriş yapmış kullanıcı arayüzde bir arama yapar ("distopik roman", "Sabahattin Ali"). Sonuçları normal görür; arkada bu arama kullanıcıya özel kayıt altına alınır (ne aradı, hangi mod, kaç sonuç, ne zaman). Kayıt, gelecekteki kişiselleştirmenin ham sinyalidir; bu dilimde kullanıcıya geri gösterilmez.

**Why this priority**: Platformun kişiselleştirme temelinin ilk taşı — kullanıcı-özel zevk sinyali burada birikmeye başlar. Olmazsa sonraki öneri/zevk-profili dilimlerinin verisi olmaz.

**Independent Test**: Login kullanıcı bir arama yapar → sistemde o kullanıcıya bağlı bir arama kaydı oluşur (query/mod/sonuç-sayısı/zaman doğru); kullanıcı sonucu anında alır.

**Acceptance Scenarios**:

1. **Given** giriş yapmış kullanıcı, **When** arama yapar, **Then** sonuçlar döner VE o kullanıcıya özel bir arama kaydı oluşur (query + mod + sonuç-sayısı + zaman).
2. **Given** aynı kullanıcı arka arkaya birden çok arama yapar, **When** aramalar biter, **Then** her arama ayrı bir kayıt olur (append-only, üzerine yazılmaz).
3. **Given** kayıt alt-sistemi geçici hata verir, **When** kullanıcı arar, **Then** kullanıcı sonucu yine de anında alır (arama kaydı aramayı bloklamaz/başarısız etmez).

---

### User Story 2 - Anonim arama kaydedilmez (Priority: P2)

Giriş yapmamış (anonim) ziyaretçi de arama yapabilir (keşif login istemez) ama araması kayıt altına ALINMAZ — kişisel veri yalnız sahipli hesapta birikir.

**Why this priority**: Gizlilik sınırı + "login'de kaydet" kuralının açık yüzü. Anonim keşif açık kalmalı ama kimliksiz veri toplanmamalı.

**Independent Test**: Anonim ziyaretçi arama yapar → sonuç döner, hiçbir arama kaydı oluşmaz.

**Acceptance Scenarios**:

1. **Given** anonim ziyaretçi, **When** arama yapar, **Then** sonuçlar döner VE hiçbir kullanıcı-arama kaydı oluşmaz.

---

### User Story 3 - Arama deneyimi tek giriş noktasından bozulmadan çalışır (Priority: P3)

İstemci (web, ileride mobile) aramayı tek bir uygulama-arka-ucu (app-BFF) üzerinden yapar. Kullanıcı açısından arama sonucu ve hızı değişmez; kayıt tamamen görünmezdir.

**Why this priority**: BFF, mobil dahil tüm istemciler için ortak giriş noktası + kayıt sinyalinin doğal evi. Ama bu altyapı kullanıcı deneyimini bozmamalı (şeffaf olmalı).

**Independent Test**: Aynı arama BFF üzerinden yapılır → sonuç içeriği ve hissi doğrudan yapılan aramayla aynı (etkileşimli, saniye-altı).

**Acceptance Scenarios**:

1. **Given** istemci app-BFF üzerinden arar, **When** sonuç gelir, **Then** sonuç kümesi ve sırası Catalog aramasıyla aynıdır; gecikme kullanıcı-hissinde fark yaratmaz.

---

### Edge Cases

- Kayıt alt-sistemi tamamen erişilemez → arama çalışmaya devam eder; kayıt sonradan dayanıklı şekilde yeniden denenir, kalıcı başarısızlık izlenir (sessiz kayıp yok).
- Boş/sonuçsuz arama (login) → yine kaydedilir (sonuç-sayısı 0); "ne aradı" sinyali değerlidir.
- Oturum süresi dolmuş/geçersiz token ile arama → anonim muamelesi (kaydedilmez), arama yine çalışır.
- Aynı kullanıcı aynı metni tekrar arar → ayrı kayıt (tekilleştirme yok; tekrar da sinyaldir).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, giriş yapmış kullanıcının her aramasını kullanıcıya özel kayıt altına almalı (aranan metin, arama modu, dönen sonuç sayısı, zaman damgası).
- **FR-002**: Anonim (kimliksiz) arama hiçbir kullanıcı-arama kaydı ÜRETMEMELİ.
- **FR-003**: İstemciler aramayı tek bir uygulama-arka-ucu (app-BFF) üzerinden yapmalı; BFF aramayı yürütüp sonucu istemciye döndürmeli.
- **FR-004**: Arama kaydı, arama yanıtını geciktirmemeli/bloklamamalı — kullanıcı sonucu anında almalı, kayıt arka planda yapılmalı.
- **FR-005**: Kayıt dayanıklı olmalı — geçici başarısızlık yeniden denenmeli; kalıcı başarısızlık gözlemlenebilir olmalı (sessiz kayıp yok).
- **FR-006**: Arama sonucu, kayıt akışının başarısından BAĞIMSIZ olmalı — kayıt başarısız olsa bile kullanıcı doğru sonucu almalı.
- **FR-007**: Arama kayıtları kullanıcıya özel ve izole saklanmalı; bir kullanıcının kaydına başka kullanıcı/BC doğrudan erişememeli.
- **FR-008**: Bu dilim YALNIZ arama sinyalini kapsamalı; satın alma/kütüphane gibi diğer kullanıcı sinyalleri kapsam dışı.
- **FR-009**: Kaydedilen veri bu dilimde kullanıcıya SUNULMAMALI/okunmamalı (öneri/serving yok); yalnız yazım/biriktirme.

### Key Entities *(include if feature involves data)*

- **Arama kaydı (UserSearchRecord)**: Bir kullanıcının tek bir aramasının ham izi — kullanıcı kimliği, aranan metin, arama modu, dönen sonuç sayısı, zaman. Append-only (değiştirilmez/silinmez); izole kişiselleştirme deposunda yaşar.
- **Arama sinyali (UserSearched integration event)**: "Şu kullanıcı şunu aradı" olgusunun BC'ler-arası duyurusu — app-BFF yayınlar, kişiselleştirme tüketir. Alanlar: kullanıcı, metin, mod, sonuç-sayısı, zaman.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Giriş yapmış kullanıcının her araması, aramadan kısa süre sonra (saniyeler mertebesi, eventual) kalıcı olarak o kullanıcıya bağlı tek bir kayıt olarak görünür.
- **SC-002**: Anonim aramaların %100'ü hiçbir kullanıcı-arama kaydı üretmez.
- **SC-003**: Arama, app-BFF üzerinden yapıldığında kullanıcı için etkileşimli kalır (sonuç hissi saniye-altı); doğrudan aramaya kıyasla fark edilir yavaşlama olmaz.
- **SC-004**: Arama başarı oranı kayıt alt-sisteminin durumundan bağımsızdır — kayıt alt-sistemi hata verse bile aramaların %100'ü doğru sonuç döndürür.
- **SC-005**: Geçici kayıt hatasında veri kaybı olmaz — hata giderilince ilgili arama kaydı tutar (dayanıklı yeniden deneme).

## Assumptions

- **App-BFF yeni bir servistir**; mevcut Gateway kenar yönlendirici (edge) olarak kalır — istemci → Gateway → BFF → BC'ler. Admin ve diğer trafik gateway→BC doğrudan sürer.
- Arama **kullanıcı sinyali BC'ler-arası integration event** ile akar (yayıncı app-BFF, tüketici Personalization); senkron çağrı değil. Kişiselleştirme kendi **izole veritabanında** yaşar (BC izolasyonu).
- **Catalog arama yeteneği değişmez** (011); app-BFF onu çağırır ve sinyali yayınlar — Catalog arama-aktivitesi sinyali taşımaz (saf ürün BC'si kalır).
- **Kimlik mevcut OIDC/JWT ile** sağlanır; app-BFF giriş yapmış kullanıcıyı istemci token'ından çözer. Web istemci aramayı BFF'e taşır ve giriş yapmışken kimlik bilgisini iletir.
- **Kayıt saklama süresi**: veri temeli olduğu için süresiz tutulur (silme/retention bu dilimde kapsam dışı).
- **Kapsam dışı (sonraki dilimler)**: öneri/serving, zevk-profili toplama (ham log yeter), ML/ONNX/Python, ChatAgent (BFF ileride barındırır), satın alma/kütüphane sinyalleri, kişiselleştirme okuma yüzeyi, mobil istemci (BFF mobile-hazır kurulur ama mobil uygulama ayrı).
