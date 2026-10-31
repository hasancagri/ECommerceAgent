# Data Model — 096 Catalog local embeddings

## ProductEmbedding (Marten dokümanı — aggregate DEĞİL)

Türetilmiş teknik kayıt (`ProductCatalogRef` emsali); invariant taşımaz, `Domains/` dışında
(`Embeddings/`) yaşar. Kaynağı her zaman Product + Author + Category'den yeniden üretilebilir.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | = ProductId (1:1; doğal anahtar) |
| Vector | float[] | bge-m3 → 1024 eleman; JSONB içinde (R2) |
| ContentHash | string | SHA256(compose edilmiş metin) — tazelik anahtarı |
| Model | string | üreten model kimliği (ör. "bge-m3"); değişim = bayat |
| UpdatedAtUtc | DateTime | üretim zamanı |

- Üretim/yenileme YALNIZ `EmbeddingReconcileProcess`'ten (tek yazar).
- Bayatlık kuralı (saf çekirdek `NeedsRefresh`): kayıt yok ∨ `ContentHash` ≠ güncel hash ∨
  `Model` ≠ config modeli → yeniden üret.
- Silme yok (ürün silme yok); model değişiminde kayıt üzerine yazılır (upsert).

## Embedding metni (kalıcı değil — üretim anı değeri)

`EmbeddingText.Compose` (saf, test-first):

```
{Name}
Yazarlar: {AuthorName1, AuthorName2, ...}
Kategori: {LeafName > ParentName}        ← ata zinciri (CategoryNaming.ResolveChain reuse)
{Description}
```

- Boş parçalar satırıyla atlanır (boş açıklama satır üretmez — edge case).
- `MaxChars` (config) aşımında sondan kırpılır; hash KIRPILMIŞ metinden (tutarlılık).
- Hash'e model adı girmez (model ayrı kolon — iki bayatlık ekseni bağımsız).

## EmbeddingOptions (tip'li config — Options pattern)

| Alan | Varsayılan | Not |
|---|---|---|
| Enabled | true | false → süreç hiç koşmaz (CI/prod supabı) |
| BaseUrl | http://localhost:11434 | Ollama adresi |
| Model | bge-m3 | FR-006 toplu tazeleme anahtarı |
| BatchSize | 64 | tek /api/embed çağrısındaki metin sayısı |
| IntervalSeconds | 60 | boşta bekleme (aday varken beklemez) |
| MaxChars | 6000 | compose kırpma sınırı |

`AddOptions<T>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()` (konvansiyon).

## İlişkiler / değişmeyenler

- Product/Category/Author/Publisher modelleri DEĞİŞMEZ; event kontratı DEĞİŞMEZ.
- `ProductEmbedding` hiçbir event yaymaz/tüketmez (BC-içi reconcile).
- Marten şema kaydı: `opts.UsePgVector()` + `opts.Schema.For<ProductEmbedding>()`; vektör index'i YOK (R2).
