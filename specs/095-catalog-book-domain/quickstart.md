# Quickstart — 095 doğrulama kılavuzu

Ön koşul: Docker (Postgres/RabbitMQ/Redis Aspire'dan), temiz catalogDb/discountDb (dev reset — R7:
eski çoklu-kategori verisi taşınmaz, seed yeniden doldurur).

## 1. Build + testler + guard'lar

```bash
dotnet build && dotnet test
scripts/check-claude-spec-links.sh
scripts/check-flow-links.sh
```

Beklenen: 0 hata; Catalog/Discount test projeleri yeşil; sökülen tiplere (ProductTag, Sku, Dimensions,
ProductCategoryAssignment…) canlı referans yok (SC-004) — `grep -rn "ProductTag\|SetIdentifiers" src tests` boş.

## 2. Temiz açılış seed (US1 / SC-001)

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

catalogDb'de (Marten doc tabloları):

- Ürün sayısı = 11.438; her üründe tek `CategoryId`, boş olmayan Gtin.
- Kategori: 31 kök (ParentCategoryId null) + ~180 alt; `NormalizedName` çift kayıt yok.
- Örnek: "Popüler Kültür" kitabı → CategoryId = "Popüler Kültür" leaf'i; "Kültür" ataması ürüne YAZILMAMIŞ.
- `relatedCategoryPaths` taşıyan satırda RelatedCategoryIds dolu.
- Seed'i ikinci kez koştur (yeniden başlat) → sayılar değişmez (Gtin-varsa-atla).

## 3. Event alanları (US2)

> Discount BC 2026-10-31'de söküldü — izdüşüm/kampanya kontrolü düştü; `CategoryIds` kontrat alanı
> olarak doğrulanır (tüketicisi 096'da gelecek).

- RabbitMQ UI'da `ProductChangedEvent` örneği: `Categories` = ["Popüler Kültür","Kültür"],
  `CategoryIds` = [leafId, parentId] (bkz. [contracts/product-changed-event.md](contracts/product-changed-event.md)).

## 4. Fiyat alarmı kesintisiz (SC-003)

- Bir ürüne fiyat değişimi uygula (yüzey yok — Marten'den fiyat değiştirip handler yolu unit testle;
  canlıda seed sonrası `OldPrice` yalnız değişimde dolu kuralı event örneğinden doğrulanır).
- libraryDb: alarm kayıtlı üründe `PriceAlarmTriggered` izi (Library kodu değişmedi).

## 5. Kapak hattı (FR-011, FileConsumers restorasyonu)

- Açılışta File `ProductAdded` tüketir → `CoverIngested` yayar → Catalog ürün `ImageUrl` dolar.
- Kontrol: örnek ürünlerde ImageUrl `GET /files/v1/covers/{isbn}` URL'i; RabbitMQ `CoverIngested.Queues.Catalog`
  kuyruğu birikmiyor (handler geri geldi).

## 6. FLOW.md hizası (FR-013)

- `src/services/catalog/FLOW.md` yeni süreci anlatıyor (tek-leaf, kapalı ağaç, iki-fazlı seed);
  `scripts/check-flow-links.sh` yeşil.
