# Data Model — 091 Katalog JSON Seed Dönüşü

## Değişen: Product (Catalog aggregate)

| Üye | Bugün | 091 sonrası |
|---|---|---|
| `Published` (bool) | Taslak/yayın ekseni; `Publish()`/`Unpublish()` ile | **SİLİNİR** — ürün var = canlı |
| `Publish()` / `Unpublish()` | Yayın kapısı (fiyat>0) / vitrin gizleme | **SİLİNİR** |
| `Price` | Yayın anında >0 zorlanır | **Oluşturma/güncelleme invariant'ı**: `Create` ve `SetPrice` fiyat≤0'ı `ResultDomain.Error` ile reddeder |
| Base `IsDeleted` (AggregateRoot denetim alanı) | Tüm aggregate'lerde `!x.IsDeleted` guard | **KALIR** — yayın makinesi değil, framework soft-delete alanı |
| Diğer davranışlar (SetPrice geçmişi, SetImage, SetAuthors…) | — | Değişmez |

Durum geçişi sadeleşir: `(yok) → canlı` — ara taslak durumu kalkmaz.

## Yeni: SeedBookRecord (Catalog `Seeding/`, saklanmaz — okuma DTO'su)

`books.json` kaydının Catalog okuyucusu. Alanlar: `isbn, title, authors[], publisher, priceTry, imageUrl, categoryMid, categoryLeaf, stock`. Doğrulama: isbn boş değil, priceTry>0, stock≥0; ihlal satırı atlatır (uyarı log). İlişkiler koddaki get-or-create ile çözülür (NormalizedName teklik — Author çok-çok, Publisher çok-bir, Category 2-seviye ağaç; `ImportProcessor`'dan taşınan mantık).

File.Api okuyucusu (`JsonCoverSource`) AYNI dosyadan yalnız `isbn + imageUrl` okur — kendi tipi, paylaşım yok (bilinçli tekrar; sözleşme `contracts/seed-record.md`).

## Silinen saklama tipleri (catalogDb)

- **ImportSession** — token-link oturumu; Marten şema + Token unique index kaydı düşer.
- **ImportRow** — xlsx staging satırı; Isbn + Status index kayıtları düşer.

Marten `ApplyAllDatabaseChangesOnStartup` tablo DÜŞÜRMEZ — eski tablolar dev full-reset'le gider (göç yazılmaz, Assumptions).

## Silinen DTO alanları

- `AdminCreateProduct` / `AdminUpdateProduct` / `AdminGetProduct` / `AdminListProducts` response'larındaki `IsPublished` alanı düşer (MCP dönüş şekli değişir — dış kırılma kaydı `contracts/mcp-surface-diff.md`).
- Keşif query filtreleri `x.Published` koşulunu bırakır (`SearchProducts`, `GetProduct`; base `!x.IsDeleted` guard kalır).

## Event etkisi (kontrat DEĞİŞMEZ)

- `ProductAdded(isbn, productId, initialStock)` — aynı; seed yolu `initialStock = kayıt.stock`, elle giriş 0.
- `ProductChangedEvent(..., IsDeleted, OldPrice)` — alanlar aynı; `IsDeleted` artık HER yayında `false` (tek `true` kaynağı `AdminSetPublished` siliniyor). "Yalnız yayındaki ürün yayar" kuralı kalkar — her ürün değişimi yayar.
- `CoverIngested(isbn, url)` — dokunulmaz.
