# Quickstart: 021 doğrulama rehberi

## Ön koşul

- Docker çalışır (volume sıfır — temiz Postgres/RabbitMQ/Redis beklenir).
- `~/Desktop/ECommerce Notları/Kitapyurdu_Katalog_v11.xlsx` erişilebilir.

## Birim doğrulama

```bash
dotnet build && dotnet test
```

Beklenen: Category*/BookTag* testleri yok; BookTaxonomyTests, WorkKeyNormalizerTests,
Product SetEnrichment, ExcelRowMapper v11, ImportJob Enriched, StockItem Create(0),
SearchPredicateBuilder facet testleri yeşil.

## Canlı doğrulama (US1 — import)

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

1. Gateway'den import yükle (X-Admin-Api-Key): `POST /api/admin/catalog/imports` (v11 dosyası).
2. Dry-run raporu: Processed ≈ 22.286, Enriched ≥ %95, hata listesi makul.
3. `POST /api/admin/catalog/imports/{id}/confirm` → Completed.
4. SQL denetim: `SELECT data->>'Genre', data->>'WorkKey' FROM catalogmanagement.mt_doc_product LIMIT 5;`
5. Tükenmiş örnek (ISBN'i "SATIŞ YOK" satırından seç): StockCount 0, IsSalable false, katalogda görünür.
6. Aynı eserin iki baskısı: WorkKey eşit.

## Canlı doğrulama (US2 — facet arama)

- REST: `/api/catalog/products/search?genre=roman&mood=karanlık` → yalnız alan-eşleşen sonuç.
- REST: `?theme=aşk/romantizm&theme=aile` (URL-encode!) → OR davranışı.
- REST: `?genre=olmayan-deger` → 400.
- REST: `?q=mizahi` → ton'u mizahi/absürt kitaplar açıklamada kelime olmadan da eşleşir.
- REST: `?onlyInStock=true` → hepsi StockCount>0.
- `categoryId` paramı artık tanınmaz.

## Canlı doğrulama (US3 — similar_books, Kâşif sohbeti)

1. React (ECommerceWeb) aç, Kâşif'e: "Suç ve Ceza'ya benzer kitaplar öner".
2. Beklenen: sonuç kartlarında Suç ve Ceza'nın HİÇBİR baskısı yok; aynı eser iki kez yok;
   hepsi aynı dil; çocuk kitabı yok; stokta olanlar önce.
3. Çocuk kitabı hedefle ("Küçük Prens'e benzer") → yalnız çocuk/uyumlu seviye.
4. Enrichment'i belirsiz bir kitap hedefle → aynı yazar/tür fallback sonuçları (boş değil).
5. "Bu kitap ne anlatıyor?" → Kâşif özet ANLATMAZ; kart + detay sayfası yönlendirmesi (karar).

## Kapanış kontrolleri

- FLOW.md (catalog + stock) as-built: planned ekler düştü, 019 mini-storm hotspot'u kapandı.
- CLAUDE.md BC haritası catalog satırı güncel (kategori yok, taksonomi + WorkKey + similar_books).
- `git grep -il "CategoryId" src/` → yalnız tarihsel spec/ADR kalıntısı (kod sıfır).