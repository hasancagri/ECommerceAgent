# Gateway Rota Kontratı — 004

YARP config (`src/others/Gateway/appsettings.json` + Aspire service discovery). Rota sınıfları:

| Dış yol | Hedef | Yetki sınıfı (Dilim 1) |
|---|---|---|
| `/api/admin/catalog/**` | Catalog.Api | **admin**: `X-Admin-Api-Key` başlığı doğrulanır (secret; eşleşmezse 401, servise ULAŞMAZ) |
| `/api/catalog/**` | Catalog.Api | anonim (ileride okuma uçları buradan) |
| `/files/**` | File.Api | anonim (kapak serve mevcut yüzeyi) |

- Rota tablosu Program.cs'e gömülmez — config/holder (Program.cs yalnız "ne kuruluyor").
- Yetki sınıfı → Dilim 2'de JWT/scope'a evrilir; anahtar katmanı sökülür (geçiş noktası tek: gateway admin policy + Catalog custom şema).
- ForwardedHeaders: ServiceDefaults'ta (`XForwardedFor|XForwardedProto`, KnownNetworks/Proxies temiz).
- PG webhook (`/payments/callback`) gateway'e TAŞINMAZ — dış-sağlayıcı istisnası mevcut yolundan sürer (ödeme dondurma kararı).