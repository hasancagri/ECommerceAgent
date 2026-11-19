# Quickstart — Unified Catalog Search (doğrulama rehberi)

Amaç: birleşik aramanın uçtan uca çalıştığını + semantik sökümün temiz olduğunu + Türkçe/ölçek davranışını kanıtlamak. Kod/implementasyon detayı yok (bkz. tasks.md).

## Ön koşul

- Sistem Aspire'dan: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
- Marten şema + `ProductSearchSchemaInitializer` boot'ta koşar (BM25 index + fast field + Türkçe ICU).
- Katalog dolu (import ya da mevcut veri). Pinned pg_search sürümü kurulu.

## Senaryo 1 — basit + kompleks aynı uç (US1)

1. `GET /api/catalog/products/search?q=Dostoyevski` → yazarın kitapları alaka sırasıyla sayfalı döner.
2. `GET /api/catalog/products/search?q=Dostoyevski&publisher=Can&minPrice=100&maxPrice=300&binding=karton&maxPageCount=300&onlySalable=true` → yalnız tüm kısıtları sağlayan kitaplar.
3. İki çağrı da aynı yanıt biçimi (items/total/page). Kod: tek executor (ayrı basit/kompleks yol yok).

## Senaryo 2 — ölçek (US2)

1. Büyük veri kümesinde (ör. ≥1M) filtreli+sıralı sorgu çalıştır.
2. Beklenen: yalnız sayfa (≤20) döner; yanıt süresi kabul sınırında; süreç belleği aday kümesiyle şişmez (SC-002). (pg_search Top-K + fast field.)

## Senaryo 3 — varsayılan sıra + boş sorgu

1. `GET .../search` (q yok, sort yok, yalnız filtre) → sonuçlar **popülerlik** (PurchaseCount desc).
2. `GET .../search?q=%20` (boş/boşluk) → hata değil; boş ya da filtre-only sonuç.

## Senaryo 4 — Türkçe (sıfır regresyon, SC-004)

1. `GET .../search?q=tarih` → noktasız-ı/katlama doğru; "Tarih"/"tarıh" varyantları doğru kitapları getirir.
2. Birleştirme öncesi çalışan örnek aramalar sonuç kaybetmez (karşılaştırmalı elle kontrol).

## Senaryo 5 — semantik söküm temiz (SC-006)

1. Build: `dotnet build` → 0 hata (ölü referans yok).
2. Boot: Aspire ayağa kalkar; Catalog Marten/şema init hatasız.
3. DB: `catalogManagement.product_embedding` tablosu YOK; HNSW index yok.
4. Uç: `GET .../search?mode=semantic` eski davranışı yok (mode yok sayılır / 404-uç kaldırıldı); `GET .../{isbn}/similar` yok; `POST /api/admin/catalog/embeddings` yok.
5. Kod: `grep -r "Embedding\|RrfFusion\|Semantic\|SimilarProducts\|HybridSearch\|pgvector\|vector(" src/services/catalog` → yalnız söküm/geçmiş izi, canlı referans yok.

## Senaryo 6 — MCP yakınsama

1. ScoutAgent `search_books` (filtreli NL sorgu) → REST ile aynı executor; aynı künye döner.
2. `get_book` (ISBN) hâlâ nokta-fetch (Marten), aramaya dahil değil.

## Birim testleri (İlke VI — canlı öncesi yeşil)

- Mod/sıra çözümü (relevance / field-sort / default-popularity).
- BM25 terim sanitize + stopword eleme.
- Filtre → SQL predicate/parametre kurma (DB'siz, saf).
- `SearchParameters` guard'ları (mevcut).
