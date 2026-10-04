# Data Model: Storefront Elasticsearch Search

Üç veri yüzeyi: (1) Postgres event-log — gerçek-kaynak, (2) Elasticsearch doc — sorgu projeksiyonu,
(3) kalan/taşınan/silinen varlıklar.

## 1. Event-log (Postgres / Marten `mt_events`)

Stream id = **ProductId**. Her stream, o ürüne dair kaynak BC event'lerinin sıralı append listesi
(append-only, gerçek-kaynak). Event gövdeleri = **Shared.IntegrationEvents** record'ları (değişmez
sözleşme — ayrı iç tip yok):

| Event (append edilen) | Kaynak | Taşıdığı (fold'u besleyen) |
|---|---|---|
| `ProductChangedEvent` | Catalog | Name, Description, Price, Authors[], Publisher(Id), Category(Id), ImageUrl, IsDeleted, Specs[], FamilyCode |
| `StockChangedEvent` | Stock | Quantity (mutlak) |
| `ReviewSummaryChanged` | Reviews | Average, Count (mutlak; Count=0 temizler) |
| `ProductDiscountChanged` | Discount | DiscountPct (0=temizle), StartsAt, EndsAt |

- Idempotency + sıra: R3 (mutlak değer + append order). Tekrar teslim zararsız.
- `OrderCompleted` artık storefront stream'ine GİRMEZ (UserPurchase → Library).

## 2. Elasticsearch doc — `StorefrontDocument` (projeksiyon fold durumu)

`_id = ProductId`. Event-log fold'undan türer (`AggregateStreamAsync`). Alanlar + ES tipi
(mapping sözleşmesi: `contracts/search-index-mapping.json`):

| Alan | ES tipi | Kaynak event | Not |
|---|---|---|---|
| `product_id` | keyword | (stream id) | _id ile aynı |
| `name` | text(turkish)+keyword | ProductChanged | keyword alt-alan sort/exact |
| `description` | text(turkish) | ProductChanged | embedding kaynağı |
| `authors` | text(turkish) | ProductChanged | ad listesi (Id doc'ta tutulmaz, sorguya gerekmez) |
| `publisher` | text(turkish)+keyword | ProductChanged | |
| `category` | text(turkish)+keyword | ProductChanged | İngilizce taksonomi (playbook'ta çeviri kılavuzu) |
| `price` | float | ProductChanged | liste fiyatı (TL) |
| `effective_price` | float | türetilir | indirim penceresi içindeyse indirimli, değilse price |
| `stock` | integer | StockChanged | null=bilinmiyor (alan yok) |
| `rating_average` | float | ReviewSummaryChanged | Count=0 → alan yok |
| `rating_count` | integer | ReviewSummaryChanged | |
| `specs` | nested{attribute,option keyword} | ProductChanged | özellik/varyant filtresi |
| `family_code` | keyword | ProductChanged | null=ailesiz |
| `image_url` | keyword | ProductChanged | kapak linki |
| `added_at` | date | ProductChanged | YAKLAŞIK (ilk görülme) |
| `discount_pct` | integer | ProductDiscountChanged | null=indirim yok |
| `discount_ends_at` | date | ProductDiscountChanged | |
| `embedding` | dense_vector(1536,cosine,index) | türetilir | açıklama embedding'i; `_source`'tan AYIKLANIR (dönmez) |

**Satılabilirlik (FR-008):** doc ES'e YALNIZ satılabilirse yazılır. Fold `IsDeleted=true` ya da `Price`
yok (Catalog raporlamadı) ise projeksiyon doc'u **siler** (`DeleteAsync`) — satılamaz kitap index'te
hiç bulunmaz; LLM filtresi unutsa da sızmaz. Çözüm-zamanı filtre değil, projeksiyon-zamanı dışlama.

**Etkin fiyat:** fold `discount_pct` + pencere + `price`'tan `effective_price`'ı **yazım anında**
hesaplar (SQL view-guard'ın yerini alır). **Davranış değişimi (B1, KABUL):** eski model sorgu-anında
hesaplıyordu; yeni model yazım-anında → indirim penceresi event'siz biterse `effective_price` bir sonraki
herhangi event'e kadar bayat kalır. Kesin satış kararı checkout'ta verildiği + vitrin yaklaşık/bayat-
toleranslı olduğu için (spec assumption) kabul edilir. İstenirse pencere-bitiş tazeleme watchdog'u
backlog (bu spec kapsamı dışı).

**Embedding tazeleme:** fold, açıklama öncekine göre değiştiyse `IEmbeddingGenerator` ile üretir; aksi
halde önceki doc'un vektörünü korur (`DecideEmbedding` mantığı — saf, TDD). Boş açıklama → embedding yok.

## 3. AgentQueryLog (Postgres / Marten doc — KALIR)

Değişmez şekil (ES sorgu izine uyarlanır): `Sql` alanı artık **ham ES DSL JSON** tutar (ad alan-seviyesi
kalabilir ya da `Query`'ye rename — tasks kararı). Verdict: Executed / Rejected (minimal rail reddi) /
Failed (ES hata/timeout). FR-009: her deneme (başarılı/red/hata) bir satır.

## 4. Taşınan / Silinen varlıklar

| Varlık | Aksiyon | Yeni yer / Not |
|---|---|---|
| `StorefrontView` (Marten doc) | SİLİNİR | Yerini `StorefrontDocument` (ES) alır |
| `ProductDescriptionEmbedding` + pgvector + `EmbeddingBackfillService` | SİLİNİR | Embedding ES doc alanı (R6); rebuild = reindex |
| `UserPurchase` (doc + OrderConsumers) | TAŞINIR | **Library BC** (libraryDb): `UserPurchase.cs` + `OrderConsumers` (OrderCompleted tüketir). Append-only, backfill yok (mevcut kontrat korunur) |
| `AgentSqlGuard`, `StorefrontSellableSchema`, `EmbedPlaceholder`(SQL), `AgentQuerySurfaceBootstrap`, kısıtlı rol conn-source | SİLİNİR | SQL serbest-sorgu yüzeyi gider; ES = sabit index + minimal rail; JSON guard ayrı iş |

## Library BC — UserPurchase (taşınan)

- Doc: `UserPurchase { Id="{userId:N}:{productId:N}", UserId, ProductId }` — storefront'tan birebir
  (read-model, aggregate değil; idempotent upsert).
- Consumer: `OrderConsumers.Handle(OrderCompleted)` → kalem başına `UserPurchase.Create` store.
- Wiring: Library Marten şemasına doc + `Index(x=>x.UserId)`; MessagingExtensions OrderCompleted exchange
  binding (TÜKETİCİ kurar, 007) + `IncludeType(OrderConsumers)`; AppHost Library zaten rabbit referanslı.
- İLKE VII: `library/FLOW.md` UserPurchase adımını kazanır.