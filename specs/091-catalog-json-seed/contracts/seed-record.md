# Sözleşme: books.json kayıt şeması (seed-record v2+stock)

Tek commit'li dosya: `src/services/catalog/Catalog.Api/Seeding/Data/books.json` — JSON array.
İki tüketici AYNI dosyayı kendi okuyucusuyla okur (ortak DTO YOK — bilinçli tekrar):
- **Catalog** `Seeding/BookSeedHostedService`: tüm alanlar.
- **File.Api** `Migration/JsonCoverSource`: yalnız `isbn` + `imageUrl`.

Dosya yolu her iki servise AppHost env ile verilir (Options pattern; bugünkü `CoverMigration__SourceXlsxPath` deseninin JSON hali).

## Alanlar

| Alan | Tip | Zorunlu | Not |
|---|---|---|---|
| `isbn` | string | ✓ | ISBN=Gtin kimlik; idempotency anahtarı; boş satır atlanır |
| `title` | string | ✓ | Ürün adı |
| `authors` | string[] | ✓ | Ad listesi; boşsa "Unknown" fallback (083 davranışı korunur) |
| `publisher` | string | ✓ | Tek yayınevi; boşsa "Unknown" |
| `priceTry` | number | ✓ | **>0 şart** — ≤0 satır reddedilir (FR-004) |
| `imageUrl` | string? | — | Boşsa kapak yok; migration atlar, ürün placeholder (FR-007) |
| `categoryMid` | string | ✓ | Üst kategori; leaf==mid tuzağına dikkat (tek Store — bilinen Marten tuzağı) |
| `categoryLeaf` | string | ✓ | Alt kategori; boşsa mid'e düşer |
| `stock` | int | ✓ | `ProductAdded.InitialStock`; ≥0 |
| `description` | string? | — | Ürün tam açıklaması (Catalog okur; boş olabilir) |

Dosyada ayrıca `tags`/`specs`/`familyCode`/`discountPercent` alanları var — İKİ okuyucu da yok sayar (xlsx import da kullanmıyordu; indirim otoritesi Discount admin kampanyaları).

## Üretim

Kaynak: `~/dev/catalog-data/books.enriched.json` (19.711 kayıt, `stock` dahil tüm alanlar HAZIR —
script değişikliği gerekmedi; `scripts/book-import` zinciri ham veri hazırlığı için durur). Dosya
doğrulanıp (fiyat≤0 / negatif stok / mükerrer-boş ISBN = 0) `Seeding/Data/books.json` olarak commit edildi (14MB).

## Evrim kuralı

Additive: yeni alan default'lu eklenir, iki okuyucu da bilmediği alanı yok sayar. Alan silme/yeniden adlandırma = iki BC'de eşzamanlı değişiklik (bu dosya güncellenir).
