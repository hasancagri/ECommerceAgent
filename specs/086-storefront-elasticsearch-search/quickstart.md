# Quickstart: Storefront Elasticsearch Search — Doğrulama

Feature'ın uçtan-uca çalıştığını kanıtlayan senaryolar. Detay: [spec.md](./spec.md),
[data-model.md](./data-model.md), [contracts/](./contracts/).

## Önkoşul

- `dotnet run --project src/aspire/AppHost/AppHost.csproj` (TÜM sistem; ES artık kaynaklardan biri).
- Storefront OpenAI secret: `dotnet user-secrets set OpenAI:ApiKey <k> --project src/services/storefront/Storefront.Api`.
- Katalog seed/import yapılmış (event-log → ES projeksiyonu dolu). ES index açılışta bootstrap'lanır.
- Müşteri AI istemcisi (Claude Desktop) storefront `/mcp` ucuna bağlı (anonim).

## Birim test (İLKE VI — impl'den önce yazılır)

```bash
dotnet test tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj
```

Kapsam (saf, mock'suz):
- `StorefrontDocument` fold: event dizisi → doğru doc durumu (Catalog+Stock+Review+Discount birleşimi).
- Satılabilirlik: `IsDeleted=true` ya da `Price` yok → doc "silinmeli" kararı (FR-008).
- `effective_price`: indirim penceresi içi/dışı türetimi.
- Embedding tazeleme kararı (`DecideEmbedding`): boş→yok, değişti→üret, aynı+var→koru.

## US1 — Serbest metin arama (P1)

1. Claude Desktop: "korku temalı, 50 TL altı, puanı iyi kitaplar".
2. `query_storefront` ES DSL üretir (`bool`: `knn`{{EMBED:"korku"}} + `range` price<50 + `range`
   rating_average>eşik).
3. **Beklenen:** yalnız fiyat<50 + puan eşik-üstü + konu-yakın kitaplar alaka sırasıyla; SC-001 < 1 sn.
4. Yazım hatası ("hari potter") → `fuzziness:"AUTO"` ile doğru kitap (FR-002, SC-002).
5. Benzerlik ("şuna benzer") → hedef embedding'iyle kNN komşular (FR-003).
6. Eşleşme yok → boş Rows + dürüst "bulunamadı" (FR-009, SC-005; uydurma yok).

## US2 — Kaynak değişimi vitrine yansır (P1)

1. Stock BC stok değiştir (admin `adjust`/`set`) → `StockChangedEvent` yayılır.
2. Consumer event'i ürün stream'ine append → async projection ES doc `stock`'u günceller.
3. **Beklenen:** ~5 sn içinde aramada yeni stok (SC-003). Aynı event tekrar → doc değişmez (idempotent).
4. Fiyat değişimi (ProductChanged) → `price` + `effective_price` güncel.

## US3 — Reindex / yeniden kurulabilirlik (P2)

1. ES index'i sil (ya da bozulma simüle et).
2. Reindex tetikle: index yeniden kur + `RebuildProjectionAsync` (event-log oynatılır).
3. **Beklenen:** tüm kitaplar event-log'dan geri yüklenir, veri kaybı %0 (SC-004).
4. Soğuk başlangıç (boş log): full reset + katalog import republish → event-log → ES dolar (FR-007).

## Sınır / honesty

- "Siparişim nerede" → arama yüzeyi üstlenmez, dürüstçe yönlendirir (başka tool).
- OpenAI embedding hatası → semantik parça başarısız, çökme yok (sorgu text'e düşer / dürüst hata izi).
- Satılamaz/silinmiş kitap → ES'te hiç yok (projeksiyon-zamanı dışlama) → sonuçta görünmez.

## UserPurchase taşıma doğrulaması

1. Checkout tamamla (OrderCompleted yayılır).
2. **Beklenen:** `UserPurchase` satırı artık **Library** DB'sinde (libraryDb), storefront'ta DEĞİL.
   Storefront ES/sorgu yüzeyi satın-almayı görmez (yapısal dışı).