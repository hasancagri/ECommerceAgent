# Data Model: İçerik-Temelli Keşif Omurgası (021)

> Adlandırma: property adları İngilizce (kod tabanı konvansiyonu), sözlük DEĞERLERİ Türkçe
> (Excel/PDF taksonomisi birebir): tur→`Genre`, ton→`Moods`, seviye→`AudienceLevel`, konu→`Themes`.

## Product (Catalog — değişen)

| Alan | Tip | Kural | Durum |
|---|---|---|---|
| Genre | string | BookTaxonomy.Genres üyesi; default "belirsiz"; geçersiz → "belirsiz" | YENİ |
| Moods | List\<string\> | 0-3 eleman, BookTaxonomy.Moods; geçersiz eleman atılır; fazlası kırpılır | YENİ |
| AudienceLevel | string | BookTaxonomy.AudienceLevels üyesi; default "belirsiz" | YENİ |
| Themes | List\<string\> | 0-4 eleman, BookTaxonomy.Themes; geçersiz atılır; fazlası kırpılır | YENİ |
| EnrichmentConfidence | decimal | [0,1] clamp | YENİ |
| WorkKey | string | WorkKeyNormalizer.Derive; boş olabilir (yazarsız/başlıksız kenar) | YENİ |
| InStock | bool (computed) | => StockCount > 0; SAKLANMAZ | YENİ |
| CategoryId | Guid | — | SİLİNİR |
| RelatedCategoryIds | List\<Guid\> | — | SİLİNİR |
| Tags | List\<BookTag\> | — | SİLİNİR (BookTag VO dahil) |
| IsSalable | bool (computed) | fiyat>0 ∧ stok>0 ∧ ≥1 yazar | DEĞİŞMEZ |

Davranış: `SetEnrichment(genre, moods, audienceLevel, themes, confidence)` (sözlük doğrulamalı,
düşürerek kabul — ResultDomain Ok), `SetWorkKey(string)`. Silinen: `SetCategory`,
`SetRelatedCategories`, `SetTags`.

## Category (Catalog) — SİLİNİR

Aggregate + Marten kaydı + unique index + CategoryNaming.ResolveChain + seed.

## BookTaxonomy (Catalog — yeni, static sözlük; aggregate DEĞİL)

- `Genres/Moods/AudienceLevels/Themes`: FrozenSet\<string\> (kanonik Türkçe küçük harf; PDF §19).
- `Unknown = "belirsiz"` sabiti.
- `IsValidGenre/Mood/AudienceLevel/Theme(string)`.
- `CompatibleAudienceLevels(string level) → IReadOnlyList<string>`: çocuk kümesi / yetişkin
  kümesi / genç yetişkin köprü / belirsiz-her-yerde (R5).

## WorkKeyNormalizer (Catalog — yeni, saf helper)

`Derive(IEnumerable<string> authorNames, string title) → string`
= join(sort(normalize(authors)), ",") + "|" + normalize(stripParens(title)).
normalize: Türkçe fold → ASCII, lowercase invariant, çoklu boşluk → tek.

## ImportJob (Catalog — değişen)

`Tagged` → `Enriched` (int): Genre ≠ "belirsiz" ∨ Themes.Count ≥ 1 olan yazılmış satır sayısı.
Yaşam döngüsü/iki-faz akış değişmez.

## ImportRow (ExcelRowMapper çıktısı — değişen)

- YENİ: `Genre` (tur kolonu), `MoodsRaw` (ton, `;`-split), `AudienceLevelRaw` (seviye),
  `ThemesRaw` (konu, `;`-split), `Confidence` (guven — decimal?, virgül toleranslı parse),
  `InStock` (bool, Stok Durumu — R7).
- SİLİNEN: `ParentCategoryName`, `LeafCategoryName`, `RelatedCategoryLeafNames`, `Tags`.
- Okunmayan kolon: Ortalama Okuma Süresi (boşaltıldı), Stok Miktarı (%97 boş).

## StockItem (Stock) — davranış DEĞİŞMEZ

`Create(productId, quantity)` 0 kabul eder (mevcut guard adet ≥ 0). Seed kararı consumer'da:
`InStock=false → 0`, `true → Random 20-50`.

## SimilarBooksOptions (Catalog — yeni, Options pattern)

`AuthorBoost=2.0, GenreBoost=1.0, TranslatorBoost=0.5, DefaultCount=5, MaxCount=20`.

## Kontratlar (Shared.IntegrationEvents)

- **ProductAdded**: `(string Barcode, Guid ProductId, string? CoverSourceUrl = null,
  bool InStock = true)` — additive, eski tüketici kırılmaz.
- **ProductChangedEvent**: `CategoryId/Category/Categories/CategoryIds` alanları ÇIKAR
  (tüketicisi yok); kalan alanlar aynen.
- **StockChanged / ProductDiscountImported / ProductDiscountChanged / CoverIngested /
  UserSearched**: DEĞİŞMEZ.