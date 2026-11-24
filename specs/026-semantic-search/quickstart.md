# Quickstart — Semantik Arama (Dilim-2) doğrulama

Ön koşul: 025 embedding omurgası canlı, `product_embedding` dolu (22k). `catalog-openai-key`/`scout-openai-key` set.

## Çalıştır

```bash
docker  # Desktop açık
dotnet run --project src/aspire/AppHost/AppHost.csproj   # Aspire; React ayrı: ~/dev/ECommerceWeb npm run dev
```

## Senaryolar (spec'teki US/SC)

### US1 / SC-001 — betimleyici sorgu anlamla eşleşir
React Kâşif'e ortak-kelime içermeyen betimleyici sorgu yaz:
- "umut veren hafif bir yaz romanı"
- "kayıp ve yas üstüne sakin bir kitap"

**Beklenen:** anlamca ilgili kitap kartları (BM25'in bulamayacağı). Dönüş `tool: "semantic_search"`.
Backend izole: `POST {gw}/api/chat/v1/extract` → `SemanticQuery` dolu mu; `POST .../search` → `reply.items[].tool`.

### US2 / SC-002 — routing
- "Suç ve Ceza" → `/extract` demir (başlık) → `search_books` (SemanticQuery null).
- "Dostoyevski" → yazar demiri → `search_books`.
- "karanlık distopya atmosferi" → `SemanticQuery` dolu → `semantic_search`.

**Beklenen:** demir içeren %100 lexical yola; betimleyici semantik yola.

### US3 / SC-003 — filtre + anlam
"stokta karanlık bir roman" → `semantic_search` + `onlyInStock`. Dönen her kitap stokta.

### SC-004 — alakasızlık yok
Anlamca çok uzak/absürt sorgu → boş sonuç + "uygun kitap bulamadım" (doldurma yok).

## kNN duman testi (elle SQL, eşik hissi)

```sql
-- bir betimleyici sorgunun vektörünü elle bilemezsin; bunun yerine bir kitabın
-- komşu mesafe dağılımına bak (eşik 0.68 makul mü):
WITH a AS (SELECT embedding FROM catalogManagement.product_embedding
           JOIN catalogManagement.mt_doc_product p ON p.id=product_id
           WHERE p.data->>'Name' ILIKE '%<bilinen kitap>%' LIMIT 1)
SELECT round((e.embedding <=> a.embedding)::numeric,3) d, p.data->>'Name'
FROM catalogManagement.product_embedding e
JOIN catalogManagement.mt_doc_product p ON p.id=e.product_id, a
ORDER BY e.embedding <=> a.embedding LIMIT 15;
```
İsabetli komşular ~0.0–0.35, alakasızlar ≥0.7 bandında olmalı → eşik ayarını doğrular.

## Birim testler (host'suz)

```bash
dotnet test tests/ScoutAgent.Api.Tests/ --filter SemanticIntent    # niyet-ayrımı saf
dotnet test tests/Catalog.Api.Tests/ --filter Semantic             # vektör-literal + eşik/filtre kurucu
```

## Regresyon kalkanı
- Mevcut lexical aramalar (başlık/yazar/filtre) + `similar_books` (024) + REST `/products/search` bozulmadı.
