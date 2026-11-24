# Data Model — Taste-vektör kişiselleştirme (Dilim-4)

Yeni KALICI tablo YOK — taste on-demand hesaplanır (cache opsiyonel, sonra). Okunan: `UserSearchRecord`
(personalizationDb), `product_embedding` (catalogDb). Aşağıdakiler taşıma/saf tipler.

## Okunan (mevcut)

- **UserSearchRecord** (012): `userId`, `query`, `mode`, `resultCount`, `searchedAt`. Taste'in tek sinyal kaynağı
  (sorgu METNİ). Bu feature OKUR.
- **product_embedding** (025): `product_id`, `embedding vector(1536)`. kNN hedefi. OKUR.

## Taşıma / saf tipler (yeni)

### Shared proto (`taste_vector.proto`)
- **GetTasteRequest**: `string user_id`.
- **TasteReply**: `repeated float vector` (boş = taste yok/cold), `int32 signal_count`.

### Personalization (SAF — İlke VI test-first)
- **TasteVector.Average(IReadOnlyList<float[]> embeddings) → float[]**: boyut-başı ortalama (ağırlıklı; MVP düz).
  Boş girdi → boş. (Gelecek: satın-alma/yorum kitap-embed'i yüksek ağırlık.)
- **TasteVector.IsColdStart(int signalCount, int threshold=3) → bool**.
- **ComputeTaste** (slice): userId → distinct sorgular → embed (OpenAI batch) → Average → (vector, signalCount).

### Catalog
- **RecommendBooks.Query(Guid? userId, int count)** → gRPC GetTaste → kNN ya da küratörlü.
- **RecommendBooks.BookItem / Response**: SemanticSearch.BookItem ile AYNI şekil (React mevcut kart).

### ScoutAgent
- **RecommendBooksResult** (ayna-tip, ToolResults): `Items`, `Total` (Catalog Response şekli; İlke I kopya).

## Kurallar / invariantlar

- Taste-vektör model Catalog kitap-vektörleriyle AYNI (`text-embedding-3-small` 1536) — aynı uzay şart.
- Taste-vektör insan-yüzeye/LLM'e/loga RENDER EDİLMEZ (İlke VIII); yalnız gRPC'de taşınır, sunucuda kalır.
- `signalCount < 3` → kişisel yok → küratörlü (öneri boş değil).
- kNN sonucu: hard filtre (satılabilir/dil/seviye) + `DISTINCT ON WorkKey` + çeşitlilik; sahip-olunan (Library, 0 veri) no-op.
- Anonim (userId yok) → küratörlü (sinyal yok).
- Kişisellik yalnız feed (sorgu-YOK); spesifik sorgu mevcut lexical/semantik yollarda (taste karışmaz).
