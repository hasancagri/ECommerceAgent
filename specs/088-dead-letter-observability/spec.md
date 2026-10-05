# Feature Specification: Dead-Letter İzleme / Replay / Alarm

**Feature Branch**: `088-dead-letter-observability`

**Created**: 2026-10-05

**Status**: Draft

**Input**: User description: "DLQ izleme/replay/alarm mekanizması — retry tükenince ölen mesajlar her BC'nin dead-letter tablosuna taşınıyor ama üstünde görünürlük/replay/alarm yüzeyi yok; ölü mesaj sessizce birikiyor, watchdog telafi ediyor."

## Clarifications

- Mesajlaşma altyapısı retry'ı tükettiğinde başarısız mesajı **her BC'nin kendi veritabanındaki ayrı dead-letter deposuna** taşır (BC izolasyonu; merkezi tek depo YOK). Depolama altyapıda zaten var; eksik olan onun üstündeki operatör yüzeyi.
- Operatör = platform admini; ölü mesajları yalnızca admin yetkisiyle görür/yönetir.
- Ham mesaj gövdesi (içinde sır/PII olabilir) operatör yüzeyine DÖNDÜRÜLMEZ — yalnız metadata + hata bilgisi.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ölü mesaj sessizce kaybolmaz (Priority: P1)

