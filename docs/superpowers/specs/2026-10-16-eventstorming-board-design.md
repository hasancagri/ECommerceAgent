# EventStorming FLOW.md + Salt-Okunur Duvar — Tasarım

**Tarih:** 2026-10-16 · **Durum:** taslak (onay bekliyor) · Önceki sürüm (etkileşimli pano) İPTAL —
kullanıcı kararı: FLOW.md kalır, EventStorming diline çevrilir; görsel katman SONRA ve salt-okunur
(elle sürükle-bırak YOK).

## Niyet + neden

FLOW.md niyeti doğru ama serbest düzyazı — EventStorming'in dilini (turuncu event, mavi komut, mor
policy, kırmızı hotspot) taşımıyor; görselleştirilebilir yapısı yok. Çözüm iki faz:

1. **Faz 1 (bu iş):** 13 FLOW.md EventStorming notasyonuyla yeniden yazılır — hem insan hem Claude
   hem (ileride) çizici aynı dosyayı okur. Gerçek-kaynak FLOW.md KALIR; ikinci veri dosyası YOK.
2. **Faz 2 (sonra, ayrı iş):** FLOW.md'leri parse edip tüm sistemi TEK duvarda çizen salt-okunur
   görüntüleyici. Düzenleme yok; yerleşim otomatik (adım sırası = x, BC kulvarı = y). Bu dokümanda
   yalnız eskiz; kendi tasarımı Faz 1 bitince yazılır.

Kural tarafı: EventStorming iş yapısı sürece bağlanır (storm-önce-spec, hotspot kapısı, as-built) —
hepsi FLOW.md üstünde, yeni araç gerektirmeden.

## Hedefler / hedef-dışı / anti-hedef

**Hedefler (Faz 1):**
- EventStorming FLOW.md formatı tanımlanır (aşağıda) ve conventions.md İLKE VII bölümüne yazılır.
- 13 FLOW.md yeni formata çevrilir — mevcut anlatı/kural/sınır içeriği KAYIPSIZ taşınır.
- Guard (`check-flow-links.sh`) DEĞİŞMEDEN çalışmaya devam eder (anchor dili aynı: backtick).
- Süreç kuralları conventions.md'ye eklenir: storm-önce-spec, hotspot kapısı, as-built tetiği.

**Hedef-dışı (Faz 1):** görüntüleyici uygulama (Faz 2), yeni veri formatı (YAML vb.), guard yeniden
yazımı, CI değişikliği.

**Anti-hedef:** ikinci gerçek-kaynak doğurmak (FLOW.md TEK kaynak kalır); formatı makine için
insan-okunmaz hale getirmek (markdown akıcılığı korunur — sticky dili eklenir, düzyazı ölmez).

## FLOW.md EventStorming formatı

Mevcut iskelet korunur (BC ne yapar / Süreç / Domain kuralları / Sınır). Değişen: **Süreç** bölümü
sticky-tipli satırlara döner. Tip işareti = renkli kare emoji (GitHub/IDE/düz metin hepsinde görünür;
Faz 2 çizicisi bundan parse eder):

- 🟦 komut (mavi) · 🟧 domain/integration event (turuncu) · 🟪 policy (mor, "X olunca Y") ·
  🟨 aggregate (sarı) · 🟩 read-model (yeşil) · 🩷 dış sistem (pembe) · 🟥 hotspot (kırmızı) ·
  👤 aktör. Pivotal event: 🟧⭐.
- Satır grameri: `N. <emoji> **Ad** — tek cümle anlatı` + satır sonunda mevcut backtick kenar-anchor
  aynen (`(CheckoutProcess.Start → CheckoutTimedOut)`). Anlatı cümlesi ZORUNLU — hikâye kalitesi
  (örn. "stok yetersizliği İLK KEZ burada yakalanır") sticky adına kurban edilmez.
- Zincir aynı satırda akabilir: `🟦 CommitStockCommand → 🟧 StockCommitted` (EventStorming grameri:
  komut → aggregate → event → policy → komut).
