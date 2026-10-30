# Kontrat — `ProductChangedEvent` (Shared.IntegrationEvents)

Dış kontrat KIRILMAZ (FR-009); tek değişiklik additive alan + `Categories` semantik netleşmesi.

## To-be

```csharp
public record ProductChangedEvent(
    Guid ProductId,
    string Name,
    string Description,
    decimal Price,
    List<AuthorRef> Authors,            // [Id, Name]
    Guid PublisherId,
    string Publisher,
    Guid CategoryId,                    // TEK leaf
    string Category,                    // leaf adı
    string? ImageUrl,
    bool IsDeleted,                     // hep false (091)
    string? FamilyCode = null,          // hep null (R6; alan kontrat temizliğine kadar durur)
    decimal? OldPrice = null,           // yalnız fiyat değişiminde dolu — Library tetiği AYNEN
    List<string>? Categories = null,    // AD listesi: leaf + ata adları (yayın anında ağaçtan; related ARTIK GİRMEZ — R2)
    List<Guid>? CategoryIds = null      // YENİ, additive: leaf + ata id'leri (related HARİÇ — R1)
);
```

## Yayın noktaları (Catalog)

- Seed Faz B (`SeedBookRow` muadili) — ürün doğumunda, kategori zinciri Faz A ağacından çözülür.
- `FileConsumers.Handle(CoverIngested)` — SetImage sonrası (geri yazılıyor, R3).
- Gelecek REST yazma yolları aynı fat-event şekillendirmeyi kullanır (ortak helper, `Helpers/`).

## Tüketici etkileri

| Tüketici | Kullandığı alanlar | Etki |
|---|---|---|
| Library `CatalogConsumers` | OldPrice, Price, Name, ProductId | DEĞİŞMEZ (FR-009/SC-003) |
| ~~Discount~~ | — | BC 2026-10-31'de SÖKÜLDÜ (kapsam değişikliği); `CategoryIds` kontratta kalır, tüketicisi 096 keşif |

## Değişmeyen komşu kontratlar (referans)

- `ProductAdded(Barcode, ProductId)` — seed'de aynı commit'te yayılır; File tüketir.
- `CoverIngested(Isbn, Url)` — File yayar; Catalog tüketir (handler restorasyonu R3).
