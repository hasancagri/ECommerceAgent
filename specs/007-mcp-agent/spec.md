# Feature Specification: MCP Query-Only Dönüşü + ScoutAgent (Kâşif Sohbeti)

**Feature Branch**: `007-mcp-agent`

**Created**: 2026-11-06

**Status**: Draft

**Input**: User description: "BC'lere query-only MCP tool yüzeyleri geri gelir (Catalog arama/detay anonim; Customer adreslerim, Library kitaplığım, Order siparişlerim scope-korumalı — basit tek-niyetli okuma, kompleks kriter + yazma KAPSAM DIŞI). Yeni first-party agent servisi ScoutAgent (MAF + Ollama): React chat'ten serbest metni alır, hangi MCP tool'una gideceğini LLM tool-seçimiyle anlar, cevabı sohbet olarak döner. Kullanıcı token'ı çağrı anında enjekte edilir. Dış BYO-agent/Claude Desktop yüzeyi YOK. React DiscoverWelcome kartları + chat kutusu bu agent'a bağlanır (genel kartlar commit'li TS config)."

**Kademe**: Tam — yeni servis (ScoutAgent) + BC'lere yeni MCP yüzeyi (yeni kontrat) + React entegrasyonu. MCP dönüşü CLAUDE.md "Yapma listesi" gereği bilinçli spec'le oluyor: budur.

**Kapsam notu**: MCP YALNIZ first-party ScoutAgent'a hizmet eder — dış agent/DCR/PRM/consent yüzeyi GERİ GELMEZ (temiz-sayfa çizgisi korunur). Tool'lar query-only; yazma MCP'ye girmez.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sohbetten kitap sorgusu (anonim) (Priority: P1)

Ziyaretçi (girişsiz) Kâşif chat kutusuna "Nutuk var mı?" ya da "Sabahattin Ali kitapları" yazar; Kâşif katalogda arar ve bulduğu kitapları (ad, yazar, fiyat, kapak) sohbet cevabı olarak listeler. Kitap detayına tıklanabilir.

**Why this priority**: Sitenin kalbi = sohbetle keşif; anonim çalışan en dar dilim bu. Login'siz test edilebilir.

**Independent Test**: Sistem ayakta; girişsiz kullanıcı chat'e kitap/yazar adı yazar → doğru kitaplar döner; katalogda olmayan şey sorulunca dürüst "bulunamadı" cevabı gelir.

**Acceptance Scenarios**:

1. **Given** girişsiz ziyaretçi, **When** "Simyacı var mı?" yazar, **Then** Kâşif katalog aramasını çağırır ve eşleşen kitapları künyeleriyle döner.
2. **Given** girişsiz ziyaretçi, **When** katalogda olmayan bir kitap sorar, **Then** "bulunamadı" anlamında dürüst cevap gelir — uydurma kitap listelenmez.
3. **Given** ziyaretçi alakasız bir şey yazar ("hava nasıl?"), **Then** Kâşif kapsamını nazikçe söyler, tool çağırmaz.

---

### User Story 2 - Kişisel verini sohbetten isteme (Priority: P1)

Girişli kullanıcı "adres bilgilerimi getirir misin", "siparişlerim ne durumda", "kitaplığımda neler var" yazar; Kâşif doğru servise gidip KULLANICININ KENDİ verisini getirir. Girişsiz kullanıcı aynı soruyu sorarsa giriş yapması gerektiği söylenir.

**Why this priority**: "Metinden doğru MCP'yi bulma" hedefinin asıl kanıtı — üç farklı BC'ye yönlendirme + kimlik taşıma.

**Independent Test**: Girişli kullanıcı üç soruyu sorar (adres/sipariş/kitaplık) → her biri doğru BC'den kendi verisini döner; başka kullanıcının verisi asla görünmez.

**Acceptance Scenarios**:

