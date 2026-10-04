# Tasks: Fiyat Alarmı + Mail Bildirimi

**Input**: Design documents from `/specs/060-price-alarm-mail/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: İLKE VI — saf domain (`PriceAlarm`) test-first ZORUNLU; handler/endpoint/UI/altyapı canlı doğrulama.

**Organization**: User story bazlı; her story bağımsız teslim edilebilir.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (proje iskeleti + orkestrasyon)

- [X] T001 `Directory.Packages.props`'a `MailKit` PackageVersion ekle (yalnız props — CPM kuralı)
- [X] T002 [P] `src/services/library/Library.Api/` iskeleti: csproj (Common/Shared/ServiceDefaults ref), `GlobalUsings.cs`, `Properties/launchSettings.json` (TUZAK: şart), `Constants/LibraryResourceConstants.cs` (boş sınıf)
- [X] T003 [P] `src/agents/NotificationAgent/` iskeleti (Reviews.Moderation şablonu): csproj (ServiceDefaults+Shared ref, Agents.AI + Agents.AI.Workflows + Extensions.AI.OpenAI paketleri), `GlobalUsings.cs`, `Properties/launchSettings.json`, user-secrets Id
- [X] T004 [P] `src/agents/Mail.Mcp/` iskeleti: csproj (ModelContextProtocol.AspNetCore + MailKit + ServiceDefaults), `GlobalUsings.cs`, `Properties/launchSettings.json`
- [X] T005 `ECommerceAgent.slnx`'e 3 yeni proje + `tests/Library.Api.Tests` girişleri; test projesi iskeleti (xUnit+Shouldly, Library.Api ref)
- [X] T006 `src/aspire/AppHost/AppHost.cs`: `libraryDb` + `mailpit` container (`axllent/mailpit`, SMTP 1025 endpoint + HTTP 8025 endpoint) + `library-api` (libraryDb+rabbit+identityServer ref/WaitFor) + `mail-mcp` (mailpit SMTP endpoint'i env: `Smtp__Host`/`Smtp__Port`) + `notification-agent` (rabbit + mail-mcp ref)

---

## Phase 2: Foundational (kontratlar + BC gövdesi — story'leri bloklar)

- [X] T007 `src/others/Shared/IntegrationEvents.cs`: `ProductChangedEvent`'e `decimal? OldPrice = null` (additive) + `PriceAlarmTriggered` + `NotificationSent` record'ları (contracts/integration-events.md birebir)
- [X] T008 [P] `src/others/Shared/RabbitMqConstants.cs`: `ProductChanged.Queues.Library="library.events"`, `PriceAlarmTriggered` (exchange `library.price-alarm-triggered`, queue `notifications.price-alarm-triggered`), `NotificationSent` (exchange `notifications.sent`, queue `library.notifications-sent`)
- [X] T009 [P] Scope'lar: `src/others/Common/Utils/Constants/AuthorizationScopes.cs`'e `LibraryRead`/`LibraryWrite`; `Identity.Server/Config.cs` `ScopeResources`+`BffServiceScopes`; `Identity.Server/Rbac/KnownScopes.cs` `Descriptions`
- [X] T010 `src/services/library/Library.Api/Program.cs`: Marten (`libraryDb`, şema `library`, IntegrateWithWolverine, ApplyAllDatabaseChangesOnStartup) + Wolverine RabbitMQ (tüketici-binding: `product.changed`→`library.events` + `notifications.sent`→`library.notifications-sent`; `PriceAlarmTriggered` exchange deklare + publish; `ListenToRabbitQueue`; `opts.Discovery.IncludeType<LibraryEventHandlers>` TUZAK) + `AddAuthenticationAndAuthorizationExtension(config, LibraryRead, LibraryWrite)` + ScopeAuthorizationMiddleware + Scalar

**Checkpoint**: `dotnet build` temiz; AppHost'ta library-api/mail-mcp/notification-agent/mailpit yeşil (içerik boş olsa da).

---

## Phase 3: User Story 1 - Alarm kurma ve kaldırma (P1) 🎯 MVP

**Goal**: Login'li kullanıcı detaydan alarm kurar/kaldırır; anonim login'e yönlenir; aynı ürüne tek alarm.

**Independent Test**: quickstart.md Senaryo 1 — kur → "Alarm Kurulu"; kaldır → ilk hâl; anonim → login + geri dönüş.

### Tests for User Story 1 (Domain-TDD — ÖNCE, FAIL etmeli)

- [X] T011 [US1] `tests/Library.Api.Tests/PriceAlarmTests.cs`: `Create` başarı + boş userId/productId red + fiyat ≤0 red + email boş kabul (snapshot, doğrulama yok) — FAIL doğrula

### Implementation for User Story 1

- [X] T012 [US1] `Library.Api/Domains/PriceAlarms/PriceAlarm.cs`: `AggregateRoot` + statik `Create` (`ResultDomain<PriceAlarm>`); alanlar data-model.md — testler YEŞİL
- [X] T013 [P] [US1] `Library.Api/Constants/LibraryResourceConstants.cs`: `PriceAlarmNotFound`, `PriceAlarmInvalid` sabitleri
- [X] T014 [US1] `Domains/PriceAlarms/Features/Commands/CreatePriceAlarm.cs`: `[Transactional]`, `[RequiredScope(LibraryWrite)]`; mevcut (UserId+ProductId) kaydı varsa idempotent Ok (FR-002)
- [X] T015 [P] [US1] `Domains/PriceAlarms/Features/Commands/RemovePriceAlarm.cs`: hard delete (`session.Delete`), yoksa NotFound
- [X] T016 [P] [US1] `Domains/PriceAlarms/Features/Queries/GetPriceAlarmStatus.cs`: `{ Exists }` dönen query (`[RequiredScope(LibraryRead)]`)
- [X] T017 [US1] `Domains/PriceAlarms/PriceAlarmEndpointExtension.cs` + Program map: POST/DELETE/GET `api/v1/library/price-alarms` (contracts/library-api.md; `CurrentUser.Load`, `.RequireAuthorization`)
- [X] T018 [US1] WebApp: `Services/Refit/ILibraryRefitService.cs` + `Services/LibraryService.cs` + `Program.cs` Refit kaydı (`http://library-api`, `AuthenticatedHttpClientHandler`)
- [X] T019 [US1] WebApp `Pages/Products/Detail.cshtml(.cs)`: login'liyse alarm durumu yükle; "Fiyat Alarmı Ekle"/"Alarmı Kaldır" düğmesi + `OnPost` handler'ları (email cookie claim'inden komuta — R3); anonimde `/Auth/SignIn?returnUrl=/products/{id}`

**Checkpoint**: quickstart Senaryo 1 canlı PASS — US1 tek başına teslim edilebilir.

---

## Phase 4: User Story 2 - Fiyat değişince mail gelir (P1)

**Goal**: Her fiyat değişiminde alarmlı her kullanıcıya kişisel mail (Mailpit'te); alarm yaşar.

**Independent Test**: quickstart Senaryo 2 — fiyat değiştir → ≤1 dk mail (ad+iki fiyat+link, Türkçe); ikinci değişim → ikinci mail; fiyat-dışı değişiklik → mail yok.

### Implementation for User Story 2

- [X] T020 [P] [US2] `Catalog.Api/.../Features/Commands/UpdateProduct.cs`: fiyat değiştiğinde yayınlanan `ProductChangedEvent`'e `OldPrice` doldur (değişmediyse null)
- [X] T021 [US2] `Library.Api/LibraryEventHandlers.cs` (ProductChanged kısmı): `OldPrice.HasValue && NewPrice != OldPrice` ise ürünün TÜM alarmları için alarm başına `PriceAlarmTriggered` yayınla (alarm mutasyonu YOK — yaşayan abonelik); alarm yoksa sessiz
- [X] T022 [P] [US2] Mail.Mcp: `Options/SmtpOptions.cs` (Host/Port/From, BindConfiguration+ValidateOnStart) + `Program.cs` (`AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` + `MapMcp("/mcp")`) + `MailTools.cs` `send_mail(to, subject, bodyHtml)` — MailKit SMTP, 3 param zorunlu, dönüş `"sent:<id>"`, hatada exception
- [X] T023 [P] [US2] NotificationAgent temel: `Options/NotificationOptions.cs` (OpenAI ApiKey/Model fail-fast) + `NotificationException.cs` + `Program.cs` (Wolverine: `library.price-alarm-triggered` exchange'ine `notifications.price-alarm-triggered` binding + listen; `NotificationSent` exchange deklare + publish; `OnException<NotificationException>().RetryWithCooldown(10s,30s,60s).Then.MoveToErrorQueue()`; `IncludeType<PriceAlarmEventHandlers>`)
- [X] T024 [US2] `NotificationAgent/NotificationMailAgent.cs` (KARAR DEĞİŞTİ 2026-09-02: MAF Workflows yerine ChatAgent/ModerationAgent deseni — düz singleton agent): email boş → skip; Compose (ChatClientAgent structured `MailDraft`; LLM hatasında yedek şablon, exception YOK); Send (minik ChatClientAgent + Mail.Mcp tool'ları, LLM tool-seçimi; başarısızlık outcome'a)
- [X] T025 [US2] `NotificationAgent/PriceAlarmEventHandlers.cs`: `Handle(PriceAlarmTriggered)` → agent'ı koş → SendFailed'de `NotificationException` → değilse `NotificationSent(UserId, ProductId, Email, Success, Detail)` cascade-return

**Checkpoint**: quickstart Senaryo 2 canlı PASS (mail içerik birebir + ikinci mail + hata yolu retry/error-queue).

---

## Phase 5: User Story 3 - Bildirim izi (P3)

**Goal**: Her gönderim denemesi kalıcı iz (FR-007).

**Independent Test**: quickstart Senaryo 3 — mail sonrası `libraryDb.library` şemasında `NotificationRecord` satırı.

### Implementation for User Story 3

- [X] T026 [US3] `Library.Api/Domains/PriceAlarms/Entities/NotificationRecord.cs` (davranışsız doküman) + `LibraryEventHandlers.cs`'e `Handle(NotificationSent)` → kayıt yaz

**Checkpoint**: üç story bağımsız çalışır durumda.

---

## Phase 6: Polish & Cross-Cutting

- [X] T027 [P] `src/services/library/FLOW.md`: BC tek cümle + süreç (alarm kur → fiyat değişimi dinle → tetik yayınla → iz yaz) + domain kuralları + sınır; kenar-anchor tip adları (İLKE VII)
- [X] T028 [P] `src/agents/NotificationAgent/FLOW.md`: tetik al → Enrich→Decide→Compose→Send→Outcome → iz yayınla (Reviews.Moderation FLOW.md emsali)
- [X] T029 `CLAUDE.md`: BC haritasına `library` satırı (Origin: `specs/060-price-alarm-mail`) + NotificationAgent/Mail.Mcp notu (ModerationAgent maddesi yanına)
- [X] T030 Doğrulama (2026-09-02: build+test+iki guard PASS; canlı: alarm kur + fiyat değişimi maili Mailpit'te PASS — kullanıcı kabulü; edge senaryolar [ikinci mail/kaldır/hata yolu] atlandı): `dotnet build` + `dotnet test` + `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` + quickstart.md TÜM senaryolar canlı (Mailpit UI dahil); sonuçları spec'e işaretle

---

## Dependencies & Execution Order

- **Phase 1 → 2 → story'ler**: T007–T010 tüm story'leri bloklar (kontrat + BC gövdesi).
- **US1 (Phase 3)**: yalnız Foundational'a bağlı — MVP; T011 (test) → T012 (aggregate) sırası ZORUNLU (İLKE VI).
- **US2 (Phase 4)**: Foundational'a bağlı; canlı testi alarm kaydı ister → US1 sonrası koşmak pratik. T020/T022/T023 birbirinden bağımsız [P]; T024 → T023'e, T025 → T024'e bağlı.
- **US3 (Phase 5)**: `NotificationSent` yayınına (T023/T025) bağlı; kod tarafı bağımsız yazılabilir.
- **Polish**: hepsinden sonra; T027/T028 paralel.

## Parallel Example: Phase 4

```text
Aynı anda: T020 (Catalog OldPrice) + T022 (Mail.Mcp) + T023 (NotificationAgent temel)
Sonra sıra: T024 (workflow) → T025 (handler)
```

## Implementation Strategy

- **MVP**: Phase 1+2+3 (US1) → quickstart Senaryo 1 canlı doğrula → commit.
- Sonra US2 (değerin tamamlandığı nokta) → Senaryo 2 → commit; US3 küçük → Senaryo 3; Polish + guard'lar.
- Her checkpoint'te commit; FLOW.md/CLAUDE.md aynı PR'da (İLKE VII).