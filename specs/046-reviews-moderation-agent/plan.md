# Implementation Plan: Reviews Moderasyon Agent'ını Ayrı Broker-Tabanlı Worker'a Taşı

**Branch**: `046-reviews-moderation-agent` | **Date**: 2026-08-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/046-reviews-moderation-agent/spec.md`

## Summary

Reviews.Api içindeki in-proc `ModerationAgent` (Microsoft.Agents.AI/ChatClientAgent), `src/agents/`
altında DB'siz ayrı bir worker servisine (`Reviews.Moderation`) taşınır. Reviews ile worker yalnız
RabbitMQ fanout event'leriyle konuşur: `ReviewModerationRequested` (Reviews→worker) ve `ReviewModerated`
(worker→Reviews). Submit yolu mevcut Wolverine+Marten transactional outbox sayesinde broker-dayanıklı
kalır (post-moderation, fail-open). Satın-alma-kanıtı, aggregate, özet hesaplama ve Storefront yayını
DEĞİŞMEZ. Reviews.Api kaynağında agent-framework/OpenAI kalmaz.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Wolverine (in-proc bus + RabbitMQ), Marten (yalnız Reviews tarafı; worker DB'siz),
Microsoft.Agents.AI + Microsoft.Extensions.AI.OpenAI (yalnız worker), Shared.IntegrationEvents kontratı

**Storage**: reviewsDb (Reviews, değişmez); **worker DB'siz** (stateless)

**Testing**: xUnit + Shouldly (Reviews.Api.Tests — mevcut domain testleri); canlı smoke Aspire

**Target Platform**: Linux/dev; Aspire AppHost ile tam-stack

**Project Type**: Mikroservis (BC) + agent worker servisi (ChatAgent emsali, DB'siz)

**Performance Goals**: Moderasyon async/best-effort; submit yolu broker'a senkron bağımlı değil

**Constraints**: Fail-open (broker/worker down submit'i bozmaz); PII yok (yalnız metin+yıldız+id);
post-moderation (yorum anında Visible)

**Scale/Scope**: Küçük yorum hacmi; tek yeni proje + 2 event + Reviews'te handler swap + silmeler

## Constitution Check

*GATE: Phase 0 öncesi geçmeli. Phase 1 sonrası tekrar bakılır.*

- **İLKE I (BC İzolasyonu) — PASS.** Worker `src/agents/` altında, kendi DB'si YOK, reviewsDb'ye
  ERİŞMEZ. Reviews↔worker iletişimi yalnız **integration event** (RabbitMQ fanout) — sanksiyonlu kanal.
  İki event `Shared.IntegrationEvents`'te (bilinçli sözleşme). DB-siz agent servisi = **ChatAgent emsali**.
  Moderasyon KARARI (gizle) hâlâ `Review.ApplyModeration` aggregate'inde uygulanır → domain otoritesi
  Reviews'te kalır; worker yalnız sınıflandırıcı verdict üretir.
- **İLKE I (MCP yalnız agent) — PASS.** Worker MCP TÜKETMEZ (structured-output agent, MCP'siz). Kimse
  imperatif CallToolAsync sürmez.
- **İLKE II (Zengin Aggregate) — PASS.** Yeni aggregate yok; `Review` + `ModerationVerdict` VO değişmez.
  Worker durumsuz (aggregate yok, ChatAgent gibi).
- **İLKE III (VSA/CQRS, Repository yok) — PASS.** Reviews'te `ReviewModerated` bir event handler'la
  tüketilir; worker'da `ReviewModerationRequested` bir handler'la. Repository yok; Marten IDocumentSession
  doğrudan (Reviews). Slice-arası çağrı IMessageBus.
- **İLKE IV (Result) — PASS.** `ApplyModeration` `ResultDomain` döner (değişmez). Worker verdict'i event'e
  yazar (Result-pattern domain akışı değil, LLM çıktısı → event).
- **İLKE V (Scope) — PASS.** Yeni HTTP yüzeyi yok; worker makine-içi mesaj tüketir. Yeni scope gerekmez.
- **İLKE VI (Domain-TDD) — PASS (yeni domain yok).** Taşınan LLM çağrısı + handler = altyapı (test-sonra/
  canlı). `Review`/`ModerationVerdict` domain'i zaten test-first yazılmış, değişmiyor. Yeni test-first birim yok.
- **Teknoloji kısıtları — PASS.** .NET 10, Wolverine+RabbitMQ, CPM (paketler props'ta), Scrutor DI,
  GlobalUsings, Aspire AppHost, agent tipi **Singleton**. Worker hepsine uyar.

**Sonuç: Tüm kapılar GEÇER. Complexity Tracking gerekmez (ihlal yok).**

Not (tasarım gerilimi, ihlal değil): moderasyon prompt'u (politika) worker'a taşınır. Karşı-argüman:
state-transition kararı (Visible→Hidden) `Review` aggregate'inde kalır; worker yalnız içerik-sınıflandırıcı.
Bu, kullanıcının açık hedefi (agent-framework BC'de olmasın) doğrultusunda bilinçli bir seçimdir.

## Project Structure

### Documentation (this feature)

```text
specs/046-reviews-moderation-agent/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1 (event + verdict şekilleri)
├── quickstart.md        # Phase 1 (canlı smoke rehberi)
├── contracts/
│   └── moderation-events.md   # iki integration event sözleşmesi
├── checklists/requirements.md # specify çıktısı
└── tasks.md             # /speckit-tasks (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/agents/Reviews.Moderation/            # YENİ worker servisi (DB'siz, AppHost resource)
├── Reviews.Moderation.csproj             # Sdk.Web; Wolverine+RabbitMQ+OpenAI+Shared; ServiceDefaults
├── Program.cs                            # Wolverine: requested tüket (kendi kuyruğunu bağla) → moderated yayınla; retry→error queue
├── GlobalUsings.cs
├── ModerationAgent.cs                    # Reviews'ten TAŞINIR (ChatClientAgent, Temp=0, structured JSON)
├── ModerationException.cs                # TAŞINIR
├── Options/ModerationOptions.cs          # TAŞINIR (section "OpenAI", fail-fast)
└── Features/ModerateReviewRequest.cs     # handler: ReviewModerationRequested → agent → ReviewModerated

