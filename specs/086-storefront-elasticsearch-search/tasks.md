# Tasks: Storefront Elasticsearch Search

**Input**: Design documents from `specs/086-storefront-elasticsearch-search/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: İLKE VI (Domain-TDD) — saf projeksiyon fold + embedding/sellability/effective-price kararları
test-first ZORUNLU (test task'ı impl'den önce). Diğer katmanlar (consumer/projection wiring/ES client/
query handler) canlı doğrulama (quickstart).

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 [P] `Directory.Packages.props`: `Elastic.Clients.Elasticsearch` (8.19.4), `Aspire.Hosting.Elasticsearch` (13.3.0), `Aspire.Elastic.Clients.Elasticsearch` (13.3.0) — Aspire 13.x ES integration transitively 8.19.4 client çeker
- [X] T002 [P] `src/services/storefront/Storefront.Api/Storefront.Api.csproj`: ES client + Aspire client integration `PackageReference` ekle; pgvector (`Marten.PgVector`, `Pgvector`) referanslarını kaldır
- [X] T003 `src/aspire/AppHost/AppHost.cs` + `AppHost.csproj`: `builder.AddElasticsearch("elasticsearch")` (persistent lifetime + data volume) kaynağı; `storefront-api`'ye `.WithReference(es).WaitFor(es)`

**Checkpoint**: ES kaynağı Aspire'da ayağa kalkar, paketler çözülür.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: Tüm user story'lerden ÖNCE bitmeli — ES pipeline iskeleti + doc fold.

### Domain-TDD (İLKE VI — impl'den ÖNCE, FAIL etmeli)

- [X] T004 [P] `tests/Storefront.Api.Tests/StorefrontDocumentFoldTests.cs`: event dizisi → doc fold testleri (Catalog+Stock+Review+Discount alan grupları birleşir; kısmi satır geçerli; mutlak değer son-yazan-kazanır)
- [X] T005 [P] `tests/Storefront.Api.Tests/StorefrontDocumentSellabilityTests.cs`: `IsDeleted=true` ya da `Price` yok → "sil" kararı; satılabilir → "yaz" (FR-008)
- [X] T006 [P] `tests/Storefront.Api.Tests/StorefrontDocumentPricingTests.cs`: `effective_price` indirim penceresi içi (indirimli) / dışı (price) türetimi
- [X] T007 [P] `tests/Storefront.Api.Tests/EmbeddingDecisionTests.cs`: `DecideEmbedding` — boş→Clear, değişti→Generate, aynı+var→Keep (mevcut testi taşı/uyarla)

### Impl

- [X] T008 [US-shared] `src/services/storefront/Storefront.Api/Search/StorefrontDocument.cs`: ES doc şekli + saf fold metotları (`ApplyCatalog/ApplyStock/ApplyReviewSummary/ApplyDiscount`) + `EffectivePrice` + sellability kararı + `DecideEmbedding` (StorefrontView'den taşınır; T004-T007 geçer)
- [X] T009 `src/services/storefront/Storefront.Api/Search/ElasticsearchExtensions.cs`: Aspire ES client DI kaydı (`AddElasticsearchClient("elasticsearch")`) + Program.cs kablosu
- [X] T010 `src/services/storefront/Storefront.Api/Search/StorefrontSearchIndex.cs`: index adı (`storefront-books`) + `contracts/search-index-mapping.json` mapping'iyle açılışta index yoksa kuran hosted service (turkish analyzer + dense_vector 1536/cosine + nested specs)
- [X] T011 `src/services/storefront/Storefront.Api/Extensions/MartenExtensions.cs` + `Projections/StorefrontProjection.cs` (NO-OP iskelet): event store moduna geç — `StorefrontView`/`ProductDescriptionEmbedding`/`UserPurchase` doc şemaları + `UsePgVector` KALDIR; **`StorefrontProjection` NO-OP iskeletini oluştur** (Marten `IProjection`, boş `ApplyAsync`) → `AddProjectionWithServices<StorefrontProjection>(ServiceLifetime.Singleton, ProjectionLifecycle.Async)` + **`AddAsyncDaemon(DaemonMode.Solo)`** (dev Wolverine Solo durability ile uyumlu; daemon koşmazsa projeksiyon hiç çalışmaz); `AgentQueryLog` doc KALIR; `IntegrateWithWolverine` + `ApplyAllDatabaseChangesOnStartup` korunur — **Foundational derlenebilir + daemon ayakta** (F1/C1 fix)
- [X] T012 `src/services/storefront/Storefront.Api/Program.cs`: ES client + index bootstrap kablosu ekle; SQL serbest-sorgu kurulumunu SÖK (`AgentQueryOption` kısıtlı rol conn-source, `AgentQuerySurfaceBootstrap` hosted service, `EmbeddingBackfillService`, OpenAI embedding singleton KALIR — projeksiyon kullanır)
- [X] T013 [P] SQL yüzeyi dosyalarını sil: `AgentSql/AgentSqlGuard.cs`, `AgentSql/StorefrontSellableSchema.cs`, `AgentSql/EmbedPlaceholder.cs`, `AgentSql/AgentQuerySurfaceBootstrap.cs`, `AgentQueryConnectionSource`, `EmbeddingBackfillService.cs` + `Options/EmbeddingBackfillOption.cs`; `AgentQueryLog.cs` KORU
- [X] T014 `src/services/storefront/Storefront.Api/GlobalUsings.cs`: pgvector/SQL-yüzey using'lerini temizle, ES client + Search namespace'leri ekle

**Checkpoint**: Boş ES index açılışta kurulur; doc fold saf+test-geçer; eski SQL/pgvector yüzeyi yok.

---

## Phase 3: User Story 1 - Serbest metin arama (P1) 🎯 MVP

**Goal**: Müşteri doğal dil isteğini ES DSL'e çevirip satılabilir kitapları alaka sırasıyla alır (text+fuzzy+kNN+filtre tek sorguda).

**Independent Test**: Index'e örnek kitap kümesi elle yazılır (pipeline beklemeden); bir metin isteğinin filtre+alaka+fuzzy+semantik kriterlerini karşıladığı doğrulanır.

- [X] T015 [US1] `Domains/StorefrontView/` → `Domains/Storefront/` klasörünü RENAME et (view kavramı öldü, F2); `src/services/storefront/Storefront.Api/Domains/Storefront/Features/Agents/Queries/QueryStorefront.cs`: handler'ı ES DSL çalıştırmaya çevir — `query` string = ham ES Query DSL JSON; sabit index'e low-level `SearchAsync`; `{{EMBED:"..."}}` → `IEmbeddingGenerator` 1536-vektör → `knn.query_vector` ikamesi (çalıştırmadan önce)
- [X] T016 [US1] Aynı dosya: **minimal sunucu rail** — `size`≤50 clamp (`Truncated`), `_source` whitelist (embedding dönmez), istek timeout; boş sonuç = başarı+boş Rows (FR-009); hata → Result.Error + resource kod. **Embedding üretilemezse (OpenAI hatası)**: çökme YOK — dürüst hata kodu dön + ize yaz; sunucu auto-fallback YAPMA (DSL LLM-authored), asistan hatayı görüp semantik-siz sorguyu yeniden kurar (C3, spec edge)
- [X] T017 [US1] `src/services/storefront/Storefront.Api/AgentSql/AgentQueryLog.cs`: ES DSL iz uyarlaması — **`Sql` alan adını KORU** (rename yok, C4; yorumla "ham ES DSL JSON tutar" belirt), Executed/Rejected/Failed verdict'leri ES'e göre; ret DAHİL her çağrı iz (FR-009)
- [X] T018 [US1] `src/others/Shared/McpToolDescriptions.cs`: `query_storefront` ES DSL playbook const'ı (070 kanonik ev) — index şeması + DSL kalıpları (`match` fuzziness:AUTO / `range` / `nested` specs / `knn`+`{{EMBED}}` / `bool` birleşim) + kategori İngilizce çeviri kılavuzu + dürüstlük bloğu; MCP tool `[Description]` buradan referanslar (inline string bırakma)
- [X] T019 [US1] `src/services/storefront/Storefront.Api/Constants/StorefrontResourceConstants.cs`: ES ret/hata kodları (timeout/execution-failed/embedding-unavailable ES karşılıkları); SQL-özel kodları temizle
- [X] T020 [US1] Canlı doğrulama (quickstart US1): korku+fiyat+puan birleşik sorgu alaka sırası; "hari potter" fuzzy; "şuna benzer" kNN; eşleşme yok → dürüst boş (SC-001/SC-002/SC-005) ✅ gerçek sorgularla geçti (fantastik/romantik/AI/yemek + saf kNN backend MCP sürücüsü). **C3 edge (OpenAI embedding hatası) canlı simülasyon ATLANDI (kullanıcı kararı)** — kod yolu okundu-doğrulandı: GenerateAsync throw → STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE + AgentQueryLog.Failed + çökme yok

**Checkpoint**: Elle beslenmiş index üzerinde arama uçtan-uca çalışır (MVP).

---

## Phase 4: User Story 2 - Kaynak değişimi vitrine yansır (P1)

**Goal**: Catalog/Stock/Reviews/Discount değişimi event-log'a akar, async projection ES doc'u günceller (idempotent + sıralı).

**Independent Test**: Integration event yayınla → ~5 sn içinde aramada alanın (ör. stok) güncellendiğini gör; aynı event tekrar → sonuç değişmez.

- [X] T021 [P] [US2] `Consumers/CatalogConsumers.cs`: upsert yerine `session.Events.Append(evt.ProductId, evt)` + SaveChanges (ProductChangedEvent → stream)
- [X] T022 [P] [US2] `Consumers/StockConsumers.cs`: `Events.Append` (StockChangedEvent → stream)
- [X] T023 [P] [US2] `Consumers/ReviewsConsumers.cs`: `Events.Append` (ReviewSummaryChanged → stream)
- [X] T024 [P] [US2] `Consumers/DiscountConsumers.cs`: `Events.Append` (ProductDiscountChanged → stream; "satır yoksa no-op" mantığı kalkar — append her zaman)
- [X] T025 [US2] `src/services/storefront/Storefront.Api/Projections/StorefrontProjection.cs`: T011 NO-OP iskeletini **DOLDUR** — batch'teki etkilenen ProductId'leri `AggregateStreamAsync<StorefrontDocument>` ile katla → satılabilirse ES `IndexAsync`, değilse `DeleteAsync`; açıklama değişince embedding üret (`IEmbeddingGenerator`), aksi halde önceki doc vektörünü koru
- [X] T026 [US2] `src/services/storefront/Storefront.Api/Extensions/MessagingExtensions.cs`: OrderCompleted exchange binding + `IncludeType(OrderConsumers)` KALDIR; 4 kaynak binding KALIR. **`OnException<ConcurrencyException>().RetryTimes(5)` KALDIR** (C2) — eski `StorefrontView` doc optimistic-concurrency içindi; artık event-log'a append (append-only, doc çakışması yok). Stream sırası için `Sequential()` KALIR (aynı ürün stream'ine eşzamanlı append determinizmi). Async daemon T011'de kayıtlı — ayrıca başlatma gerekmez
- [X] T027 [P] [US2] Sil: `Consumers/OrderConsumers.cs` + `Domains/UserPurchase/UserPurchase.cs` (storefront'tan; Library'ye Phase 6)
- [X] T028 [US2] Canlı doğrulama (quickstart US2): stok değişimi ~5 sn içinde aramada (SC-003); tekrar event idempotent; fiyat değişimi `effective_price` güncel ✅ backend StockChangedEvent publish → 8→99 ~6sn + 3× tekrar idempotent (doc=1, stock=99, append-only stream). Fiyat/effective_price canlı doğrulaması kaldı (ayrı)

**Checkpoint**: Uçtan-uca pipeline (event → log → projection → ES) canlı; US1 artık gerçek veriyle.

---

## Phase 5: User Story 3 - Arama vitrini yeniden kurulabilir (P2) — ⏸ BACKLOG (ertelendi)

> **ERTELENDİ (kullanıcı kararı):** Reindex (index sil+kur+`RebuildProjectionAsync`) şu aşamada YAGNI —
> `EnsureAsync` (index yoksa kur) açığı kapatır; dev full-reset + katalog republish event-log'u yeniden
> doldurur (R8 soğuk-başlangıç). Tek açık senaryo = ES kaybolur+Postgres sağ kalır (offset ilerlemiş →
> daemon backfill yapmaz); nadir, prod derdi. `StorefrontReindex.cs` + `RecreateAsync` SİLİNDİ + çift-daemon
> wart'ı kalktı. İhtiyaç doğunca: çift-daemon'suz internal endpoint ya da prod republish komutu.

**Goal (ertelendi)**: Operatör index'i sıfırdan event-log'tan yeniden kurar (veri kaybı yok).

- [ ] T029 [US3] ⏸ BACKLOG — Reindex yolu (index sil+kur + `RebuildProjectionAsync`); çift-daemon'suz tasarım gerekir
- [ ] T030 [US3] ⏸ BACKLOG — Soğuk başlangıç prod republish komutu (dev full-reset+import R8 ile zaten dolar)
- [ ] T031 [US3] ⏸ BACKLOG — Reindex canlı doğrulaması (SC-004 %0 kayıp)

**Checkpoint**: US1 + US2 bağımsız çalışır; US3 (reindex) backlog.

---

## Phase 6: Sınır taşıması — UserPurchase → Library (FR-011)

**Goal**: Kişisel satın-alma kaydı storefront'tan çıkar, Library BC'ye (libraryDb) taşınır.

- [X] T032 [P] `src/services/library/Library.Api/Domains/UserPurchase/UserPurchase.cs`: read-model doc (Id=`{userId:N}:{productId:N}`, idempotent upsert) — storefront'tan birebir taşı
- [X] T033 [P] `src/services/library/Library.Api/OrderConsumers.cs`: `OrderCompleted` tüket → kalem başına `UserPurchase.Create` store
- [X] T034 `src/services/library/Library.Api/Extensions/MartenExtensions.cs`: `UserPurchase` doc + `Index(x=>x.UserId)`
- [X] T035 `src/services/library/Library.Api/Extensions/MessagingExtensions.cs`: OrderCompleted exchange binding (TÜKETİCİ kurar, 007) + `IncludeType(OrderConsumers)`; AppHost Library zaten rabbit referanslı (gerekirse `WaitFor` ekle)
- [ ] T036 Canlı doğrulama: checkout tamamla → `UserPurchase` libraryDb'de, storefront'ta DEĞİL

**Checkpoint**: UserPurchase Library'de; storefront sorgu yüzeyi satın-almayı görmez.

---

## Phase 7: Polish & Cross-Cutting

- [X] T037 [P] `src/services/storefront/FLOW.md`: domain süreci güncelle (read-model upsert → event-log append + async projection → ES; SQL kapı → ES DSL kapı; UserPurchase adımı çıkar) — İLKE VII, AYNI PR
- [X] T038 [P] `src/services/library/FLOW.md`: UserPurchase adımı ekle (OrderCompleted → kişisel kayıt)
- [X] T039 [P] `scripts/check-agent-query-schema.sh`: SQL kolon-adı drift guard'ı — ES'e göre güncelle ya da retire et (yeni hedef = tool Description ES alan adları)
- [X] T040 [P] `CLAUDE.md` BC haritası: storefront satırını güncelle (Postgres read-model → Marten event-log + Elasticsearch projeksiyon; query_storefront ES DSL; UserPurchase→Library)
- [X] T041 `scripts/check-flow-links.sh` + `dotnet build` + `dotnet test tests/Storefront.Api.Tests tests/Library.Api.Tests` yeşil
- [ ] T042 Tam quickstart.md doğrulaması (AppHost'tan, Claude Desktop ile US1-3 + UserPurchase)

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: bağımsız, hemen.
- **Phase 2 (Foundational)**: Phase 1 sonrası; TÜM story'leri BLOKLAR. T004-T007 (TDD) → T008; T009-T014 paralel-ish.
- **Phase 3 (US1)**: Foundational sonrası. Elle index seed ile bağımsız test edilebilir (MVP).
- **Phase 4 (US2)**: Foundational sonrası. Pipeline'ı kurar; US1'i gerçek veriyle besler (ama US1 seed ile US2'siz de test edilir → bağımsızlık korunur).
- **Phase 5 (US3)**: US2 pipeline'ı + projection gerekir (rebuild onu oynatır).
- **Phase 6 (UserPurchase→Library)**: storefront aramasından BAĞIMSIZ; Phase 2+ ile paralel koşabilir (ayrı servis). T027 (storefront OrderConsumers sil) ile T032-T036 (Library'ye ekle) AYNI PR — mesaj kaybı olmasın.
- **Phase 7 (Polish)**: ilgili story'ler bitince.

### Paralel fırsatlar

- T001-T002 [P]; T004-T007 [P] (farklı test dosyaları); T013 [P] (silme); T021-T024 [P] (farklı consumer dosyaları); T032-T033 [P]; T037-T040 [P] (farklı doküman).

---

## Implementation Strategy

- **MVP**: Phase 1 → 2 → 3 (US1). Elle beslenmiş index'te arama çalışır → STOP + doğrula.
- **Incremental**: +US2 (canlı pipeline) → +US3 (rebuild) → +UserPurchase taşıma → Polish.
- **İLKE VI**: T004-T007 testleri T008'den ÖNCE yazılır ve FAIL eder.
- **İLKE VII**: FLOW.md güncellemeleri (T037-T038) bu PR'da.

---

## Coverage (FR/SC → task)

- FR-001 → T015/T018/T020 · FR-002 → T018/T020 · FR-003 → T015/T018/T020 · FR-004 → T021-T024 · FR-005 → T011/T025/T029 · FR-006 → T004/T025/T028 · FR-007 → T030/T031 · FR-008 → T005/T008/T025 · FR-009 → T016/T017/T020 · FR-010 → T015/T018 · FR-011 → T032-T036
- SC-001 → T020 · SC-002 → T018/T020 · SC-003 → T028 · SC-004 → T031 · SC-005 → T016/T020