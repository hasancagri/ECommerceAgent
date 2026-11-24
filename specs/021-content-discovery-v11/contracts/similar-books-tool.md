# Kontrat: similar_books MCP tool (YENİ — Catalog /mcp, query-only)

## İstek

```
similar_books(productId: Guid, count: int = 5)   // count ≤ 20
```

## Davranış

1. Hedef ürün okunur; yoksa anlaşılır hata (boş liste DEĞİL).
2. Benzerlik: hedefin Themes+Moods değerleri BM25 OR örtüşmesi (`paradedb.score` — IDF ağırlıklı)
   + boost: aynı yazar (+2.0) > aynı Genre (+1.0) > aynı çevirmen (+0.5) (SimilarBooksOptions).
3. Hard filtre: `Language` eşit · `AudienceLevel` uyum grubu (çocuk↔çocuk, yetişkin↔yetişkin,
   genç yetişkin köprü) · `WorkKey <> hedef` (kendi baskıları asla dönmez).
4. Tekilleştirme: WorkKey başına EN İYİ baskı (skor; eşitlikte stokta olan).
5. Sıralama: stokta-olan önce → skor → PurchaseCount. LIMIT count.
6. Fallback (hedefin Themes+Moods boş/belirsiz): aynı yazar VEYA aynı Genre jsonb filtresi,
   yalnız boost skoru; o da boşsa boş sonuç + "benzer bulunamadı" işareti.

## Dönüş (ToolResults mirror: SimilarBooksResult)

```
{ Items: [ { Id, Title, Authors, Price?, IsSalable, InStock, CoverUrl?, Publisher?, Isbn,
             Genre, Moods[] } ], Total }
```

WorkKey dönüşe ÇIKMAZ (iç alan). Kart render'ı mevcut tool-adı→kart sözleşmesiyle.

## ScoutAgent tarafı

- `CatalogSubAgent`: yeni tool wrapper + prompt kuralı ("şuna benzer kitap" niyeti →
  similar_books; önce search_books/karttan productId çöz).
- Prompt kuralı (karar): "ne anlatıyor" sorusunda özet ANLATMA — kart + detay sayfası yönlendirmesi.
- Anonim erişilebilir (search_books emsali); token gerekmez.