1. **Given** girişli, adres defterinde 2 adresi olan kullanıcı, **When** "adreslerimi göster" yazar, **Then** kendi 2 adresi okunur biçimde döner (iç kimlikler/GUID gösterilmez).
2. **Given** girişli kullanıcı, **When** "kitaplığımda neler var" yazar, **Then** Library'deki kendi kayıtları döner.
3. **Given** girişli kullanıcı, **When** "siparişlerim ne durumda" yazar, **Then** kendi siparişleri durumlarıyla döner.
4. **Given** GİRİŞSİZ ziyaretçi, **When** "adreslerimi göster" yazar, **Then** veri DÖNMEZ; giriş yapması gerektiği söylenir (giriş yolu gösterilir).
5. **Given** girişli kullanıcı, **When** verisi boşsa (hiç sipariş yok), **Then** dürüst boş-durum cevabı gelir.

---

### User Story 3 - Keşif kartları sohbeti başlatır (Priority: P2)

DiscoverWelcome'daki genel kartlardan birine ("Kâşif'e sor") tıklanınca kartın sorusu chat'e yazılmış olarak sohbet başlar. Bugün cevaplanabilen kartlar aktif; motor bekleyenler (ruh hali, benzer kitap) "yakında" durumunda görünür.

**Why this priority**: Kartlar = sohbetin tanıtım vitrini; US1/US2 çalışmadan kartın gideceği yer yok.

**Acceptance Scenarios**:

1. **Given** ana sayfadaki aktif kart, **When** tıklanır, **Then** chat ekranı kartın sorusuyla açılır ve cevap akışı başlar.
2. **Given** "yakında" kartı, **When** görüntülenir, **Then** pasif/rozetli görünür, tıklanınca sohbet başlatmaz.

---

### User Story 4 - Sohbet geçmişi (Priority: P2)

Girişli kullanıcının sohbetleri kaydedilir; "Sohbet Geçmişi" sayfasında geçmiş sohbetlerini listeler, birini açıp okuyabilir ve kaldığı yerden DEVAM edebilir. Anonim ziyaretçinin kalıcı geçmişi olmaz (yalnız o oturumda hatırlanır).

**Why this priority**: Mock'taki ChatHistory sayfasının gerçeğe bağlanması; devamlılık olmadan Kâşif her mesajda hafızasız bir araca düşer.

**Independent Test**: Girişli kullanıcı iki ayrı sohbet yapar → geçmiş sayfasında ikisi de listelenir; biri açılıp "az önce söylediğim yazar kimdi?" sorulunca doğru cevap gelir.

**Acceptance Scenarios**:

1. **Given** girişli kullanıcı sohbet etti, **When** Sohbet Geçmişi sayfasını açar, **Then** sohbetleri (başlık/tarih) listelenir — yalnız KENDİ sohbetleri.
2. **Given** geçmişten açılan sohbet, **When** kullanıcı yeni mesaj yazar, **Then** Kâşif önceki bağlamı hatırlayarak devam eder.
3. **Given** uzun sohbet (bağlam sığmaz), **When** kullanıcı eski bir detaya atıf yapar, **Then** Kâşif damıtılmış sohbet hafızası üzerinden tutarlı cevap verir (en azından sohbetin ana konusunu kaybetmez).
4. **Given** anonim ziyaretçi, **When** sohbet eder, **Then** aynı oturumda bağlam sürer; oturum bitince geçmiş saklanmaz.

---

### Edge Cases

