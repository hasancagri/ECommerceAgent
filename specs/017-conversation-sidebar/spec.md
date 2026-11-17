# Feature Specification: Sohbet Kayıt Sadakati + Kalıcı Sidebar

**Feature Branch**: `017-conversation-sidebar`

**Created**: 2026-11-17

**Status**: Draft

**Input**: Tasarım belgesi `docs/superpowers/specs/2026-11-17-scout-conversation-persistence-sidebar-design.md`

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sohbet başından sonuna aynen saklanır (Priority: P1)

Girişli kullanıcı Kâşif ile kitap konuşur, kitap kartları görür. Daha sonra o sohbeti tekrar
açtığında yazışma **gördüğü haliyle** gelir — metin **ve** kitap kartları. Konuşmanın başı da
sonu da eksiksiz; kaybolan bir şey yok.

**Why this priority**: Çekirdek değer — "yazışma sürecini başından sonuna kayıt altına almak".
Bugün metin kayıtlı ama kartlar reload'da kayboluyor; kullanıcı eksik bir geçmiş görüyor.

**Independent Test**: Login ol, kitap ara (kart dönen bir soru sor), sohbeti kapat, tekrar aç →
kartlar metinle birlikte aynen görünüyor mu.

**Acceptance Scenarios**:

1. **Given** girişli kullanıcı kart dönen bir soru sordu, **When** sohbeti tekrar açar, **Then**
   assistant cevabı metin + aynı kitap kartlarıyla render edilir.
2. **Given** kart dönmeyen (düz metin) bir tur, **When** sohbet tekrar açılır, **Then** yalnız
   metin görünür, kart alanı boş/yok.
3. **Given** uzun bir sohbet, **When** yeni mesaj gönderilir, **Then** modelin gördüğü bağlam
   yalnız metindir (kartlar bağlama girmez) ve sınırlıdır — cevap kalitesi/maliyeti bozulmaz.

---

### User Story 2 - Geçmiş sohbetler kalıcı sol panelde yaşar (Priority: P1)

Kullanıcı hangi ekranda olursa olsun (keşif, kitaplık, sepet, hesap) sol panelde sohbet
listesini görür; tıklayıp kaldığı yerden sürdürür, yeni sohbet başlatır. Geçmiş artık ayrı bir
sayfaya gitmeyi gerektirmez.

**Why this priority**: İstenen claude.ai-tarzı "sohbet açılıp orada yaşıyor" hissi. Ayrı geçmiş
sayfası bu sürekliliği vermiyor.

**Independent Test**: Farklı rotalara git; sol panelde sohbet listesi her yerde duruyor mu, bir
sohbete tıklayınca sürdürülüyor mu.

**Acceptance Scenarios**:

1. **Given** girişli kullanıcının sohbetleri var, **When** herhangi bir ekranı açar, **Then** sol
   panelde sohbet listesi görünür (en son etkinlik üstte).
2. **Given** listeden bir sohbete tıklar, **When** sohbet açılır, **Then** tam transkript (kartlar
   dahil) yüklenir ve kaldığı yerden yazabilir.
3. **Given** "Yeni sohbet"e basar, **When** ilk mesajı gönderir, **Then** yeni sohbet listede
   belirir ve aktif olur.

---

### User Story 3 - Sohbeti yeniden adlandır / sil (Priority: P2)

Kullanıcı bir sohbetin başlığını kendi istediğiyle değiştirir ya da sohbeti kalıcı siler. Her
satırda bu aksiyonlar erişilir.

**Why this priority**: Geçmişi düzenli tutma. Çekirdek kayıt/görünürlükten sonra gelir.

