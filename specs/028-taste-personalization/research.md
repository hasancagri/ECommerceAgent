# Research — Taste-vektör kişiselleştirme (Dilim-4)

Belirsizlikler storm'da (8 hotspot) kapandı. Kararlar + gerekçe:

## 1. Taste nerede + cross-BC kNN

- **Decision:** Taste Personalization'da (embed+merkez+sakla/sun). Kitap-kNN Catalog'da. Catalog `recommend_books`
  taste'i **gRPC** (`TasteQuery.GetTaste`) çeker. Yön: Catalog=client, Personalization=server (taste sahibi).
- **Rationale:** İlke I — user data Personalization'ın, book embedding Catalog'un. gRPC sanksiyonlu S2S
  (Order→Basket deseni; karşının API'sine erişir, DB'sine değil). Vektör taşınır ama insan-yüzeye çıkmaz (İlke VIII).
- **Alternatives:** Catalog UserSearched tüketip taste'i kendi tutar (user data Catalog'a sızar — red); ScoutAgent
  vektör-arg'la Catalog'a taşır (1536-float LLM/tool arg — çirkin, red).

## 2. Taste hesap tetiği: on-demand

- **Decision:** `GetTaste` çağrısında kullanıcının `UserSearchRecord` sorguları embed → ağırlıklı merkez. Cache opsiyonel (sonra).
- **Rationale:** Sinyal küçük (≤~41, distinct daha az); tembel hesap yeter, event-başı embed maliyetinden kaçar.
- **Alternatives:** event-başı artımlı merkez (sürekli güncel ama her UserSearched'te embed); periyodik batch. Sonra.

## 3. Cold-start eşiği + fallback

- **Decision:** `signalCount < 3` → Catalog **küratörlü** döner (çok-satan / popülerlik sıralı browse, 026/018 mevcut yol).
  3+ → kişisel kNN. Öneri asla boş değil.
- **Rationale:** 1-2 sinyalden anlamlı merkez çıkmaz; küratörlü boş-durumdan iyi. Eşik config'lenebilir.

## 4. Tek centroid + decay yok (MVP)

- **Decision:** Tek ağırlıklı merkez; zaman-decay yok (düz/ağırlıklı ortalama).
- **Rationale:** Basit, kurulabilir. Çamurlu-merkez (karışık zevk) riski kabul — K-Means zevk-kümeleri faz-2 Python (ileride).
- **Alternatives:** çok-nokta kümeler (K-Means) — veri+ONNX hattı ister, sonra.

## 5. Sinyal: yalnız arama-sorgusu (MVP); satın-alma/yorum kablosu hazır

- **Decision:** Taste = sorgu-metinlerinin embed merkezi. `UserSearchRecord` sorgu METNİ tutar (tıklanan kitap değil)
  → sorgu-embedding merkezi (aynı uzay). Satın-alma/yorum (OrderCompleted/Review) → kitap-embedding yüksek ağırlık; 0 veri, kablo hazır.
- **Rationale:** Elimizdeki tek sinyal. Sorgu = niyet (zayıf) ama uzayda anlamlı nokta. Ağırlık: arama düşük, satın-alma yüksek (gelince).
- **Alternatives:** tıklama-loglama (hangi karta tıkladı — daha iyi sinyal) ayrı iş; şimdi yok.

## 6. Exclude-seen / çeşitlilik

- **Decision:** kNN sonucu WorkKey tekil + çeşitlilik (dar kümeye sıkışma: genre yay / DISTINCT). **Sahip-olunan** =
  Library satın-alma (0 veri → şimdilik no-op, kablo hazır). **Aradığı/gördüğü** = Catalog per-user görüntüleme izlemiyor
  → MVP exclude-seen YOK (not: taste-kNN zaten yakın-ama-yeni yüzeye çıkarır; tam exclude sonra, görüntüleme-log ile).
- **Rationale:** Veri yok; diversity + WorkKey dedup pratikte yeterli başlangıç. Eksik bilinçli + notlu.

## 7. Embedding client Personalization'a

- **Decision:** `EmbeddingClientFactory` Personalization'a eklenir (025 Catalog deseni), **AYNI model** `text-embedding-3-small`
  (koordinat Catalog kitap-vektörleriyle kıyaslanabilir olmalı). Anahtar Aspire `scout-openai-key` reuse (env).
- **Rationale:** Taste sorgu-embed'i Personalization'da üretilir; aynı uzay şart.

## 8. Feed yüzeyi

- **Decision:** ScoutAgent `GET /api/chat/v1/feed` (login şart) → `recommend_books` MCP (token forward → Catalog userId çözer) → kartlar.
  React "sana özel" ekranı AYRI dilim. recommend_books ayna-tipi `RecommendBooksResult` (ToolResults).
- **Rationale:** Mevcut ScoutAgent MCP + token-forward deseni (search_books); feed sorgu-YOK özel yol.

## Yeniden kullanılan parçalar
- gRPC: `basket_items.proto` + Basket server + Order `AddGrpcClient` + S2S token `DelegatingHandler` deseni.
- Catalog kNN: `SemanticSearchQueries` (026) `<=>` + `VectorLiteral` + hard filtre + WorkKey (027).
- `EmbeddingClientFactory` (025). Küratörlü fallback: `ProductSearchQueries` browse (popülerlik) yolu (018).
- ScoutAgent: `SearchTyped` MCP tool-çağrısı + `ToolResults` ayna-tip + token forwarding.