- Sohbet beyni (yerel model) kapalı/ulaşılamazsa: chat anlaşılır "şu an hizmet veremiyorum" döner; site geri kalanı etkilenmez.
- LLM yanlış tool seçerse/parametre uyduramazsa: tool sonucu boş/hatalıysa Kâşif dürüstçe söyler; asla veri uydurmaz (tool dönüşü = tek gerçek).
- Kullanıcının token'ı sohbet ortasında süresi dolarsa: kişisel sorular yeniden giriş ister; anonim sorular çalışmaya devam eder.
- Aynı mesajda iki niyet ("Nutuk'u bul ve adreslerimi göster"): v1'de sırayla iki tool çağrısı meşru; olmuyorsa tek niyete netleştirme sorusu sorulur.
- Uzun sonuç listeleri: sohbet cevabı kısaltılır (ilk N + "devamı için"), ham dökümanlar basılmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Dört serviste query-only sohbet tool'ları açılır: katalog kitap arama + kitap detay (anonim); adreslerim (Customer), kitaplığım (Library), siparişlerim (Order) — yalnız çağıran kullanıcının verisi.
- **FR-002**: Yazma/az-admin/kompleks kriter tool'u AÇILMAZ; tool seti kapalı listedir, bu spec'tekilerle sınırlıdır.
- **FR-003**: Kullanıcı-bağlı tool'lar kimlik ve yetkiyle korunur (İlke V: scope); kimliksiz çağrı veri döndürmez. Kullanıcı yalnız KENDİ verisini görür.
- **FR-004**: ScoutAgent serbest metni alır, niyeti anlar, uygun tool'u LLM tool-seçimiyle çağırır, sonucu sohbet diliyle döner. Tool'un döndürmediği hiçbir veri cevapta yer almaz (uydurma yasak).
- **FR-005**: ScoutAgent çağrı anında kullanıcının kimliğini taşır (oturum başına sabit agent durumu tutulmaz). Girişsiz kullanıcının kişisel veriye erişimi İKİ KATMANDA engellenir: (a) girişsiz oturumda kişisel tool'lar agent'ın araç kutusuna HİÇ konmaz (model çağıramaz, "giriş gerekli" der); (b) tool yine de sunucuda scope ister (İlke V) — token yoksa/bayatsa reddeder. (a) deneyim, (b) gerçek güvenlik sınırı; girişsiz hiçbir koşulda kişisel veri DÖNMEZ.
- **FR-005a**: ScoutAgent MCP tool'larına her BC'nin kendi MCP ucuna DOĞRUDAN bağlanarak ulaşır (uç listesi service discovery'den statik). Araya tool-toplayan/keşfeden fasat ya da gateway katmanı KONMAZ; dinamik downstream keşfi / discovery-token dansı YOK (eski MCP fasat makinesi geri gelmez).
- **FR-006**: Cevap iki parçadan oluşur: Kâşif'in sohbet cümlesi (model üretir) + yapılı öğeler (kitap/adres/sipariş — tool dönüşünden BİREBİR). React yapılı öğeleri gerçek kart olarak çizer (kapak tıklanabilir). Fiyat/kapak/künye modelin metninden değil tool verisinden gelir; iç kimlikler (GUID) kullanıcıya gösterilmez.
- **FR-007**: React chat yüzeyi: giriş kutusu + cevap akışı + kart tıklamasıyla önceden doldurulmuş soru. Genel kartlar commit'li yapılandırma dosyasından okunur (DB değil); her kartın aktif/yakında durumu vardır.
- **FR-008**: Sohbet beyni erişilemezse kullanıcıya anlaşılır hata döner; sistemin geri kalanı (gezinme, login, listeler) etkilenmez.
- **FR-009**: Dış agent erişim yüzeyi (keşif/kayıt/consent) AÇILMAZ; sohbet tool yüzeyi yalnız first-party ScoutAgent'ın erişimine kapalıdır.
- **FR-010**: Girişli kullanıcının sohbetleri kalıcı saklanır (ScoutAgent'ın KENDİ verisi, kendi deposunda); kullanıcı geçmişini listeler, açar, devam eder. Başka kullanıcının sohbeti asla görünmez. Anonim sohbet kalıcı saklanmaz.
- **FR-011**: Sohbet hafızası katmanlı yönetilir: TAM transkript depoda durur (kayıp yok); modele giden bağlam = yakın mesajlar + eskilerin damıtılmış özeti — sohbet uzadıkça bağlam maliyeti sabit kalır. Hacimli tool sonuçları bağlam geçmişine ham haliyle taşınmaz.
- **FR-012**: Geçmişten açılan sohbette devamlılık ScoutAgent'ın KENDİ DB kaydından kurulur — agent çerçevesinin conversation/previous-response state'ine GÜVENİLMEZ (write-only olduğu bilinen tuzak); her çağrıda bağlam (özet + yakın mesajlar) kayıttan okunup modele verilir.
- **FR-013**: Cevap v1'de tek parça döner (akış/stream yok); kullanıcı bekleme sırasında "düşünüyor" göstergesi görür. Token-token streaming (yazılıyor efekti) sonraki dilim.

### Key Entities

- **Sohbet (Conversation)**: girişli kullanıcıya ait; başlık (ilk sorudan türer) + mesaj dizisi (kullanıcı/Kâşif sırayla) + damıtılmış özet bloğu + zaman damgaları. ScoutAgent'ın kendi deposunda.
- **Öneri kartı (genel)**: etiket + soru metni + durum (aktif/yakında); commit'li yapılandırma.
- BC verileri değişmez — tool'lar mevcut verileri yalnız okur; tek yeni kalıcı veri sohbet kaydıdır (ScoutAgent'da).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: "Adreslerimi getir / siparişlerim / kitaplığım / kitap ara" niyetlerinin her biri, 10 farklı doğal ifadeyle denendiğinde ≥9'unda doğru servise yönlenir.
- **SC-002**: Girişsiz kullanıcının kişisel veri sorusu %100 veri DÖNDÜRMEZ (giriş daveti döner); kullanıcı yalnız kendi verisini görür (%100).
- **SC-003**: Basit sorunun sohbet cevabı tipik olarak 10 saniyeden kısa sürede gelir (yerel model dahil).
- **SC-004**: Kâşif'in cevabındaki her kitap/adres/sipariş bilgisi tool dönüşünde birebir vardır (uydurma 0).
- **SC-005**: 30+ mesajlık sohbette Kâşif sohbetin ana konusunu kaybetmez (ör. baştaki yazar atfına tutarlı cevap) ve cevap süresi kısa sohbetle aynı mertebede kalır.
- **SC-006**: Geçmişten açılan sohbette ilk devam mesajı, önceki bağlama uygun cevaplanır (10 denemede ≥9).

## Assumptions

- Sohbet beyni = yerel model (mevcut makinedeki Ollama); bulut LLM kullanılmaz. Model seçimi plan aşamasında (tool-calling destekli olmalı).
- Agent çerçevesi = Microsoft Agent Framework (kullanıcı kararı, 2026-11-06); MAF'ın MCP tool entegrasyonu kullanılır.
- ScoutAgent ayrı servis olarak Aspire'da koşar; React'ın tek origin kuralı gereği Gateway üzerinden erişilir.
- Kimlik taşıma: React'taki mevcut oturum token'ı sohbet isteğiyle gelir; agent tool çağrısına aynı kimliği iletir ("agent Singleton, token çağrı anında" kuralı).
- Tool cevapları mevcut query yollarını yeniden kullanır ya da BC içinde yeni ince query slice'ı açar (`Features/Agents/Queries` deseni geri yaşar); aggregate davranışı değişmez.
- 094'te "giriş yüzeyi MCP'yle söküldü, REST first-party'de açılacak" denen okuma yüzeyleri bu spec'le SOHBET kanalından dönüyor; ekran (REST) yüzeyi ayrı dilim (Catalog read API) olarak sürer.
- Ekran tarafı listeleme/detay REST API'si bu spec'in DIŞINDA (ayrı dilim); kartların "liste aç" aksiyonları o dilim gelene dek sohbet aksiyonuyla çalışır.
- Hafıza stratejisi (plan girdisi): pencere (son N mesaj ham) + rolling summary (eskiler damıtılır) + tam arşiv depoda; yerel modelin bağlam penceresi modelin kaldırdığı kadar geniş açılır (Ollama num_ctx — varsayılan küçüktür). Sohbetler-arası anlamsal hatırlama (RAG) KAPSAM DIŞI — pgvector/embedding spec'iyle gelir, arşiv bugünden onun ham maddesini biriktirir.
- MAF kalıntısı: `Order.Api` hâlâ `Microsoft.Agents.AI.A2A` paketine referans veriyor (024 taksit-quote artığı, kullanılmıyor). Plan aşamasında ya temizlenir ya ScoutAgent'ın MAF sürümüyle hizalanır — karar plan'da.
- Eski ChatAgent (2026-10-30 söküldü) MAF `MapOpenAIResponses` hosting'i + istemci-taşımalı transkript kullanıyordu; stream YOKTU. ScoutAgent o dersleri devralır (stateless-benzeri kurulum ama transkript artık kendi DB'sinde kalıcı).
