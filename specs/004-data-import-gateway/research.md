# Research — 004 Temel Veri + Gateway

## R1 — Excel dosya yapısı (gerçek dosya incelendi, 2026-11-04)

- **Karar:** Çok-sayfalı çalışma kitabı; SAYFA = YAYINEVİ (5 sayfa: Yapı Kredi 5.988, İletişim 3.629, Can 3.793, İş Bankası Kültür 7.327, İthaki 2.863 = 23.600 satır). Yayınevi adı satırda değil SAYFA ADINDAN alınır.
- **Sütunlar (27):** Kitap Adı · Yazar · ISBN · Çevirmen · Kategori · İlgili Kategoriler · Sayfa Sayısı · Ortalama Okuma Süresi · Dil · Yayın Tarihi · Cilt Tipi · Liste Fiyatı (₺) · Kitapyurdu Fiyatı (₺) · İndirim % · Puan · Puan Adedi · Yorum Sayısı · Satın Alma Adedi · Okuyacağım · Okuyorum · Okudum · Favori · Stok Durumu · Stok Miktarı · Kapak URL · Açıklama · Yazar Açıklaması
- **Kaynak ayrımı (2026-11-04 oturumu):** 14 kolon yayıncı künyesi; 13 kolon Kitapyurdu platform verisi (okur davranışı + mağaza işletmesi + türetilmiş). Okur-davranışı kolonları "dış-kaynak popülerlik snapshot'ı" olarak ALINIR (kullanıcı kararı: "veriler şimdilik kalsın"); mağaza-işletme kolonları alınmaz (fiyat fallback'i hariç).

## R2 — Sütun → sade Product eşlemesi

**İlke: import yalnız INSERT yapar, ISBN üzerinden kontrol eder (FR-002). Künyeye girmeyen sütun OKUNMAZ.**

| Excel | Hedef |
|---|---|
| Kitap Adı | Product.Name |
| Yazar | çok-yazar get-or-create (ayraç analizi implement'te; tek ad = tek yazar) |
| ISBN | Product.Gtin (kimlik; skip anahtarı) |
| Çevirmen | Product.Translator |
| Kategori | tek leaf: "Kitap > Üst > Leaf" zinciri — "Kitap" kökü atılır, ≤2 seviye; çözülemeyen → "Genel" |
| İlgili Kategoriler | RelatedCategoryIds (çözülemeyen atlanır) |
| Sayfa Sayısı | Product.PageCount |
| Dil | Product.Language |
| Yayın Tarihi | Product.PublishDate (dd.MM.yyyy) |
| Cilt Tipi | Product.Binding |
| Liste Fiyatı | Product fiyatı; boşsa Kitapyurdu Fiyatı'na düşülür; ikisi de boş = fiyatsız doğar (satış-dışı, 001) |
| Puan / Puan Adedi / Yorum Sayısı / Satın Alma Adedi / Okuyacağım / Okuyorum / Okudum / Favori | popülerlik sayaçları (001 dış-kaynak anlık görüntüsü — `Product.SetPopularity`) |
| Kapak URL | kapak zinciri kaynağı (Catalog'da saklanmaz; event'le File'a taşınır — R5) |
| Açıklama | Product.Description |
| Yazar Açıklaması | Author bio (Author'da alan varsa doldurulur; yoksa YOK SAYILIR — implement'te koda bakılır) |

**Okunmayan sütunlar ve gerekçeleri:**
- *Ortalama Okuma Süresi*: kaynağın sayfa sayısından türettiği değer; künye sade (02 kararı).
- *İndirim %*: indirim ayrı BC'nin (Discount/Campaign) kavramı; liste fiyatını ezmez, dış kaynağın o günkü oranı domain'e girmez.
- *Stok Durumu / Stok Miktarı*: bu rakamlar KAYNAĞIN deposunun gerçeği — bizim envanterimiz değil. Kullanıcı kuralı (2026-11-04): stok elle/import'la YAZILMAZ, gerçek olaylarla değişir (mal kabul artırır, satış düşürür; admin bile elle oynatamaz). Import stok yazmaz; ürün stok 0 doğar, satış-dışıdır. NOT: stok dilimi gelince aynı Excel'in Stok Miktarı sütunu TEK SEFERLİK "kurgusal açılış irsaliyesi" (toplu mal kabul) girdisi olabilir — Stock BC'nin kendi spec'inin konusu.

## R3 — Excel okuma kitaplığı

- **Karar:** ClosedXML (MIT; 23.600 satır küçük hacim, akışlı okuma yeterli).
- **Alternatifler:** EPPlus (Polyform lisans — reddedildi), MiniExcel (düşük bellek — gerek yok).

## R4 — Import işinin koşumu

- **Karar:** Upload ucu dosyayı geçici diske yazar, `ImportJob` (Marten belgesi, Catalog DB) doğar, Wolverine **durable local queue**'ya iş mesajı atılır; `Process/` handler'ı sayfaları/satırları işler, sayaçları ImportJob'a aralıklı yazar (ör. 100 satırda bir). Cache-boşaltma alt-zinciriyle aynı idiom (BC-içi iş, RabbitMQ'ya çıkmaz).
- **Tek-iş kuralı:** aktif (kuyrukta/sürüyor) iş varken yenisi reddedilir — aggregate guard'ı (test-first).
- **Alternatif:** IHostedService + Channel — reddedildi: durable değil; Wolverine retry/error-queue bedava.

## R5 — Kapak zinciri taşıma

- **Karar:** `ProductAdded` kontratına **additive, default'lu** `CoverSourceUrl` eklenir (eski tüketici kırılmaz). File `CatalogConsumers` genişler: R2'de ISBN varsa indirme ATLANIR; yoksa `CoverSourceUrl`'den indirilir → `IFileStore` (R2) → kayıt defteri → `CoverIngested(isbn,url)`. Catalog YENİ tüketicisi `FileConsumers` → `Product.SetImage` (097 kapaksızlık kararı tersine — FLOW 6b). Açılış migration'ı kapalı kalır.
- **Alternatif:** import'un File S2S'ini senkron çağırması — reddedildi: import'u dış indirmeye kilitler; event yolu kurulu + DLQ'lu.

## R6 — Gateway

- **Karar:** YARP (`Yarp.ReverseProxy`), yeni proje `src/others/Gateway` (BC DEĞİL — DB'siz ince proxy). Rotalar config: `/api/catalog/**` → Catalog (anonim) · `/api/admin/**` → admin sınıfı. Aspire kaydı + service discovery adresleme.
- **Dilim 1 kenar yetkisi:** admin rota sınıfında `X-Admin-Api-Key` başlığı doğrulanır (değer config/secret; İlke VIII — ekran/log'a yazılmaz). Dilim 2'de JWT bu noktaya oturur, anahtar sökülür.
- **Servis ikinci katı:** Catalog admin uçları kendi anahtar doğrulamasını DA yapar (custom auth şeması — İlke V "JWT-olmayan şema meşru, zorlama scope"; gateway'e kör güven yok).
- **ForwardedHeaders:** ServiceDefaults'ta tek yerde (`XForwardedFor|XForwardedProto`, KnownNetworks/Proxies temiz) — bilinen tuzak.
- **Alternatifler:** Ocelot (bakımsız), elle middleware proxy (tekerlek) — reddedildi.

## R7 — Artefakt kademesi

- **Karar:** TAM akış — yeni aggregate (ImportJob), kontrat değişikliği (ProductAdded additive), yeni tüketici (Catalog FileConsumers), yeni proje (Gateway).