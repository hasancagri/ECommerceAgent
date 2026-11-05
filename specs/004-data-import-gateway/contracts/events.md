# Event Kontratları — 004

## ProductAdded (DEĞİŞİR — additive)

```csharp
public record ProductAdded(
    string Barcode,
    Guid ProductId,
    string? CoverSourceUrl = null); // 004: Excel "Kapak URL"; null = eski davranış
```

Yayıncı: Catalog (ürün doğumunda — import dahil her doğum yolu). Tüketici: File `CatalogConsumers` —
`CoverSourceUrl` doluysa ve R2'de ISBN yoksa indirir; null ise mevcut staging/R2 yoklaması.
Binding'i TÜKETİCİ kurar (soğuk-açılış kuralı). Default'lu alan = eski tüketici kırılmaz.

## CoverIngested (DEĞİŞMEZ; tüketicisi GERİ GELİR)

```csharp
public record CoverIngested(string Isbn, string Url);
```

Yayıncı: File (kapak R2 + kayıt defteri sonrası). YENİ tüketici: Catalog `FileConsumers` →
Gtin'le ürün bulunur → `Product.SetImage(url)`. Idempotent (aynı url tekrar set edilebilir).
DLQ deseni: retry merdiveni → error-queue (088). Wolverine keşfi: `FileConsumers` adı `*Consumers` —
`IncludeType` kaydı ZORUNLU (bilinen tuzak).

## Import iç mesajı (BC-İÇİ — RabbitMQ'ya çıkmaz)

`ProcessImportJob(Guid JobId)` — Wolverine durable LOCAL queue; Catalog `Process/` handler'ı tüketir.
Integration event DEĞİL; Shared'a girmez.