# Data Model — Unified Catalog Search (Phase 1)

Yeni aggregate/tablo YOK. Bir sorgu DTO'su + saf mod kararı + index fast-field haritası + söküm. `Product` aggregate el değmez.

## SearchRequest (query DTO — niyet)

| Alan | Tip | Not |
|---|---|---|
| Text | string? | Serbest metin (kitap/yazar/açıklama/etiket). Boş = filtre-only. |
| Publisher | string? | Yayınevi dahil (kısmi ad). |
| ExcludePublisher | string? | Yayınevi hariç. |
| PriceMin / PriceMax | decimal? | Fiyat aralığı (liste fiyatı). |
| PageMin / PageMax | int? | Sayfa aralığı. |
| Binding | int? (BindingType) | Karton/Ciltli. |
| CategoryId | Guid? | Tek-leaf kategori filtresi (v1; RelatedCategoryIds dahil değil). |
| OnlySalable | bool | IsSalable kopyalarından (fiyat>0 ∧ stok>0 ∧ ≥1 yazar). |
| OnlyDiscounted | bool | DiscountRate kopyası > 0. |
| Sort | enum? | price_asc / price_desc / newest / popular. |
| Page / PageSize | int | PageSize ≤ 20 (SearchParameters guard). |

Doğrulama/normalize: `SearchParameters` (boş sorgu hata değil; limit/offset/uzunluk guard; iç-boşluk tekler).

## SearchMode (türetilmiş — saklanmaz)

Saf karar (İlke VI test-first):
- `Text` dolu ∧ `Sort` boş → **Relevance** (`ORDER BY pdb.score`).
- `Sort` dolu → **FieldSort** (`ORDER BY <fast-field>`), alaka ezilir.
- `Text` boş ∧ `Sort` boş → **DefaultBrowse** (`ORDER BY PurchaseCount DESC` = popülerlik).
- Tek mod; alaka + alan-sıralaması aynı anda UYGULANMAZ (FR-004).

## ProductSummary (sonuç künyesi — mevcut, korunur)

`Isbn, Name, ImageUrl, Price, DiscountRate, Tags` (+ UI için yazar/yayınevi adları çözülür). Tüm arama türleri aynı künyeyi döner (SC-005). `From(Product)`.

## Index fast-field haritası (ProductSearchSchema — BM25)

Text (aranabilir, Türkçe/ICU): `Name`, `Description`, `Tags.Value`.
Fast field (filtre/sort): `Price.Amount`(decimal), `PageCount`(int), `PublishedOn`(date), `Binding`(int), `PublisherId`(uuid), `CategoryId`(uuid), `StockCount`(int), `DiscountRate`(decimal?), `PurchaseCount`(int).
Hepsi `mt_doc_product.data` jsonb'sinden türetilir — index Marten serileştirme şekline bağlı (`$values`, Money VO nesting); kırılganlık → anchor/test.

## Kaldırılan veri yapıları (söküm)

- Tablo: `catalogManagement.product_embedding` (+ HNSW index) DROP.
- Extension: `vector` (opsiyonel DROP; başka tüketici yok).
- Tip/slice: QueryEmbeddingClient, OpenAiEmbeddingOptions, RrfFusion, SemanticSql/SimilarSql, HybridSearch, SimilarProducts, IngestEmbeddings, SearchProducts.Semantic.

## Durum geçişi

Yok — arama saf okuma, domain state değiştirmez (FLOW.md domain süreci değişmez).
