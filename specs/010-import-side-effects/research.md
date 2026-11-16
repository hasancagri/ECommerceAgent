# Research: Import Yan-Etkileri

## 1. İndirim oranı formatı — Excel ZATEN kesir

- **Decision**: Excel "İndirim %" değeri DOĞRUDAN oran olarak alınır (0.285 = %28.5); /100 dönüşümü YOK. `ImportRow.DiscountRate` = `decimal?`. Guard: 0 < rate ≤ 0.90 → kampanya adayı; aksi (0/boş/>0.90) → indirim yok.
- **Rationale**: Veri kontrolü gösterdi — değerler zaten kesir (0.285, 0.3674, 0.395, 0.34), %37.1 dolu. Campaign.Rate guard'ı (%1-90 = 0.01-0.90) ile uyumlu.
- **Alternatives**: /100 bölme — YANLIŞ (değer zaten kesir), reddedildi.

## 2. Açık-uçlu Campaign — EndsAt nullable (aggregate evrimi)

- **Decision**: `Campaign.EndsAt` → `DateTimeOffset?` (null = açık-uçlu/süresiz). `Create(productId, rate, startsAt, endsAt?)`: endsAt null ise pencere-guard atlanır; doluysa `endsAt>startsAt` korunur. `IsActiveAt(now)`: `now>=StartsAt ∧ (EndsAt is null || now<EndsAt) ∧ !Cancelled`. Rate guard (%1-90) KORUNUR.
- **Rationale**: İthal indirimin bitişi yok (Kitapyurdu süresiz gösteriyor). Mevcut model pencere zorunlu kılıyordu — minimal, geriye-uyumlu evrim (dolu endsAt davranışı değişmez).
- **Marten**: `DateTimeOffset?` nested değil skaler — deserialize sorunu yok (Money/BookTag tuzağı burada geçerli değil).
- **Alternatives**: Uzak-gelecek bitiş (now+100yıl) — "2125'te biten kampanya" semantik kirliliği, reddedildi. Campaign bypass (oranı doğrudan Catalog'a) — BC ihlali (Discount otorite), reddedildi.

## 3. İndirim yayını — import-özel ProductDiscountChanged

- **Decision**: Discount `CatalogConsumers`, `ProductDiscountImported` tüketince `Campaign.Create` (açık-uçlu, hemen aktif) + ARDINDAN `ProductDiscountChanged(productId, rate)` YAYINLAR (consumer post-commit publish — File/worker deseni). Genel `CampaignWindowProcess` ertelenmiş kalır.
- **Rationale**: Import kampanyası hemen aktif + tek (ürün başına), etkin oran = o rate → yayını create anında yapmak doğru + CampaignWindowProcess'i beklemez. `ProductDiscountChanged` kontratı + Catalog tüketici ZATEN var.
- **Alternatives**: CampaignWindowProcess'i şimdi kurmak — kapsam dışı (zaman-pencereli yürürlük ayrı iş), reddedildi.

## 4. Stok seed — ProductAdded tüketimi + random consumer'da

- **Decision**: Stock `CatalogConsumers`, mevcut `ProductAdded`'i tüketir → `StockItem` zaten varsa ATLA (idempotent, ProductId sorgusu) → `qty = Random.Shared.Next(30, 51)` → `StockItem.Create(productId, qty)` → store → `StockChanged(productId, qty)` YAYINLAR. Random consumer'da (altyapı); aggregate `Create(productId, onHand)` deterministik (İlke VI).
- **Rationale**: ProductAdded ürün başına bir kez (doğumda); Catalog stok-kopyası tüketicisi ZATEN var. Random demo verisi — Excel stok kullanılamaz (%97 boş + "10+"). Idempotency: re-delivery'de mükerrer kayıt olmasın.
- **Alternatives**: Excel stok ithali — kullanılamaz veri, reddedildi. Sabit N — çeşitlilik yok (demo için random daha gerçekçi), kullanıcı kararı 30-50.

## 5. Wolverine keşif + binding (tuzak)

- **Decision**: Yeni `CatalogConsumers` (çoğul) adlandırmayla BULUNMAZ → ilgili `MessagingExtensions`'a `opts.Discovery.IncludeType(typeof(CatalogConsumers))` ZORUNLU (IncludeAssembly yeterli DEĞİL — çoğul suffix convention'a uymaz). Binding'i TÜKETİCİ kurar: Stock, Catalog'un `ProductAdded` exchange'ine kendi kuyruğunu (`ProductAdded.Queues.Stock`) bağlar; Discount, Catalog'un yeni `ProductDiscountImported` exchange'ine (`Queues.Discount`) bağlar.
- **Rationale**: Kanıtlı tuzak (memory `wolverine-eventhandler-includetype` + Catalog'un kendi *Consumers IncludeType'ları). Binding tüketici-kurar = soğuk-açılış kayıp dersi (007).
- **Alternatives**: Consumer'ı tekil `*Consumer` adlandırmak — conventions kaynak-adı kuralı `<Kaynak>Consumers` çoğul ister (File/Discount/Stock Catalog'da hep çoğul); tutarlılık için IncludeType.

## 6. Worker post-commit indirim event'i

- **Decision**: `ImportJobWorker` batch'ine indirim event'leri ayrı toplanır (`batch` içinde `ProductDiscountImported` listesi); `ProductAdded` gibi commit SONRASI yayılır (yalnız `!dryRun` + rate>0 olan yeni ürün). ProductId create'ten gelir.
- **Rationale**: Ürün yazılmadan event çıkmamalı (ProductId garantisi + at-least-once); mevcut `batch.Events`/post-commit publish deseni.

## Çözülen NEEDS CLARIFICATION

- Rate format → Excel zaten kesir (§1).
- Açık-uçlu → EndsAt nullable (§2).
- İndirim Catalog'a nasıl ulaşır → import-özel ProductDiscountChanged (§3).
- Stok kaynağı → random 30-50, ProductAdded tüketimi (§4).
