# Feature Specification: ScoutAgent Router → Sub-Agent Routing

**Feature Branch**: `019-scout-subagent-routing`

**Created**: 2026-11-19

**Status**: Draft

**Input**: User description: "ScoutAgent'ı tek düz agent'tan router → sub-agent mimarisine çevir. Router niyeti anlar, BC başına bir sub-agent'a dağıtır (agent-as-tool). Her sub-agent yalnız kendi MCP ucuna bağlanır, kendi modelini kullanabilir, tipli çıktı üretir."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Niyete göre yalnız ilgili alana bağlanma (Priority: P1)

Kullanıcı sohbete serbest metin yazar (ör. "Dostoyevski'nin ucuz kitapları"). Sistem niyeti anlar ve
yalnızca ilgili alanın (kitap arama) sub-agent'ını devreye alır; alakasız alanların (sepet, sipariş,
kütüphane, adres…) veri uçlarına hiç bağlanmaz. Kullanıcı bugünküyle aynı kart cevabını alır.

**Why this priority**: Çekirdek kazanç — bugün her mesajda 7 veri ucu kör yoklanıyor. Niyete göre
seçici bağlanma hem gereksiz işi keser hem de yanıt kalitesini korur/artırır. Tek başına teslim
edilse bile değer üretir (daha hızlı, daha ucuz, daha odaklı).

**Independent Test**: Anonim "Dostoyevski" araması gönder → yalnız katalog alanının yoklandığı,
diğer 6 alana bağlantı kurulmadığı gözlemlenebilir (log/iz); kart cevabı bugünküyle eşdeğer.

**Acceptance Scenarios**:

1. **Given** anonim kullanıcı, **When** "Suç ve Ceza kaç sayfa" yazar, **Then** yalnız kitap-detay
   alanı devreye girer ve kitap künyesi döner; kişisel alanlara bağlantı kurulmaz.
2. **Given** giriş yapmış kullanıcı, **When** "sepetimde ne var" yazar, **Then** yalnız sepet alanı
   devreye girer ve sepet kartı döner; katalog/diğer alanlara bağlantı kurulmaz.
3. **Given** herhangi bir kullanıcı, **When** alan-dışı bir şey sorar (ör. hava durumu), **Then**
   hiçbir veri alanına bağlanılmaz ve kapsam nazikçe belirtilir.

---

### User Story 2 - Alan başına farklı yapay zeka modeli (Priority: P2)

Sistem yöneticisi, niyet-dağıtımı yapan bileşen için ucuz/hızlı bir model, zor akıl-yürütme gerektiren
alanlar için daha güçlü bir model yapılandırabilir. Her alan kendi modeliyle çalışır.

**Why this priority**: Maliyet/kalite ayarını alan bazında yapma imkânı — ucuz dağıtım + gerektiğinde
güçlü uzman. Doğrudan işletme maliyetini düşürür. US1 olmadan anlamsız olduğu için P2.

**Independent Test**: Dağıtıcı ve bir sub-agent farklı modellerle yapılandırılır → bir sohbet turunda
her bileşenin kendi yapılandırılmış modeliyle çağrı yaptığı iz/log'dan doğrulanır.

**Acceptance Scenarios**:

1. **Given** dağıtıcıya model A, katalog alanına model B atanmış, **When** kitap araması yapılır,
   **Then** niyet-dağıtımı A ile, katalog yanıtı B ile üretilir.
2. **Given** bir alana model atanmamış, **When** o alan devreye girer, **Then** yapılandırılmış
   varsayılan model kullanılır (hata yok).

---

### User Story 3 - Giriş gerektiren alanlarda kimlik taşınması (Priority: P2)

Giriş yapmış kullanıcı kişisel bir şey sorduğunda (sepet/sipariş/kütüphane/adres/arama geçmişi), ilgili
alan devreye girer ve kullanıcının kimliği ilgili servise taşınır; yetki o serviste doğrulanır. Giriş
yoksa kişisel alan veri döndürmez, kullanıcı nazikçe girişe yönlendirilir.

**Why this priority**: Mevcut güvenlik sınırını (yetki downstream serviste) korur. US1 davranışının
kişisel alanlar için bozulmadığını güvence altına alır.

**Independent Test**: Girişli kullanıcı "siparişlerim" sorar → yalnız sipariş alanı devreye girer,
kimlik taşınır, siparişler döner. Aynı istek anonimde kişisel veri döndürmez, girişe yönlendirir.

**Acceptance Scenarios**:

1. **Given** girişli kullanıcı, **When** "kütüphanemde ne var" sorar, **Then** kütüphane alanı kimlikle
   çağrılır ve kullanıcının kayıtları döner.
2. **Given** anonim kullanıcı, **When** "siparişlerim" sorar, **Then** kişisel veri dönmez; giriş
   gerektiği nazikçe söylenir (uydurma yok).

---

### Edge Cases

- **Çok-alanlı tek mesaj** ("sepetim ve Dostoyevski kitapları"): dağıtıcı birden çok alanı devreye
  alabilir; dönen kartlar tek cevapta birleşir (sıra öngörülebilir olmalı).
- **Hiçbir alana uymayan niyet**: hiçbir veri ucu yoklanmaz; kapsam-dışı yanıtı döner.
- **Bir alan erişilemez (servis down / giriş yok)**: o alan sessizce atlanır, diğerleri etkilenmez,
  kullanıcıya teknik hata sızmaz (mevcut dayanıklılık davranışı korunur).
