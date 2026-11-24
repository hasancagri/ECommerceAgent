# Research — Semantik Arama (Dilim-2)

Tüm belirsizlikler storm'da (5 hotspot) kapandı + 067 prior-art kanıtlı. Özet kararlar:

## 1. Yüzey: ayrı `semantic_search` MCP tool

- **Decision:** Catalog'da ayrı `semantic_search(query)` query-only MCP tool; `search_books` DEĞİŞMEZ.
- **Rationale:** Rol-ayrımı net (lexical vs semantik); ScoutAgent routing berrak; REST yüzeyi lexical kalır.
- **Alternatives:** `search_books`'a `mode=semantic` flag — tek tool şişer, iki farklı yolu bir imzaya sıkıştırır; red.

## 2. Routing: ScoutAgent `/extract`, deterministik dal

- **Decision:** LLM `/extract`'te betimleyici sorguyu `SemanticQuery` slotuna yazar (başlık/yazar değil,
  "ne hakkında/nasıl hissettiren"). `/search` (SearchTyped) dalı: `SemanticQuery` dolu → semantic_search ·
  `SimilarToTitle` dolu → similar_books · else → search_books. Dal deterministik (saf `IsSemanticIntent`).
- **Rationale:** LLM niyet yargısı için doğru yer (zaten iki-faz orada); dal saf/test-edilebilir.
- **Alternatives:** Catalog auto-detect (sunucu betimleyici-mi bilemez) — red; demir-yoksa-hep-semantik saf
  türetme ("roman" gibi tek kelime de semantiğe kaçar) — LLM yargısı daha isabetli, red.

## 3. Filtre + kNN sırası: yapısal pre-filter → kNN

- **Decision:** jsonb hard filtre (stokta/dil/fiyat/sayfa/yayınevi) ile aday daralt → kNN aday üstünde,
  kosinüs mesafesi sırala. 067 deseni.
- **Rationale:** Aday havuzu küçülür, net; filtre=uygunluk / vektör=yakınlık ayrımı korunur.
- **Alternatives:** kNN → jsonb post-filter (HNSW'i iyi kullanır ama sonra eler, top-K bozulabilir) — canlı
  ölçümle ters çevrilebilir; başlangıç pre-filter.

## 4. Eşik: cosine-distance cutoff, Options

- **Decision:** `SemanticSearchOptions.MaxCosineDistance` (başlangıç **0.68**), eşiğin üstü dönmez.
- **Rationale:** 067 canlı kalibrasyonu — Türkçe sorgu ↔ Türkçe/İng açıklama isabet bandı 0.56–0.64,
  alan-dışı ≥0.70; 0.68 recall/precision dengesi. Yeni kalibrasyon canlı yapılır (008 açıklamaları Türkçe → daha sıkı olabilir).
- **Alternatives:** eşiksiz top-K (alakasız doldurur, SC-004 ihlali) — red.

## 5. Sorgu embedding: anlık, cache yok

- **Decision:** Her semantik aramada sorgu `EmbeddingClientFactory` (025) ile anlık embed; saklanmaz.
- **Rationale:** Omurgayla aynı model/uzay şart (koordinat kıyaslanabilir). Önce doğruluk; maliyet ihmal
  (1 çağrı ~kuruş). Vektör ham-SQL'e text-literal `[...]::vector` (025 upsert deseni; Npgsql plugin yok).
- **Alternatives:** tekrar-sorgu Marten cache (query-embedding-cache backlog) — latency/dayanıklılık kazancı,
  şimdi değil (önce çalışsın).

## 6. Hibrit (filtre+anlam tek sorgu): rol-ayrımı, RRF yok

- **Decision:** Semantik yolda jsonb filtre + vektör sıralama birlikte; BM25 metin skoru KARIŞMAZ.
- **Rationale:** RRF 018'de söküldü ("ezber"); skor-füzyon karmaşıklığı kanıtlı ihtiyaç doğana dek yok.
- **Alternatives:** RRF füzyon (BM25+vektör) — karışık-niyet için meşru ama kanıtlı başarısızlık modu
  olmadan erken karmaşıklık; cebe (ADR-değeri), şimdi değil.

## Yeniden kullanılan mevcut parçalar

- `EmbeddingClientFactory` (025) — sorgu embed.
- `ProductSearchQueries` ham-SQL deseni (NpgsqlCommand/OpenConnectionAsync/ResolveOrderedAsync).
- `SimilarBooks` slice + `[McpServerToolType]` deseni (query-only tool).
- `SimilarBooksOptions` deseni (Options POCO + Program.cs bind/unwrap).
- Publisher/Author ad→id çözümü (`ResolvePublisherIdsAsync`/`ResolveAuthorIdsAsync`) — filtrede gerekirse.
- `BookSearchFilters.SimilarToTitle` slot + `SearchTyped` dal deseni (024) — `SemanticQuery` aynen.
- `UserSearched` publish (012/014 arama ucu deseni).