- **Dikiş (BC-aşan event):** satır sonu ok notasyonu — yayıncıda `⇨ Reviews, Storefront, Library`
  (kimler tüketir), tüketicide `⇦ Order` (kim yayınlar). Yön AÇIK yazılır; Faz 2 dikişi buradan çizer.
- **Hotspot:** `🟥 Hotspot (open|closed): soru/çelişki` — Süreç içinde ilgili adımın altında ya da
  dosya sonunda ayrı `## Hotspot` bölümünde. Kapanınca silinmez, `(closed: cevap)` olur — karar izi.
- **Planned (storm-fazı) öğe:** henüz kodu olmayan sticky backtick'SİZ yazılır (+ `_(planned)_` eki) —
  guard yalnız backtick taradığından fail etmez; implement bitince backtick-anchor eklenir. Guard'a
  sıfır dokunuş.
- Örnek satır:
  `4. 🟦 **CommitStock** → 🟧 **StockCommitted** — kalem kalem düşüm (döngü); stok yetersizliği İLK KEZ burada yakalanır, telafiye geçer ` `(CommitStockCommand → StockCommitted)`

## Kural (conventions.md İLKE VII bölümü güncellenir)

- **Artefakt:** FLOW.md, EventStorming notasyonuyla (yukarıdaki format). Altitude aynı: domain-önce,
  ubiquitous dil, satır-numarası yok, teknoloji dökümü yok.
- **Storm-önce-spec:** önemsiz olmayan feature'da speckit-specify'dan ÖNCE mini-storm — ilgili FLOW.md'ye
  (veya spec klasörüne taslak storm bölümü) event'ler geçmiş zamanla dökülür, komut/policy/hotspot eklenir;
  Claude döker, kullanıcı düzeltir.
- **Hotspot kapısı:** open hotspot'lar clarify sorularının kaynağı; ilgili feature implement'e girmeden
  closed olur (cevap spec'e + hotspot satırına işlenir). Master'da open hotspot YAŞAYABİLİR — kapı süreç,
  CI değil.
- **As-built:** süreci değiştiren PR FLOW.md'yi AYNI PR'da günceller; planned öğenin eki düşer,
  backtick-anchor eklenir. Guard (değişmemiş `check-flow-links.sh`) rename/silme driftini yakalamaya
  devam eder.

## Taşıma planı (Faz 1 işi)

13 FLOW.md tek tek yeni formata çevrilir: her Süreç adımı sticky-tipli satıra, BC-aşan event'lere
⇨/⇦ dikiş notasyonu, mevcut anchor'lar aynen, kurallar/sınır bölümleri dokunulmadan. Çeviri Claude
tarafından; doğrulama = `scripts/check-flow-links.sh` yeşil (anchor seti değişmedi) + kullanıcı göz
kontrolü (önce 1 örnek BC — checkout — onaya gelir, format oturunca kalan 12 paralel çevrilir).

## Faz 2 eskizi (bağlayıcı değil)

Salt-okunur duvar: FLOW.md'leri parse eden küçük araç (tek HTML üreten script ya da minimal API +
statik sayfa) — BC kulvarları, emoji tipinden renkli sticky, adım sırasından soldan-sağa yerleşim,
⇨/⇦'den BC-arası dikiş okları, 🟥 hotspot vurgusu. Veri girişi yok, kaydetme yok, pozisyon verisi yok.
Format Faz 1'de buna göre tasarlandığı için parser basit kalır (satır başı numara + emoji + bold ad).

## Benim kararlarım (işaretli)

- Tip işareti olarak emoji (🟦🟧🟪…) seçtim — ayrı metin etiketi (`[komut]` vb.) yerine: markdown'da
  görsel, grep'lenebilir, parse basit. İtiraz edilirse etikete döner.
- Dikiş notasyonu ⇨/⇦; planned = backtick'siz + `_(planned)_`; hotspot kapanınca silinmez (closed+cevap).
- Önce 1 örnek BC çevirisi onaya gelir, sonra kalanlar — format tartışması 13 dosya sonrası değil,
  1 dosya üstünde yapılsın diye.