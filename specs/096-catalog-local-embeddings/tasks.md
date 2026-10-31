# Tasks: Catalog local embedding altyapısı (kitap vektörleri)

**Input**: Design documents from `/specs/096-catalog-local-embeddings/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R7), data-model.md, contracts/ollama-embed-api.md, quickstart.md

**Tests**: İlke VI — saf çekirdekler test-first: `EmbeddingText.Compose` (bileşim/kırpma/hash) +
`NeedsRefresh` (bayatlık kararı). HttpClient/Marten/BackgroundService yolları test-sonra/canlı doğrulama.

**Organization**: US1 = üretim omurgası (MVP); US2 = dayanıklılık; US3 = 095+096 canlı kapanış.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 `src/services/catalog/Catalog.Api/Catalog.Api.csproj` — `Marten.PgVector` + `Pgvector`
      PackageReference ekle (SÜRÜMSÜZ — sürümler `Directory.Packages.props`'ta zaten pinli)

---

## Phase 2: Foundational (şema + config — story'leri bloklar)

- [X] T002 [P] `src/services/catalog/Catalog.Api/Options/EmbeddingOptions.cs` — Enabled/BaseUrl/Model/
      BatchSize/IntervalSeconds/MaxChars (data-model tablosu; DataAnnotations) + `Program.cs`'te
      `AddOptions<T>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()` + appsettings varsayılanları
- [X] T003 [P] `src/services/catalog/Catalog.Api/Embeddings/ProductEmbedding.cs` — Marten dokümanı
      (Id=ProductId, float[] Vector, ContentHash, Model, UpdatedAtUtc; private setter+ctor — Marten VO ctor TUZAĞI)
- [X] T004 `src/services/catalog/Catalog.Api/Extensions/MartenExtensions.cs` — `opts.UsePgVector()` +
      `opts.Schema.For<ProductEmbedding>()` (vektör index'i BİLİNÇLİ YOK — research R2)

**Checkpoint**: Şema + config hazır; derleniyor.

---

## Phase 3: User Story 1 - Katalog kendiliğinden vektörleşir (Priority: P1) 🎯 MVP

**Goal**: Reconcile döngüsü tüm kitapları bge-m3 ile vektörler; idempotent, hash/model tazeliği.

**Independent Test**: quickstart §3-§4 — temiz dolum 11.438, ikinci koşu 0 üretim, metin/model değişimi tazelemesi.

- [X] T005 [P] [US1] `tests/Catalog.Api.Tests/EmbeddingTextTests.cs` YAZ (failing): compose satır formatı
      (ad/yazarlar/kategori zinciri/açıklama), boş parça satır üretmez, MaxChars kırpma, hash KIRPILMIŞ
      metinden, aynı girdi→aynı hash
- [X] T006 [US1] `src/services/catalog/Catalog.Api/Embeddings/EmbeddingText.cs` — saf `Compose(...)` +
      `Hash(...)` (SHA256); T005 yeşil
- [X] T007 [P] [US1] `tests/Catalog.Api.Tests/EmbeddingRefreshDecisionTests.cs` YAZ (failing): kayıt yok /
      hash farklı / model farklı → refresh; ikisi de eş → refresh değil
- [X] T008 [US1] `NeedsRefresh` saf kararı (`Embeddings/EmbeddingText.cs` ya da `ProductEmbedding` statik —
      tek dosyada kalsın); T007 yeşil
- [X] T009 [US1] `src/services/catalog/Catalog.Api/Embeddings/OllamaEmbeddingClient.cs` — typed HttpClient;
      `POST /api/embed` batch (contracts/ollama-embed-api.md); boyut/sayı doğrulama (uyuşmazsa batch reddi)
- [X] T010 [US1] `src/services/catalog/Catalog.Api/Process/EmbeddingReconcileProcess.cs` — BackgroundService:
      döngüde aday seç (ürün+yazar+kategori yükle → compose+hash → NeedsRefresh ilk BatchSize) → embed →
      upsert (`IDocumentSession`, scope döngü başına) → aday bitene dek sür; boşta PeriodicTimer(IntervalSeconds);
      ilerleme/bitiş logları
- [X] T011 [US1] `Program.cs` kablosu — `AddHttpClient<OllamaEmbeddingClient>` + `AddHostedService<EmbeddingReconcileProcess>`
      (yalnız orkestrasyon; politika/sabit yok)
- [X] T012 [US1] Canlı doğrulama: quickstart §3 (dolum 11.438 + süre notu) + §4 (idempotensi, metin/model tazelemesi)

**Checkpoint**: Vektör omurgası canlı — MVP.

---

## Phase 4: User Story 2 - Ollama yokken sistem yara almaz (Priority: P2)

**Goal**: Fail-soft: erişilemezlik/404/bozuk yanıt loglanır, host çökmez; kapanabilir (Enabled=false).

**Independent Test**: quickstart §2 — Ollama kapalı açılış yeşil; sonradan açılınca müdahalesiz dolum.

- [X] T013 [US2] Hata yolları (T009+T010 içinde rafine): bağlantı/timeout/404/kısmi-yanıt → batch atla+logla,
      sonraki döngü dener; **TUZAK**: HttpClient timeout `TaskCanceledException` ≠ `stoppingToken` iptali —
      ayrım `stoppingToken.IsCancellationRequested` (memory: httpclient-timeout-taskcanceled-stophost)
- [X] T014 [US2] `Enabled=false` kısa devresi (süreç hiç başlamaz, tek bilgi logu) + 404'te "ollama pull
      {Model}" ipucu logu
- [X] T015 [US2] Canlı doğrulama: quickstart §2 (kapalı açılış → yeşil sistem; Ollama açılınca kendiliğinden dolum;
      restart'ta kaldığı yerden)

**Checkpoint**: US1+US2 canlı doğrulanmış.

---

## Phase 5: User Story 3 - 095+096 canlı doğrulama kapanışı (Priority: P3)

**Goal**: İki feature'ın kanıt kapanışı tek Aspire koşusunda (SC-006/SC-007).

**Independent Test**: quickstart §5 uçtan uca yeşil.

- [X] T016 [US3] Quickstart §5 koşusu: specs/095 quickstart §2-§6 (seed sayımları, event `Categories`/
      `CategoryIds`, kapak hattı, Library alarm; Discount maddeleri DÜŞTÜ) → `specs/095-catalog-book-domain/tasks.md`
      T014/T020/T026 `[X]` işaretle
- [X] T017 [US3] Quickstart §6: SC-001..007 kapanış kanıtı (sayı/süre notlarıyla quickstart'a işle)

---

## Phase 6: Polish & Cross-Cutting

- [X] T018 [P] `docs/adr/adr-search-fold-to-postgres.md` YAZ (R7: 086 ES kararının tersine dönüşü + local
      embedding; tüketici yüzeyler sonraki spec) + `docs/adr/README.md` index satırı
- [X] T019 [P] `src/services/catalog/FLOW.md` — sticky 8 planned→as-built (kenar-anchor:
      `EmbeddingReconcileProcess`, `EmbeddingText`, `ProductEmbedding`); `scripts/check-flow-links.sh` yeşil
- [X] T020 [P] `CLAUDE.md` — catalog BC satırına embedding omurgası cümlesi + Teknoloji bölümüne Ollama
      (dış bağımlılık) + origin'e `specs/096-catalog-local-embeddings`; `scripts/check-claude-spec-links.sh` yeşil
- [X] T021 Final: `dotnet build && dotnet test` + iki guard — hepsi yeşil

---

## Dependencies & Execution Order

- Setup (T001) → Foundational: T002, T003 paralel; T004←T003.
- **US1**: T005, T007 paralel başlar (test-first); T006←T005; T008←T007; T009←T002 [P T006 ile];
  T010←T003+T004+T006+T008+T009; T011←T009+T010; T012←T011 (canlı, Ollama açık).
- **US2**: T013←T010 (aynı dosyalar — sıralı); T014←T013; T015←T014 (canlı).
- **US3**: T016←T012+T015 (temiz-DB tam koşu); T017←T016.
- **Polish**: T018/T019/T020 paralel (T010 sonrası herhangi an); T021 en son.

### Parallel Example: US1

```text
Aynı anda: T005 (EmbeddingTextTests) + T007 (RefreshDecisionTests); sonra T006+T008 impl,
T009 (OllamaEmbeddingClient) T006 ile paralel (farklı dosya).
```

## Implementation Strategy

- **MVP = Setup + Foundational + US1**: omurga dolu; quickstart §3-§4'te dur-doğrula.
- US2 dayanıklılık rafinesi (çoğu T010/T009 içinde — ayrı dosya açılmaz), US3 kanıt kapanışı
  (095 borcunu da kapatır), Polish ADR/FLOW/CLAUDE.md.
- Canlı doğrulamalar (T012/T015/T016) Ollama + Docker ister; temiz-DB koşusu T016'da (destructive reset
  onayı kullanıcıda).
