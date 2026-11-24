# Kontrat: ProductAdded (v2 — InStock) + ProductChangedEvent sadeleşmesi

## ProductAdded (Shared.IntegrationEvents) — ADDITIVE

```csharp
public record ProductAdded(
    string Barcode,
    Guid ProductId,
    string? CoverSourceUrl = null,   // 004
    bool InStock = true);            // 021 YENİ — Kitapyurdu "Stok Durumu" ikili sinyali
```

- Yayıncı: Catalog `ImportJobWorker` (yalnız doğumda). "SATIŞ YOK*" satırı → `InStock=false`.
- Tüketiciler: **File** (kapak — InStock'u YOK SAYAR, değişiklik gerekmez) · **Stock**
  (`CatalogConsumers`): `false → StockItem.Create(productId, 0)`, `true → Create(productId,
  Random 20-50)`; ardından mevcut `StockChanged(productId, onHand)` fanout aynen.
- Default `true` → eski yayıncı/yeni tüketici ve tersi kırılmaz (additive kural).

## ProductChangedEvent — KONTRAT SADELEŞMESİ (breaking ama tüketicisiz, 097)

Çıkan alanlar: `Guid CategoryId`, `string Category`, `List<string>? Categories`,
`List<Guid>? CategoryIds`. Kalan alanlar aynen (ProductId, Name, Description, Price, Authors,
PublisherId, Publisher, ImageUrl, IsDeleted hep false, FamilyCode).

Taksonomi alanları bu kontrata BİLİNÇLİ EKLENMEDİ — tüketici doğduğunda ihtiyaçla eklenir
(additive, YAGNI).