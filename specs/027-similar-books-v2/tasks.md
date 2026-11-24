---
description: "Task list — 027 similar_books V2 (vektör)"
---

# Tasks: similar_books V2 (vektör) — Dilim-3

**Ölçek**: küçük (tek query yolu rewrite; yeni tablo/event/kontrat yok). Saf domain birimi YOK (kNN SQL
= canlı doğrulanır) → test-first task yok; V1 Overlap saf-birim testleri SÖKÜLÜR.

## Format: `[ID] [P?] Açıklama + dosya yolu`

- [ ] T001 `SimilarBooksQueries.FindSimilarAsync`'i VEKTÖR kNN'e rewrite: çapanın `product_embedding`
  vektörünü al → yoksa boş; kNN `e.embedding <=> anchor` + hard filtre (WorkKey<>çapa, AudienceLevel=ANY
  uyum grubu, varsa Language) + `DISTINCT ON WorkKey` (çapaya en yakın baskı) + stok-önce→yakınlık→popülerlik
  → LIMIT. Etiket-BM25 (`BuildOverlapSql`/`BuildFallbackSql`/`AppendBoosts`) KALDIR — `src/services/catalog/Catalog.Api/Search/SimilarBooksQueries.cs`
- [ ] T002 `SimilarBooks.Overlap` saf sınıfını + kullanımını SİL (vektör yolu etiket örtüşmesi kullanmaz);
  handler (`SimilarBooksHandler`) yalnız `queries.FindSimilarAsync` + ad çözümü kalır — `src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Queries/SimilarBooks.cs`
- [ ] T003 `SimilarBooksOptions`'tan boost katsayılarını (`AuthorBoost`/`GenreBoost`/`TranslatorBoost`) SİL;
  yalnız `DefaultCount`/`MaxCount`/`ClampCount` kalsın — `src/services/catalog/Catalog.Api/Domains/Products/SimilarBooksOptions.cs`
- [ ] T004 V1 Overlap saf-birim testlerini SİL (`Overlap.BuildBm25Query` artık yok) — `tests/Catalog.Api.Tests/SimilarBooksQueryTests.cs`
- [ ] T005 Catalog FLOW.md: Dilim-3 storm → as-built (anchor + hotspot closed); 021 similar_books V1 satırı
  (8d) "V2 vektör" olarak güncelle — `src/services/catalog/FLOW.md`
- [ ] T006 `dotnet build && dotnet test` yeşil + canlı: bilinen kitabın similar_books'u anlamlı komşu verir
  (V1'in kaçırdığı anlam-yakınları dahil), kendi baskıları gelmez; 024 React "benzer" akışı bozulmadı

## Dependencies
T001→T002/T003 (Overlap/boost kaldırma T001'in SQL'ini takip eder); T004 bağımsız [P]; T005/T006 en son.

## Not
Çapasız-embed fallback (eski etiket-BM25) BİLİNÇLİ kaldırıldı — backfill sonrası tüm ürün embed'li,
embed'siz-çapa yarışı boş döner (hata değil). Eşik yok (çapa-temelli hep anlamlı komşu).
