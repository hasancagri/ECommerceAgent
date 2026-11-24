# Data Model — Tipli Niyet-Slot Kontratı (Faz 1)

Yeni kalıcı tablo YOK. Tek yeni şey = React↔ScoutAgent arası **tipli DTO kontratı** + bunun mevcut
`SearchRequest`'e deterministik eşlemesi.

## BookSearchFilters (NESTED, React↔backend kontratı)

Kural: her GRUP nullable → grup null = "o konu hiç konuşulmadı". LLM kullanıcının KENDİ kelimelerini
koyar (hint = metin, çeviri/enum/sayı YOK — yorum Tur-2'de).

```
BookSearchFilters
├── Text        : TextSlot?        { Query: string?, Author: string? }
├── Publisher   : PublisherSlot?   { Include: string?, Exclude: string? }
├── PriceHint   : string?          # "ucuz", "100-300 TL", "300 altı"
├── SortHint    : string?          # "ucuzdan pahalıya", "en çok satan", "yeni"
├── BindingHint : string?          # "ciltli" / "karton"
├── PageHint    : string?          # "kalın", "kısa", "300 sayfadan fazla"
├── Taxonomy    : TaxonomySlot?    { Genres: string[]?, Moods: string[]?, Themes: string[]?, Audience: string[]? }
├── Flags       : FlagsSlot?       { OnlyDiscounted: bool, OnlyInStock: bool, OnlySalable: bool }
├── Page        : int   (vars. 1)
└── PageSize    : int   (vars. 10)
```

**Slotlama kuralları (LLM — extract prompt'u):**
- Yazarlık ("X'in kitapları/eserleri", "yazar X") → `Text.Author`; "X hakkında/üzerine" → `Text.Query`.
- Yayınevi adı/kalıbı → `Publisher.Include`; "X hariç" → `Publisher.Exclude`.
- Tür/ruh-hali/konu kelimeleri (roman, karanlık, distopya) → `Text.Query`'de bırak (sunucu facet'e çevirir).
- Sorguda AÇIKÇA geçmeyen alan null/false (uydurma yasağı — SC-003).

## Eşleme: BookSearchFilters → SearchRequest (FilterInterpreter, saf, test-first)

ScoutAgent `FilterInterpreter` düzler; Catalog `search_books` kalanı yorumlar (taksonomi/sort/ad→id).

| Slot | search_books argümanı | Yorum yeri |
|---|---|---|
| Text.Query | `query` | Catalog: BookTaxonomy.ExtractFromText (facet lift) + BM25 |
| Text.Author | `author` | Catalog: ResolveAuthorIds (jsonb `?|`), bu oturumda eklendi |
| Publisher.Include/Exclude | `publisher`/`excludePublisher` | Catalog: ResolvePublisherIds |
| SortHint | `sort` (metin) | Catalog: ParseSort ("ucuzdan pahalıya"→price_asc) |
| BindingHint | `binding` (metin) | Catalog: binding metin→enum |
| PriceHint | `minPrice`/`maxPrice` | **FilterInterpreter** (metin→sayı, saf, test-first) |
| PageHint | `minPageCount`/`maxPageCount` | **FilterInterpreter** (metin→sayı, saf, test-first) |
| Taxonomy.* | (gerekirse; v1'de query'den çıkarım yeterli olabilir) | Catalog facet |
| Flags.* | `onlyDiscounted`/`onlyInStock`/`onlySalable` | doğrudan |
| Page/PageSize | `page`/`pageSize` | doğrudan |

**Saf birim (İlke VI test-first):** `FilterInterpreter` — PriceHint/PageHint metin→sayı parse
(örn. "300 TL altı"→max=300; "100-300"→min=100,max=300; boş→null) + grup-null güvenli düzleme.
Diğer eşlemeler pass-through (metin aynen Catalog'a).

## Mevcut tipler (değişmez / hazır)
- `SearchRequest` (Catalog) — `Author` alanı bu oturumda eklendi; diğer alanlar hazır.
- `SearchBooks.Query`/`Tool` (Catalog MCP) — `author` param bu oturumda eklendi.
- `ProductSearchQueries.SearchAsync` + `ResolveAuthorIdsAsync` — hazır.

## Doğrulama kuralları
- Grup null → ilgili filtre uygulanmaz (uydurma yok).
- Author eşleşmesi yoksa → boş sonuç (dürüst), uydurma kart yok (FR-005).
- PriceHint parse edilemezse → fiyat filtresi uygulanmaz (sessiz düş, aramayı bozmaz).