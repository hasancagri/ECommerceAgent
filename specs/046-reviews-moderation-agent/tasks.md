---
description: "Task list — Reviews moderasyon agent'ını ayrı broker-tabanlı worker'a taşı"
---

# Tasks: Reviews Moderasyon Agent'ını Ayrı Broker-Tabanlı Worker'a Taşı

**Input**: `specs/046-reviews-moderation-agent/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: Yeni saf-domain mantığı YOK (`Review` aggregate + `ModerationVerdict` VO değişmiyor). İLKE VI
gereği yeni test-first birim gerekmez. Regression = mevcut `tests/Reviews.Api.Tests` + canlı smoke.

## Format: `[ID] [P?] [Story?] Açıklama + dosya yolu`

- **[P]**: paralel olabilir (farklı dosya, bağımsız)
- **[US#]**: user story etiketi (yalnız story fazlarında)

---

## Phase 1: Setup — worker iskeleti + AppHost/slnx kaydı

- [x] T001 Yeni proje `src/agents/Reviews.Moderation/Reviews.Moderation.csproj` oluştur (Sdk.Web, net10.0, Nullable+ImplicitUsings, yeni UserSecretsId); ProjectReference: `src/aspire/ServiceDefaults` + `src/others/Shared`.
- [x] T002 Worker paket refs ekle (sürümsüz — CPM), T014'te Reviews'ten sökülenleri aynala: `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI` + messaging için `WolverineFx` + `WolverineFx.RabbitMQ`. Marten/Postgres/Http/JwtBearer/gRPC YOK (worker DB'siz). Sürümler zaten `Directory.Packages.props`'ta.
- [x] T003 `ECommerceAgent.slnx`'e `src/agents/` klasörü altına Reviews.Moderation projesini ekle.
- [x] T004 `src/aspire/AppHost/AppHost.csproj`'a Reviews.Moderation ProjectReference ekle.
- [x] T005 `src/aspire/AppHost/AppHost.cs`: `reviews-moderation-agent` resource kaydet (`AddProject`, `.WithReference(rabbit)`, `.WaitFor(rabbit)`); **reviewsDb referansı YOK**.
- [x] T006 [P] Worker `src/agents/Reviews.Moderation/GlobalUsings.cs` oluştur (Wolverine, Wolverine.RabbitMQ, Shared, Shared.IntegrationEvents, Microsoft.Extensions.AI, Microsoft.Agents.AI vb.).

**Checkpoint**: Boş worker çözümü derlenir; AppHost'ta resource görünür.

---

## Phase 2: Foundational — paylaşılan event sözleşmeleri (BLOKLAYICI)

**Her iki taraf da bunlara bağımlı — story fazlarından ÖNCE bitmeli.**

- [x] T007 [P] `src/others/Shared/IntegrationEvents.cs`: `record ReviewModerationRequested(Guid ReviewId, string Text, int Rating)` + `record ReviewModerated(Guid ReviewId, bool Violation, string Category, string Reason)` ekle.
- [x] T008 [P] `src/others/Shared/RabbitMqConstants.cs`: iki sabit sınıfı ekle — `ReviewModerationRequested` (Exchange `reviews.moderation-requested`, `Queues.Worker=reviews-moderation.requested`) + `ReviewModerated` (Exchange `reviews.moderated`, `Queues.Reviews=reviews.moderated`).

**Checkpoint**: Sözleşmeler derlenir; iki taraf da referanslayabilir.

---

## Phase 3 (US1 — P1): Agent kodu yalnız agents/ altında

**Hedef**: Moderasyon LLM kodu worker'a taşınır; Reviews.Api'de agent-framework kalmaz.
**Bağımsız test**: `grep Microsoft.Agents.AI/OpenAI src/services/reviews/Reviews.Api` = boş; worker process ayrı çalışır.

- [x] T009 [P] [US1] `ModerationAgent.cs`'i worker'a taşı: `src/agents/Reviews.Moderation/ModerationAgent.cs` (namespace `Reviews.Moderation`); prompt + ChatClientAgent + Temp=0 + structured `ModerationOutput` korunur.
- [x] T010 [P] [US1] `ModerationException.cs` + `Options/ModerationOptions.cs`'i worker'a taşı (`src/agents/Reviews.Moderation/`, section "OpenAI", fail-fast `ValidateOnStart`).
- [x] T011 [US1] Worker handler `src/agents/Reviews.Moderation/Features/ModerateReviewRequest.cs`: `ReviewModerationRequested` tüket → `ModerationAgent.ModerateAsync(Text, Rating)` → `ModerationOutput` → `ReviewModerated` yayınla. Metin boşsa `Violation=false, "none"` (savunma). Şema-dışı çıktı → `ModerationException`.
- [x] T012 [US1] Worker `src/agents/Reviews.Moderation/Program.cs`: AddServiceDefaults; Wolverine+RabbitMQ; `DeclareExchange(ReviewModerationRequested.Exchange, Fanout, BindQueue(Queues.Worker))` + `ListenToRabbitQueue(Queues.Worker)`; `DeclareExchange(ReviewModerated.Exchange, Fanout)` + `PublishMessage<ReviewModerated>().ToRabbitExchange(...)`; `OnException<ModerationException>` retry 10s/30s/60s → `MoveToErrorQueue`; `AddOptions<ModerationOptions>` + `AddSingleton<ModerationAgent>`; MapDefaultEndpoints.
- [x] T013 [US1] Reviews'ten sil: `src/services/reviews/Reviews.Api/Infrastructure/Moderation/*` + `Options/ModerationOptions.cs`.
- [x] T014 [US1] `src/services/reviews/Reviews.Api/Reviews.Api.csproj`: OpenAI/Microsoft.Extensions.AI.OpenAI/Microsoft.Agents.AI paket refs kaldır.
- [x] T015 [US1] `src/services/reviews/Reviews.Api/Program.cs`: `using ...Infrastructure.Moderation` + `AddOptions<ModerationOptions>` + `AddSingleton<ModerationAgent>` satırlarını kaldır.

**Checkpoint**: Worker derlenir + moderasyon üretir; Reviews'te agent-framework yok (grep boş).

---

## Phase 4 (US2 — P1): Yorumcu deneyimi korunur (Reviews tarafı)

**Hedef**: SubmitReview isteği yayınlar; ReviewModerated tüketilip ApplyModeration + özet uygulanır.
**Bağımsız test**: temiz yorum Visible kalır; sakıncalı yorum async denetimden sonra Hidden + özet güncellenir.

- [x] T016 [US2] `src/services/reviews/Reviews.Api/Domains/Reviews/Features/Commands/SubmitReview.cs`: `ModerateReview.ModerateReviewCommand` publish yerine `ReviewModerationRequested(review.Id, review.Text, review.Rating)` yayınla — **yalnız metin boş değilse** (FR-010). PII yok (id+metin+yıldız).
- [x] T017 [US2] Yeni tüketici `src/services/reviews/Reviews.Api/Domains/Reviews/Features/ReviewModeratedHandler.cs`: `ReviewModerated` tüket → `Review` yükle (null/`ModeratedAtUtc` set ise no-op) → `ModerationVerdict.Create(Violation,Category,Reason)` → `ApplyModeration` → Store; Visible→Hidden olduysa kalan Visible'lardan özet hesapla + `ReviewSummaryChanged` yayınla. (Mantık eski `ModerateReview` handler'ından taşınır, LLM çağrısı hariç.)
- [x] T018 [US2] `src/services/reviews/Reviews.Api/Domains/Reviews/Features/Commands/ModerateReview.cs` dosyasını sil.
- [x] T019 [US2] `src/services/reviews/Reviews.Api/Program.cs`: `PublishMessage<ModerateReview...>().ToLocalQueue(...)` + `OnException<ModerationException>...MoveToErrorQueue` kaldır; ekle: `DeclareExchange(ReviewModerationRequested.Exchange, Fanout)` + `PublishMessage<ReviewModerationRequested>().ToRabbitExchange(...)`; `DeclareExchange(ReviewModerated.Exchange, Fanout, BindQueue(Queues.Reviews))` + `ListenToRabbitQueue(Queues.Reviews)`. `UseDurableLocalQueues` (outbox) kalır.
- [x] T020 [US2] Reviews'te `ModerationException`/`ModerateReview` kalan referanslarını temizle (derleme kırılmasın).

**Checkpoint**: Uçtan uca çalışır — submit → worker → moderated → apply. Post-moderation davranışı korunur.

---

## Phase 5 (US3 — P2): Broker/agent kesintisinde dayanıklılık (fail-open)

**Hedef**: broker/worker down submit'i bozmaz; moderasyon geç çalışır.
**Bağımsız test**: broker down iken submit başarılı + yorum Visible; broker dönünce istek relay edilir.

- [x] T021 [US3] Doğrula: `SubmitReview` `[Transactional]` handler'ında yayın yapıyor → Wolverine+Marten transactional outbox devrede (broker down → reviewsDb commit olur, mesaj outbox'ta bekler). Senkron broker bağımlılığı yok.
- [x] T022 [US3] Doğrula: tüketici-binding kuralı — worker `Queues.Worker`'ı, Reviews `Queues.Reviews`'i AÇILIŞTA bağlar (007 dersi); AppHost `WaitFor(rabbit)` ile broker önce ayakta.

**Checkpoint**: Kesinti senaryosu (quickstart Senaryo 3) elde edilir.

---

## Phase 6: Polish & doğrulama (cross-cutting)

- [x] T023 `dotnet build` — tüm çözüm 0 hata.
- [x] T024 `dotnet test tests/Reviews.Api.Tests/Reviews.Api.Tests.csproj` — tümü PASS (aggregate/VO değişmedi).
- [x] T025 [P] Statik doğrulama (FR-002/SC-001): `grep -rn "Microsoft.Agents.AI\|ChatClientAgent\|OpenAIClient" src/services/reviews/Reviews.Api --include=*.cs` boş + csproj'da OpenAI refi yok.
- [ ] T026 OpenAI user-secret'ı worker'a taşı (`dotnet user-secrets set OpenAI:ApiKey/Model --project src/agents/Reviews.Moderation`); Reviews'in OpenAI secret'ı kaldır.
- [ ] T027 Canlı smoke (Aspire, quickstart): temiz yorum Visible; sakıncalı yorum Hidden + özet; ürüne sert küfürsüz yorum Visible; broker-down submit dayanır; metinsiz yorum isteği yayınlamaz.
- [x] T028 [P] `CLAUDE.md` BC haritası/agent notu güncelle: `reviews-moderation-agent` ekle; Reviews satırı "moderasyon broker üzerinden ayrı agent'a" olarak; `src/agents` listesine ekle.

---

## Bağımlılıklar & sıra

- **Setup (P1)** → **Foundational (P2, bloklayıcı)** → **US1 (P3)** + **US2 (P4)** → **US3 (P5, doğrulama)** → **Polish (P6)**.
- US2 (T017 tüketici) US1'in worker'ının `ReviewModerated` yayınına canlı-testte bağlıdır; kod olarak Phase 2 event'leriyle bağımsız yazılabilir.
- US3 çoğunlukla mevcut outbox'tan düşer (doğrulama ağırlıklı).

## Paralel fırsatlar

- T006 ∥ (T001-T005 sonrası). T007 ∥ T008. T009 ∥ T010. T025 ∥ T028.
- US1 dosya-taşıma (worker) ile US2 Reviews-tarafı büyük ölçüde farklı dosyalar → Phase 2 bitince kısmi paralel.

## MVP kapsamı

- **MVP = Phase 1–4 (US1 + US2)**: agent izolasyonu + çalışan broker-tabanlı moderasyon. US1 tek başına
  izolasyonu kanıtlar ama moderasyonu işlevsiz bırakır → US2 ile birlikte anlamlı. US3 outbox'tan düşer,
  Polish canlı doğrular.