**Independent Test**: Bir sohbeti yeniden adlandır (liste güncellenir), başka birini sil (listeden
ve DB'den kalkar).

**Acceptance Scenarios**:

1. **Given** bir sohbet, **When** kullanıcı başlığı geçerli bir metinle değiştirir, **Then** yeni
   başlık listede görünür ve sohbetin listedeki sırası **değişmez**.
2. **Given** boş/yalnız-boşluk başlık, **When** kaydetmeye çalışır, **Then** reddedilir, eski
   başlık korunur.
3. **Given** bir sohbet silinir, **When** silme onaylanır, **Then** sohbet listeden ve kalıcı
   kayıttan kalkar (geri alınamaz).
4. **Given** silinen sohbet **o an açık** olandır, **When** silinir, **Then** ekran temiz
   yeni-sohbet durumuna döner; sonraki mesaj yanlışlıkla eski sohbete bağlanmaz.
5. **Given** başka kullanıcının sohbeti, **When** erişim/değişiklik denenir, **Then** izin yok
   (varlık sızmaz).

---

### User Story 4 - Girişsiz sohbetler oturum boyunca panelde (Priority: P3)

Girişsiz kullanıcı da o oturumda açtığı sohbetleri sol panelde görür, aralarında geçiş yapar,
adlandırır/siler. Sekme kapanınca bunlar kaybolur (kalıcı değil).

**Why this priority**: Giriş yapmadan deneyim sürekliliği; ama geçici doğası gereği en düşük
öncelik.

**Independent Test**: Girişsiz iki sohbet aç, panelde ikisi de görünür mü, geçiş/sil çalışıyor mu;
sekmeyi kapat-aç → gittiler mi.

**Acceptance Scenarios**:

1. **Given** girişsiz kullanıcı iki sohbet açtı, **When** panele bakar, **Then** ikisi de listelenir.
2. **Given** girişsiz bir sohbeti siler/adlandırır, **When** işlemi yapar, **Then** yalnız yerel
   oturumda etkilenir; sunucuya yazılmaz.
3. **Given** girişsiz oturum, **When** sekme kapanır, **Then** sohbetler kalıcı değildir.

---

### Edge Cases

- Açık sohbet silinince sonraki mesaj: eski silinmiş kimliğe değil, yeni sohbete gider (temiz durum).
- Çok uzun sohbet: tam transkript saklanır ama modele giden bağlam sınırlı kalır (maliyet/kalite guard).
- Başkasının sohbet kimliğiyle erişim/sil/adlandırma: reddedilir, varlık varlığı sızmaz.
- Kart dönmeyen turlar: kart alanı boş; reload bozulmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, girişli bir sohbette assistant cevabının **gördüğü yapılı sonuçları (kitap
  kartları)** metinle birlikte kalıcı saklamalı.
- **FR-002**: Sistem, saklanan sohbeti tekrar açışta metni **ve** kartları **aynen** sunmalı.
- **FR-003**: Modele gönderilen bağlam yalnız **metin** olmalı ve **sınırlı** tutulmalı; yapılı
  sonuçlar bağlama girmemeli.
- **FR-004**: Sistem, girişli kullanıcının sohbet listesini **her ekranda** kalıcı bir sol panelde
  göstermeli (en son etkinlik üstte).
- **FR-005**: Kullanıcı, listeden bir sohbeti açıp kaldığı yerden sürdürebilmeli ve yeni sohbet
  başlatabilmeli.
- **FR-006**: Kullanıcı, bir sohbetin başlığını değiştirebilmeli; boş/yalnız-boşluk başlık
  reddedilmeli; yeniden adlandırma sohbetin **sıralamasını değiştirmemeli**.
- **FR-007**: Kullanıcı, bir sohbeti **kalıcı silebilmeli** (geri alınamaz); yalnız kendi sohbetini.
- **FR-008**: Açık olan sohbet silinirse ekran temiz yeni-sohbet durumuna dönmeli; sonraki mesaj
  silinen sohbete bağlanmamalı.
- **FR-009**: Sohbet erişimi/değişikliği/silme yalnız **sahibine** açık olmalı; başkasının sohbeti
  için varlık sızmamalı.
- **FR-010**: Eski ayrı sohbet-geçmişi sayfası kaldırılmalı; arama-geçmişi görünümü hesap/ayarlar
  alanına taşınmalı (işlevi korunarak); ölü bağlantı kalmamalı.
- **FR-011**: Girişsiz kullanıcının sohbetleri oturum boyunca panelde görünmeli; adlandır/sil yerel
  olmalı; oturum bitince kalıcı olmamalı.

### Key Entities *(include if feature involves data)*

- **Sohbet (Conversation)**: Bir kullanıcıya ait yazışma turu dizisi + başlık. Girişli = kalıcı;
  girişsiz = oturumluk.
- **Mesaj (ChatMessage)**: Sohbetteki tek tur — rol (kullanıcı/kâşif), metin, zaman, ve assistant
  turunda opsiyonel **yapılı sonuçlar (kartlar)**.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Girişli bir sohbet tekrar açıldığında, orijinalde görünen kartların **%100'ü** yeniden
  görünür (kart kaybı sıfır).
- **SC-002**: Sohbet listesi kullanıcının bulunduğu **her ekranda** erişilebilir (ayrı sayfaya
  gitmeden 0 ek adım).
- **SC-003**: Yeniden adlandırma sonrası sohbetin listedeki konumu değişmez (sıra kaymaz).
- **SC-004**: Silinen sohbet kalıcı olarak kaybolur; silme sonrası yeni mesaj her zaman doğru
  (yeni) sohbete gider — "yanlış sohbete bağlanma" olayı 0.
- **SC-005**: Uzun sohbette yeni tur, bağlam sınırı sabit kaldığından kart-saklama öncesiyle aynı
  maliyet/kalite aralığında kalır (bağlam şişmesi 0).

## Assumptions

- Kalıcı kayıt yalnız **girişli** kullanıcı için; girişsiz oturumluk (mevcut tasarım korunur).
- Kimlik JWT `sub`'tan çözülür; kişisel veri — named-scope gerekmez (013 deseni).
- Kalıcı panel mevcut sol navigasyon alanına yerleşir; ayrı bir kabuk gerekmez (tasarım kararı).
- Arama-geçmişi (013) backend'i değişmez; yalnız görünüm hesap/ayarlar alanına taşınır.
- Sohbet başlığı için makul bir üst karakter sınırı uygulanır (uygulama kararı).
