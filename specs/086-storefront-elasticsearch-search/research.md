# Research: Storefront Elasticsearch Search

Phase 0 — tasarım kararları. Her madde: Karar / Gerekçe / Elenen alternatif.

## R1 — Marten event-log'dan Elasticsearch'e projeksiyon

- **Karar:** Marten **async projection daemon** + özel `IProjection` (async). Daemon ilerlemeyi
  Postgres'te (`mt_event_progression`) tutar. Projection her batch'te etkilenen ProductId'leri
  `AggregateStreamAsync<StorefrontDocument>` ile taze katlar → `StorefrontDocument` → Elasticsearch
  `IndexAsync` (upsert) ya da sellable değilse `DeleteAsync`. Rebuild = ES index'i baştan kur +
  `daemon.RebuildProjectionAsync`.
- **Gerekçe:** Marten native projection hedefi Postgres; dış store'a (ES) yazım özel projection ister
  (design doc riski). `AggregateStreamAsync` ile fold mantığı **saf + test edilebilir** (İLKE VI); ES
  yazımı yan etki. Daemon offset Postgres'te → "nereye kadar işledim" dayanıklı, reindex idempotent.
- **Elenen:** (a) Wolverine consumer içinde doğrudan ES'e yaz — event-log'u atlar, rebuild/gerçek-kaynak
  kaybolur (spec'in kalbi). (b) Marten `EventProjection` inline — ES'e transaction'sız yazar, async
  daemon'un progress/rebuild garantisi yok.

## R2 — Event-log modeli (yabancı event'ler stream'de)

- **Karar:** Consumer'lar gelen integration event'i (ProductChangedEvent / StockChangedEvent /
  ReviewSummaryChanged / ProductDiscountChanged) **ürün stream'ine append** eder (`session.Events.Append
  (evt.ProductId, evt)`), stream id = `ProductId`. Bunlar "durable inbox" — başka BC'lerin olayları, kendi
  domain olayımız değil. OrderCompleted artık storefront'a GİRMEZ (UserPurchase → Library).
- **Gerekçe:** ProductId her event'te var; tek stabil anahtar. Fold sırası = stream sequence. Shared
  integration event record'ları doğrudan event gövdesi olur (ayrı iç tip gereksiz, İlke I sözleşme).
- **Elenen:** Ayrı iç "captured event" tipine çevirme — gereksiz dolaylama, bilinçli-tekrar değil saf
  tercüme maliyeti; Shared tipi zaten paylaşılan sözleşme.

## R3 — Sıra + idempotency (FR-006)

- **Karar:** Sıra otoritesi = **append order (stream sequence)**. Idempotency, event'lerin **mutlak değer**
  taşımasından gelir: StockChangedEvent (Quantity mutlak), ReviewSummaryChanged (mutlak özet),
  ProductDiscountChanged (tam pencere), ProductChangedEvent (tam künye). Fold = son-yazan-kazanır (her
  alan grubu kaynağının son event'inden). Tekrar teslim → aynı değer → doc değişmez.
- **Gerekçe:** Her kaynak tek yayıncı → RabbitMQ tek kuyruk kaynak içi sırayı korur; farklı kaynaklar ayrı
  alan grupları yazar (çakışma yok). Event sözleşmeleri DEĞİŞMEZ (assumption) → version/timestamp alanı
  eklemiyoruz.
- **Artık risk (kabul):** Aynı kaynaktan iki event broker'da ters sıralanırsa eski değer yeniyi ezebilir.
  Pratikte tek-kuyruk + Sequential bunu engeller; mutlak-değer + stok'un checkout'ta yeniden okunması
  (vitrin satış kararı vermez) kalan riski domine eder. Guard/versiyonlama ileri iş.

## R4 — Elasticsearch index mapping

- **Karar:** Tek index (ör. `storefront-books`). Metin alanları (`name`, `description`, `publisher`,
  `category`, `authors`) **`turkish` analyzer** + `name` ek `keyword` sub-field (exact/sort). Sayısal/filtre:
  `price`, `effective_price` (float), `stock` (integer), `rating_average` (float), `rating_count` (integer),
  `added_at` (date), `discount_pct` (integer), `discount_ends_at` (date), `family_code`/`image_url`/`isbn`
  (keyword). `specs` = nested ({attribute, option} keyword). `embedding` = **`dense_vector`, dims=1536,
  index=true, similarity=cosine** (kNN). Sözleşme: `contracts/search-index-mapping.json`.
- **Gerekçe:** `turkish` analyzer fuzzy/çekim toleransı (FR-002, SC-002); dense_vector+cosine semantik
  (FR-003); mevcut embedding 1536 boyut (OpenAiOption sabit). Index mapping sunucu-sahipli (LLM vermez).
- **Elenen:** Ayrı vektör-DB — ES native kNN yeter, +1 depo gereksiz. HNSW vs exact: index=true (HNSW)
  seçildi; ES default, 20k'da da ucuz, parametrik gerek yok.

## R5 — _id = ProductId (design "ISBN"inden sapma)

- **Karar:** ES doküman `_id = ProductId`.
- **Gerekçe:** Storefront'un event sözleşmelerinde **ISBN/Barcode YOK** (ProductChangedEvent'te alan yok;
  ProductAdded Barcode taşır ama Stock'a gider, storefront tüketmez). Sözleşme değişmez (assumption) →
  ISBN'le anahtarlamak imkansız. ProductId her event'te var, stream id ile birebir → projeksiyon
  tabii-idempotent. `isbn` alanı event'e girerse ileride eklenebilir (additive).
- **Elenen:** Design doc'un `_id=ISBN`'i — veri yok; contract kırmadan uygulanamaz.

## R6 — Embedding doc alanı olur (ayrı tablo kalkar)

- **Karar:** Embedding, `StorefrontDocument.Embedding` (float[1536]) → ES `dense_vector` alanı. Ayrı
  `ProductDescriptionEmbedding` tablosu + pgvector + `UsePgVector` + `EmbeddingBackfillService` SÖKÜLÜR.
  Projeksiyon fold'u açıklama değişince embedding üretir (`DecideEmbedding` mantığı taşınır), aksi halde
  önceki doc'un vektörünü korur (API çağrısı bosa gitmez).
- **Gerekçe:** Tek store (ES) → drift yok; "önce SQL süz + kosinüs sırala" yerine ES tek sorguda
  text+filter+kNN birleştirir. Backfill artık projection rebuild (ayrı servis gereksiz).
- **Elenen:** pgvector'ı koru + ES yanında — iki anlamsal store, senkron derdi; design "yerine geç" dedi.

## R7 — Sorgu arayüzü: LLM → ES Query DSL (guard ertelendi)

- **Karar:** `query_storefront` tek string param alır = **ham Elasticsearch Query DSL (JSON)**. Sunucu
  sabit index'e karşı çalıştırır (`SearchAsync` low-level JSON). Embedding: DSL içinde `{{EMBED:"..."}}`
  yer-tutucusu kalır → sunucu OpenAI ile vektöre çevirir → `knn.query_vector`'a basar (mevcut EmbedPlaceholder
  deseninin ES karşılığı). **Minimal sunucu kalkanı** (tam JSON guard DEĞİL, o ayrı iş): index sabit (LLM
  vermez), `size`≤50 clamp, `_source` whitelist (embedding dönmez), istek timeout, `AgentQueryLog` iz.
- **Gerekçe:** Kullanıcı DSL'i seçti (ES öğrenme hedefi). Tam yapısal JSON guard kapsam dışı (spec
  assumption) ama size/_source/timeout olmadan ucuz felaket riski → minimal rail şart. Playbook (070 kanonik
  ev) tool Description'ında ES DSL kalıplarıyla yeniden yazılır.
- **Elenen:** Parametrik slot (JSON intent) — mimar önerdi ama kullanıcı DSL öğrenmek istedi; parametrik
  ES'i de silmez, ileride dönülebilir.

## R8 — Reindex + soğuk başlangıç (US3 / FR-005 / FR-007)

- **Karar:** **Reindex** = ES index sil+kur → `RebuildProjectionAsync` (event-log'u oynatır, kaynak BC'ye
  bağımsız) — SC-004 sıfır kayıp. **Soğuk başlangıç** (event-log boş, mevcut kitaplar): kaynak BC'lerin
  republish'i ile dolar. Dev'de full reset + Catalog import zaten ProductChanged/ProductDiscount/Stock/Review
  özetlerini yeniden yayar → event-log doğal dolar. Açılışta ES index yoksa bootstrap kurar (boş index +
  mapping).
- **Gerekçe:** Event sourcing'in asıl kazancı rebuild; kaynak BC'ye uzanmadan (İlke I) log'tan kurulur.
  Soğuk başlangıç mevcut seed/republish akışına yaslanır — yeni ağır backfill kanalı açmaz.
- **Artık iş (backlog):** Prod'da "tüm kaynakları republish ettir" için Catalog (+Stock/Reviews/Discount)
  republish komutu gerekebilir — ayrı iş, bu spec dev reset republish'ine güvenir.

## R9 — Aspire'da Elasticsearch

- **Karar:** AppHost'a `builder.AddElasticsearch("elasticsearch")` (Aspire ES hosting integration);
  storefront-api `.WithReference(es).WaitFor(es)`. Client `Elastic.Clients.Elasticsearch` (Aspire client
  integration ile DI). Persistent lifetime + data volume (reset'e dayanıklı değil zorunlu — ES rebuildable).
  Sürümler `Directory.Packages.props`'ta pin (Aspire 13.x hattı / Elastic 8.x) — implement anında doğrula.
- **Gerekçe:** Mevcut postgres/rabbit/redis Aspire deseni birebir. ES tek node dev yeter.
- **Elenen:** Elle docker-compose — Aspire orkestrasyon kuralı (İLKE: sistem hep AppHost'tan).