- **Yanlış alana dağıtım**: dağıtıcı yanlış alan seçerse sonuç boş/alakasız olabilir — kullanıcı
  yeniden sorabilmeli; sistem çökmemeli.
- **Sayfalama ("devamını getir")**: önceki aramanın devamı aynı alana, sonraki sayfayla gider.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, gelen serbest-metin mesajın niyetini anlayıp iş alanlarına (katalog, yorumlar,
  sepet, sipariş, kütüphane, adres, arama-geçmişi) **dağıtan** bir yönlendirme katmanı sunmalı.
- **FR-002**: Yönlendirme kararını **yapay zekâ** vermeli (kural/regex sınıflandırıcı DEĞİL); mevcut
  tasarım kararı (D3) bu yönüyle korunur.
- **FR-003**: Her iş alanı yalnızca **kendi veri ucuna** bağlanmalı; bir mesaj için seçilmeyen alanların
  veri uçları **hiç yoklanmamalı** (bugünkü "her istekte tümünü yokla" davranışı kalkar).
- **FR-004**: Sistem, yönlendirici ve her alan için **ayrı yapay zekâ modeli** yapılandırmaya izin
  vermeli; model atanmamışsa yapılandırılmış varsayılan kullanılmalı.
- **FR-005**: Her alan **yapılandırılmış (şemaya uygun) çıktı** üretmeli; dönen kart verisi ilgili
  servisin döndürdüğünden BİREBİR olmalı (fiyat/kapak/künye uydurma YOK).
- **FR-006**: Giriş gerektiren alanlar çağrıldığında kullanıcının kimliği ilgili servise taşınmalı;
  yetki o serviste doğrulanmalı. Giriş yoksa kişisel veri dönmemeli, kullanıcı girişe yönlendirilmeli.
- **FR-007**: Sistem BC verisini DEĞİŞTİRMEMELİ — tüm alanlar yalnız **okuma** yapar (query-only).
- **FR-008**: Bir alan erişilemezse (servis down / yetki yok) o alan atlanmalı, diğer alanlar
  çalışmaya devam etmeli; kullanıcıya ham teknik hata gösterilmemeli.
- **FR-009**: Giriş yapmış kullanıcı için sohbet kalıcılığı, kart-reload sadakati ve arama sinyali
  (`UserSearched` yalnız login+dolu-sorgu) davranışları **değişmeden** korunmalı.
- **FR-010**: Çok-alanlı tek mesajda birden çok alan devreye girebilmeli; dönen kartlar tek cevapta
  öngörülebilir sırayla birleşmeli.

### Key Entities *(include if feature involves data)*

- **Yönlendirici (Router)**: Niyeti anlayıp alan seçen bileşen; kendi modeli var. Durum tutmaz.
- **Alan sub-agent'ı**: İş alanı başına bir uzman bileşen (7 adet); yalnız kendi veri ucuna bağlı,
  kendi modeli + kendi dar yönergesi + yapılandırılmış çıktısı var.
- **Model yapılandırması**: Yönlendirici ve her alan için model adı/sağlayıcı eşlemesi.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tek-alanlı bir mesajda, seçilen alan dışındaki **tüm** veri uçlarına kurulan bağlantı
  sayısı **sıfır** (bugün: her istekte 7 uç yoklanıyor).
- **SC-002**: Kart cevaplarının içeriği (ad, yazar, fiyat, kapak, künye) bugünkü düz-agent çıktısıyla
  **eşdeğer** — kullanıcı için görünür bir gerileme yok.
- **SC-003**: Yönlendirici ve en az bir alan **farklı modelle** çalışabiliyor; yapılandırma değişince
  kod değişmeden model değişiyor.
- **SC-004**: Giriş gerektiren her alan yalnız girişli kullanıcıda veri döndürüyor; anonimde kişisel
  veri **sıfır** sızıntı.
- **SC-005**: Bir alan erişilemez olduğunda sohbet turu yine de başarılı tamamlanıyor (diğer alanlar
  + nazik yanıt); kullanıcıya teknik hata gösterilmiyor.

## Assumptions

- Mevcut MCP tabanlı erişim (agent yalnız MCP konuşur, İlke I), query-only tool'lar ve downstream
  yetki modeli **korunur** — bu feature yalnız agent-içi yapıyı (tek düz → router+sub-agent) değiştirir.
- Mevcut tool sonucu yakalama (CapturingFunction + ToolResultCollector → `items`) ve kimlik taşıma
  (bearer forward) desenleri yeniden kullanılır; BC tarafındaki MCP tool'ları değişmez.
- Niyet-dağıtımı için ek bir yapay zekâ turu kabul edilebilir bir maliyet/gecikme takasıdır; kazanç
  (seçici bağlanma + odaklı küçük bağlam + alan-başına model) bunu karşılar.
- Çok-adımlı deterministik orkestrasyon (workflow grafı) bu dilimde **kapsam dışı** — yönlendirme
  yapay zekâ-güdümlü (agent-as-tool); gerekirse ileride ayrı dilimde ele alınır.
- Dilim bulut modeliyle çalışır; local-LLM kuzey-yıldızı ayrı dilim.