Bir işleme (ör. sipariş onay event'i) tekrar denemeler tükendiği halde başarısız olursa, mesaj
atılmaz; kalıcı olarak ölü-mesaj deposuna düşer ve sonradan incelenebilir/işlenebilir durumda kalır.

**Why this priority**: Diğer tüm hikayeler bunun üstüne kurulur. Yakalama garantisi olmadan izleme/replay
boşlukta kalır. Bugünkü asıl risk: sessiz kayıp.

**Independent Test**: Kasıtlı her denemede hata fırlatan bir handler'a mesaj gönder; retry bütçesi
bitince mesajın ölü-mesaj deposunda (ilgili BC) metadata+hata ile durduğunu doğrula.

**Acceptance Scenarios**:

1. **Given** bir handler her denemede hata fırlatıyor, **When** retry bütçesi tükenir, **Then** mesaj
   ilgili BC'nin ölü-mesaj deposuna hata tipi/mesajı + zaman ile yazılır, sessizce atılmaz.
2. **Given** aynı davranış tüm servislerde, **When** herhangi bir servis handler'ı kalıcı başarısız olur,
   **Then** yakalama davranışı tek-tek exception tipine bağlı olmadan **uniform** çalışır.

---

### User Story 2 - Admin ölü mesajları görür (Priority: P1)

Admin, kendi AI istemcisiyle platform yüzeyine bağlanıp hangi mesajların öldüğünü, hangi tipte, hangi
hatayla ve ne zaman olduğunu listeler ve tek bir ölü mesajın detayına bakar.

**Why this priority**: Birikmeyi görünür kılar. Operatörün ilk ihtiyacı "ne öldü, neden" — replay'den önce
gelir.

**Independent Test**: Deposu birkaç ölü mesaj içeren bir servisde listeleme yüzeyini çağır; her satırda
tip/hata/zaman döndüğünü, ham gövde dönmediğini doğrula; bir id ile detay çağır, hata ayrıntısını gör.

**Acceptance Scenarios**:

1. **Given** bir serviste ölü mesajlar var, **When** admin listeleme yüzeyini çağırır (tip/zaman/limit
   süzgeçleriyle), **Then** her kayıt için mesaj tipi, hata özeti ve ölüm zamanı döner.
2. **Given** admin bir ölü mesajın id'sini verir, **When** detay yüzeyini çağırır, **Then** tam hata tipi
   + hata mesajı + zaman damgaları döner, **ham mesaj gövdesi DÖNMEZ**.
3. **Given** admin olmayan bir çağıran, **When** yüzeyi çağırmaya çalışır, **Then** yetki reddi alır.
4. **Given** birden çok BC'de ölü mesaj var, **When** admin tek bağlantıdan bakar, **Then** her BC'nin
   yüzeyi ayrı görünür (depolar izole, tek listede birleşmez) ama hepsine aynı oturumdan erişilir.

---

### User Story 3 - Admin ölü mesajı replay'ler veya atar (Priority: P2)

Kök neden giderildikten sonra admin bir ölü mesajı yeniden işleme sokar; artık geçersiz/çöp bir mesajı
ise atar. Her iki işlem de admin eylem izine yazılır.

**Why this priority**: Görünürlükten sonraki doğal adım; kuyruğu boşaltmanın tek güvenli yolu. İzlemeden
sonra geldiği için P2.

**Independent Test**: Deposundaki bir ölü mesajı replay yüzeyiyle işaretle; mesajın yeniden işlendiğini
(başarılıysa depodan düştüğünü) doğrula. Ayrı bir mesajı discard yüzeyiyle at; depodan silindiğini + admin
izinde kayıt oluştuğunu doğrula.

**Acceptance Scenarios**:

1. **Given** bir ölü mesaj + giderilmiş kök neden, **When** admin replay'ler, **Then** mesaj yeniden
   işlenmek üzere işaretlenir ve başarılı işlenince ölü-mesaj deposundan düşer.
2. **Given** bir geçersiz ölü mesaj, **When** admin atar (discard), **Then** mesaj depodan kalıcı silinir.
3. **Given** herhangi bir replay/discard, **When** işlem gerçekleşir, **Then** ilgili BC'nin admin eylem
   izine kim/ne/ne zaman kaydı append edilir.

---

### User Story 4 - Birikme alarmı (Priority: P3)

Ölü mesaj oluştukça sistem her birini kaydeder (log) ve sayar (metrik); belirli bir eşik aşıldığında
admine proaktif e-posta gider, böylece operatör dashboard'a bakmadan da birikmeyi fark eder.

**Why this priority**: Değer katar ama P1/P2 olmadan anlamsız. Log+metrik tabanı ucuz ve her zaman açık;
mail eşiği ek konfor.

**Independent Test**: Eşiği düşük ayarla; eşik sayıda ölü mesaj üret; (a) her ölümde Error log + metrik
sayacının arttığını, (b) eşik aşılınca admine tek (throttle'lı) bildirim gittiğini doğrula.

**Acceptance Scenarios**:

1. **Given** bir mesaj ölür, **When** olay gerçekleşir, **Then** yapısal bir Error log satırı yazılır ve
   ölü-mesaj metrik sayacı (mesaj tipi etiketiyle) artar.
2. **Given** ölü mesaj sayısı pencere içinde eşiği aşar, **When** eşik aşılır, **Then** admine tek bir
   bildirim e-postası gider; pencere/throttle süresi dolmadan tekrar mail atılmaz.

---

### Edge Cases

- **Zaten replayable işaretli mesaj**: tekrar replay no-op / idempotent; çift işleme yok.
- **Replay ettikten sonra yine ölürse**: mesaj tekrar ölü-mesaj deposuna düşer (döngü), sayaç artar; sonsuz
  otomatik retry YOK (replay elle tetiklenir).
- **Alarm maili gönderilemezse** (SMTP/compose hatası): log+metrik tabanı bozulmaz; mail best-effort, hatası
  mesaj işlemeyi etkilemez.
- **Boş depo**: listeleme boş sonuç döner (hata değil).
- **Çok sayıda ölü mesaj**: listeleme limit/sayfalama ile sınırlı; sınırsız dump yok.
- **Bilinmeyen id ile detay/replay/discard**: açık "bulunamadı" sonucu.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, retry bütçesi tükenen her mesajı — tek-tek exception tipinden bağımsız, **tüm
  servislerde uniform** — ilgili BC'nin kalıcı ölü-mesaj deposuna taşımalı (sessiz kayıp yasak).
- **FR-002**: Admin, bir servisin ölü mesajlarını mesaj tipi / zaman aralığı / adet-limiti süzgeçleriyle
  listeleyebilmeli; her kayıt mesaj tipi, hata özeti ve ölüm zamanı içermeli.
- **FR-003**: Admin, bir ölü mesajın id'siyle detayını (hata tipi + hata mesajı + zaman damgaları)
  görebilmeli.
- **FR-004**: Sistem, ölü mesajın **ham gövdesini** (sır/PII içerebilir) hiçbir operatör yüzeyine VEYA
  log/trace'e döndürmemeli — yalnız metadata + hata bilgisi.
- **FR-005**: Admin, bir ölü mesajı yeniden işlenmek üzere replay'leyebilmeli; başarılı işlenince mesaj
  depodan düşmeli.
- **FR-006**: Admin, bir ölü mesajı kalıcı atabilmeli (discard).
- **FR-007**: Her replay ve discard, ilgili BC'nin admin eylem izine (kim/ne/ne zaman) append edilmeli.
- **FR-008**: Ölü-mesaj yönetim yüzeyinin tamamı admin yetkisi gerektirmeli; yetkisiz çağrı reddedilmeli.
- **FR-009**: Her ölü mesaj olayında sistem yapısal bir Error log satırı yazmalı ve mesaj tipi etiketli bir
  metrik sayacı artırmalı.
- **FR-010**: Ölü mesaj sayısı yapılandırılabilir bir eşiği (bir zaman penceresinde) aştığında sistem admine
  bir bildirim e-postası göndermeli; aynı pencerede throttle ile tek mail (spam yok).
- **FR-011**: Alarm e-postası best-effort olmalı; gönderilememesi mesaj işlemeyi veya log/metrik tabanını
  etkilememeli.
- **FR-012**: Ölü-mesaj depoları BC-izole kalmalı — merkezi tek depo/tablo oluşturulmamalı; admin birden
  çok BC'ye tek oturumdan erişse de depolar ayrı listelenmeli.

### Key Entities

- **Ölü mesaj (dead-letter envelope)**: Kalıcı başarısız olmuş bir mesajın kaydı. Nitelikler: benzersiz id,
  mesaj tipi, kaynak, ölüm/alınma zamanı, hata tipi, hata mesajı, "yeniden-işlenebilir" bayrağı, (yüzeye
  dönmeyen) serileştirilmiş gövde. Her BC'nin kendi deposunda yaşar.
- **Admin eylem izi**: Replay/discard gibi yönetim işlemlerinin kim/ne/ne zaman salt-append kaydı (her BC'de
  mevcut yapı yeniden kullanılır).
- **Birikme alarmı sinyali**: Eşik aşımını temsil eden, bildirim üretimini tetikleyen olay (mesaj tipi,
  sayı, zaman).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Retry tükenen mesajların **%100'ü** ilgili BC'nin ölü-mesaj deposunda bulunur; sessizce
  kaybolan mesaj sayısı sıfır (kasıtlı-hata testiyle doğrulanır).
- **SC-002**: Admin, bir ölü mesajın varlığından detayını görmeye **tek yüzey çağrısıyla** ulaşır; birikmeyi
  görmek için veritabanına elle SQL atmaya gerek kalmaz.
- **SC-003**: Kök neden giderildikten sonra admin bir ölü mesajı **elle veritabanı müdahalesi olmadan**
  yeniden işleme sokar.
- **SC-004**: Ölü mesaj oluştuğunda operatör, dashboard sayacından veya (eşik aşımında) e-postadan
  **dakikalar içinde** haberdar olur; fark etme artık watchdog/tesadüfe bağlı değil.
- **SC-005**: Hiçbir operatör yüzeyi veya log kaydı ham mesaj gövdesi / sır / PII sızdırmaz (inceleme ile
  doğrulanır).

## Assumptions

- Ölü-mesaj depolama altyapıda zaten mevcut (dayanıklı mesaj deposu); bu feature yalnız üstüne yakalama
  garantisi + operatör yüzeyi + gözlemlenebilirlik + alarm ekler, yeni depo teknolojisi getirmez.
- Operatör yüzeyi mevcut admin erişim modeline (yeni bir ops yetkisi/scope) ve platform MCP yüzeyine
  eklenir; ayrı bir görsel panel bu sürümde kapsam dışı.
- Alarm e-postası mevcut bildirim/mail boru hattını yeniden kullanır; yeni mail altyapısı kurulmaz.
- Eşik ve zaman penceresi yapılandırılabilir; varsayılan değerler dev/prod için makul seçilir.
- Kapsam = bu repodaki 12 BC servisi + paylaşılan ortak katman + bildirim worker'ı + Aspire host.
- **İlgili iş (bu spec DIŞI):** PaymentGateway ayrı repo ve aynı mesajlaşma/kalıcılık stack'ini (Wolverine +
  Marten) kullanıyor; aynı desen orada kendi spec'iyle birebir uygulanacak. Bu spec yalnız ECommerceAgent'ı
  kapsar.
- Replay sonrası tekrar ölen mesaj için otomatik yeniden deneme yoktur; replay daima elle tetiklenir.

## Dependencies

- Platform IdP'de yeni bir admin ops yetkisi tanımı (ölü-mesaj yönetimi için).
- Mevcut admin eylem izi (replay/discard kaydı için) ve mevcut bildirim/mail worker'ı (alarm maili için).
- AgentPlatform MCP fasadı, BC başına ölü-mesaj yüzeylerini birleştirip admine sunar.