src/others/Shared/
├── IntegrationEvents.cs                  # +ReviewModerationRequested, +ReviewModerated
└── RabbitMqConstants.cs                  # +iki exchange/queue sabiti

src/services/reviews/Reviews.Api/
├── Program.cs                            # ModerateReview local-queue + ModerationAgent kayıtları SİL;
│                                         #   ReviewModerationRequested yayınla + ReviewModerated tüket (binding)
├── Reviews.Api.csproj                    # OpenAI/Microsoft.Agents.AI paket refs SİL
├── Domains/Reviews/Features/Commands/
│   ├── SubmitReview.cs                   # ModerateReview publish → ReviewModerationRequested (metin varsa)
│   └── ModerateReview.cs                 # SİL (handler mantığı taşınır)
├── Domains/Reviews/Features/ (yeni)      # ReviewModerated tüketici handler (ApplyModeration + özet)
├── Infrastructure/Moderation/*           # SİL (ModerationAgent, ModerationException)
└── Options/ModerationOptions.cs          # SİL (worker'a taşındı)

src/aspire/AppHost/
├── AppHost.cs                            # +reviews-moderation-agent resource (rabbit); reviewsApi OpenAI bağı kalkar
└── AppHost.csproj                        # +ProjectReference Reviews.Moderation

ECommerceAgent.slnx          # +Reviews.Moderation projesi (src/agents/ klasörü)
```

**Structure Decision**: Yeni worker `src/agents/` altında (kullanıcının ilkesi: agent yazımı agents/'ta).
DB'siz → BC değil, ChatAgent gibi bir agent process. Reviews BC yapısı korunur; yalnız moderasyon adımı
event sınırının ötesine taşınır.

## Complexity Tracking

> Anayasa Check ihlali yok — bu bölüm boş.
