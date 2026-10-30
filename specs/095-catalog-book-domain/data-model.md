# Data Model — 095 Catalog Kitap Domain Redesign

## Product (Catalog, redesign)

| Alan | Tip | Not |
|---|---|---|
| Gtin | string, required | ISBN; kimlik; unique-index (mevcut) |
| Name | string | boş reddi |
| Description | string | TEK alan (Short/Full birleşti) |
| Price | Money VO | >0 var oluş invariant'ı (Create/SetPrice) |
| PageCount | int? | opsiyonel |
| PublishedOn | DateOnly? | opsiyonel |
| Language | string? | opsiyonel |
| Translators | List\<string\> (private, R/O) | trim + dedup |
| AuthorIds | List\<Guid\> (private, R/O) | dedup + boş reddi |
| PublisherId | Guid | |
| CategoryId | Guid | TEK leaf; zorunlu (seed "Genel" fallback) |
| RelatedCategoryIds | List\<Guid\> (private, R/O) | editoryal; dedup; leaf'i İÇERMEZ |
| ImageUrl | string? | kapak File'dan (CoverIngested) |

**Davranışlar** (hepsi `ResultDomain`, test-first — İlke VI):

- `Create(name, gtin, price, description)` — fiyat>0, ad/gtin boş reddi
- `Rename(name)` · `SetDescription(description)` · `SetPrice(price)` (fiyat>0; `ProductPriceChange`
  append'i handler yapar) · `SetImage(imageUrl?)`
- `SetCategory(leafCategoryId)` — boş Guid reddi
- `SetRelatedCategories(ids)` — dedup; primary leaf listedeyse atlanır/reddedilir (invariant: related∌leaf)
- `SetAuthors(ids)` · `SetPublisher(id)` · `SetBookMeta(pageCount?, publishedOn?, language?)` ·
  `SetTranslators(names)` — mevcut davranış korunur

**Sökülen alanlar**: Sku, ShortDescription, Dimensions (+ ProductDimensions VO), Type/ProductType +
ParentGroupedProductId + FamilyCode, ManufacturerPartNumber, ShowOnHomepage, MarkAsNew,
AllowCustomerReviews, Categories (çoklu `ProductCategoryAssignment` + VO), TagIds, Seo.

**Sökülen metotlar**: AssignToCategory, RemoveFromCategory, AddTag, RemoveTag, SetFamilyCode,
SetDimensions, SetSeo, SetIdentifiers, UpdateDescriptions.

## Category (Catalog, redesign)

| Alan | Tip | Not |
|---|---|---|
| Name | string | |
| NormalizedName | string | `NameNormalization.Normalize`; unique-index; ağaç-global unique varsayımı |
| ParentCategoryId | Guid? | null=kök; en fazla 2 seviye; DOĞUMDA SABİT |

- Tek davranış: `Create(name, parentCategoryId?)` — boş ad reddi.
- Sökülen: Description, Seo, Published, ShowOnHomepage, DisplayOrder; Rename, SetParent, Reorder,
  SetPublished, SetSeo. Ağaç yalnız seed Faz A'da kurulur (kapalı liste, FR-007).

## ProductTag (Catalog) — SİLİNİR

Aggregate + `Domains/ProductTags/` + `ProductTagTests.cs` + tüm referanslar (Product.TagIds dahil).

## Değişmeyenler

- **Author** (Name, NormalizedName, Bio) · **Publisher** — FR-012.
- **ProductPriceChange** (append-only; OldPrice=null ilk fiyat) — FR-010.
- **Money VO** — kalır. ProductDimensions/SeoMetadata (Product'ta) kullanım dışı kalırsa VO dosyasından ölür.

## Discount `ProductCatalogRef` (read-model, izdüşüm)

| Alan | Eski | Yeni |
|---|---|---|
| CategoryId | Guid (tek leaf) | — |
| CategoryIds | — | List\<Guid\> (leaf + ata; event `CategoryIds`'ten) |

- `CatalogConsumers.Handle`: `evt.CategoryIds ?? [evt.CategoryId]` map'ler (null-dayanıklı).
- `CampaignSelectionResolver`: `ScopeType.Category => p.CategoryIds.Contains(scopeRef)` (saf çekirdek +
  Marten sorgusu aynı mantığa geçer). Testler hizalanır (test-first).

## Invariant özeti

- Fiyat > 0 (Create/SetPrice).
- Ürün tek leaf kategoriye bağlı; üst kategori ürüne denormalize EDİLMEZ (ağaçtan çözülür).
- RelatedCategoryIds dedup + primary leaf'i içermez.
- Category ebeveyni doğumda sabit; mutator yok; ≤2 seviye.
- Kategori adı ağaç-global unique (NormalizedName); seed ihlalde deterministik hata.
- Gtin unique; seed Gtin-varsa-atlar.
