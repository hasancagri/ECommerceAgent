# Implementation Plan: Fiyat Alarmı + Mail Bildirimi

**Branch**: `060-price-alarm-mail` | **Date**: 2026-09-02 | **Spec**: `specs/060-price-alarm-mail/spec.md`

**Input**: Feature specification from `/specs/060-price-alarm-mail/spec.md`

## Summary

Kullanıcı ürün detayından fiyat alarmı kurar (yaşayan abonelik — kaldırılana dek her fiyat değişiminde mail). Yeni **Library BC** (`Library.Api`, kitaplık alanının ilk dilimi) alarmı saklar, Catalog `ProductChangedEvent`'ini (additive `OldPrice` ile) dinler, alarm başına `PriceAlarmTriggered` yayınlar. DB'siz **NotificationAgent** (MAF Workflows: Enrich→Decide→Compose→Send→Outcome) maili LLM'le kişiselleştirir, **Mail.Mcp** `send_mail` tool'u üzerinden (Send adımının minik agent'ı, LLM tool-seçimiyle) **Mailpit**'e gönderir; `NotificationSent` izini Library tüketip kaydeder. Kararların tamamı: `research.md` (R1–R10).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings`)

**Primary Dependencies**: Marten 9.5.0, WolverineFx(.RabbitMQ) 6.4.1, Microsoft.Agents.AI 1.13.0 + **Microsoft.Agents.AI.Workflows 1.13.0 (repo'da ilk kullanım; `Executor` + `ConfigureProtocol`, `ReflectingExecutor` obsolete)**, ModelContextProtocol(.AspNetCore) 1.4.0, **MailKit (YENİ — props'a eklenecek)**, Aspire

**Storage**: Postgres `libraryDb` / şema `library` (Marten, otomatik şema); NotificationAgent + Mail.Mcp DB'siz

**Testing**: xUnit + Shouldly — saf domain (`PriceAlarm`) test-first; handler/endpoint/UI canlı doğrulama (`quickstart.md`)

**Target Platform**: Aspire AppHost orkestrasyonu (dev: macOS/Docker)

**Project Type**: Mikroservis (yeni BC + 2 agent projesi + WebApp dokunuşu)

**Performance Goals**: SC-002 — fiyat değişimi → mail ≤1 dk

**Constraints**: Aynı değişim için bilinçli mükerrer mail sıfır (at-least-once retry istisnası kabul); email yoksa gönderim atlanır, iz düşer

**Scale/Scope**: Tek kullanıcılı dev/öğrenme ortamı; ölçek hedefi yok

## Constitution Check

*GATE: Phase 0 öncesi + Phase 1 sonrası değerlendirildi — İHLAL YOK.*

- **İLKE I (BC izolasyonu): PASS.** Library kendi DB'si; iletişim yalnız fanout event (`product.changed`, `library.price-alarm-triggered`, `notifications.sent`). Email alarm kuruluşunda snapshot — worker kimseye sormaz (R3). MCP'yi yalnız agent tüketir: Mail.Mcp'yi Send adımının agent'ı LLM'le çağırır (R7).
- **İLKE II (zengin aggregate): PASS (bilinçli sınırda).** `PriceAlarm` fabrika-doğrulamalı kayıt aggregate'i; yaşayan-abonelik kararı tetik mutasyonunu kaldırdı, v1'de mutator yok (data-model). Favori/listeler gelince davranış büyür.
- **İLKE III (VSA+CQRS, repo yok): PASS.** `Domains/PriceAlarms/Features/Commands|Queries` slice'ları; handler doğrudan `IDocumentSession`; Minimal API + `*EndpointExtension`.
- **İLKE IV (Result): PASS.** `FeatureResultModel`/`ResultDomain` + `LibraryResourceConstants`.
- **İLKE V (scope): PASS.** `library.read`/`library.write` KnownScopes kapalı registry'ye; WebApp BFF scope'ları; `customer` rol demetine admin ekranından.
- **İLKE VI (Domain-TDD): PASS.** `PriceAlarm.Create` guard testleri implementasyondan ÖNCE (`tests/Library.Api.Tests`).
- **İLKE VII (FLOW.md): PASS.** `src/services/library/FLOW.md` + `src/agents/NotificationAgent/FLOW.md` aynı PR'da; Catalog süreci değişmiyor (event'e alan eklemek süreç değişikliği değil).

## Project Structure

### Documentation (this feature)

```text
specs/060-price-alarm-mail/
├── plan.md              # bu dosya
├── research.md          # R1–R10 kararlar + tuzaklar
├── data-model.md        # PriceAlarm, NotificationRecord, event alanları
├── quickstart.md        # canlı doğrulama senaryoları
├── contracts/
│   ├── integration-events.md   # ProductChangedEvent+OldPrice, PriceAlarmTriggered, NotificationSent
│   ├── library-api.md          # REST + scope'lar + WebApp Refit
│   └── mail-mcp.md             # send_mail tool + SmtpOptions + Mailpit
└── tasks.md             # /speckit-tasks üretecek
```

### Source Code (repository root)

```text
src/services/library/
├── FLOW.md                                  # İLKE VII (yeni BC)
└── Library.Api/
    ├── Program.cs                           # Marten(libraryDb) + Wolverine(RabbitMQ) + auth(library.*)
    ├── Properties/launchSettings.json       # TUZAK: şart
    ├── GlobalUsings.cs
    ├── Constants/LibraryResourceConstants.cs
    ├── LibraryEventHandlers.cs              # ProductChangedEvent → tetik; NotificationSent → iz (IncludeType!)
    └── Domains/PriceAlarms/
        ├── PriceAlarm.cs                    # aggregate (Create fabrikası)
        ├── PriceAlarmEndpointExtension.cs
        ├── Entities/NotificationRecord.cs   # iz dokümanı (aggregate değil)
        └── Features/
            ├── Commands/CreatePriceAlarm.cs, RemovePriceAlarm.cs
            └── Queries/GetPriceAlarmStatus.cs

src/agents/NotificationAgent/                # DB'siz worker (Reviews.Moderation şablonu)
├── Program.cs                               # Wolverine dinleyici + retry→error queue + Workflow DI
├── Properties/launchSettings.json
├── Options/NotificationOptions.cs           # OpenAI fail-fast
├── FLOW.md
├── NotificationWorkflow.cs                  # MAF Workflows: Enrich→Decide→Compose→Send→Outcome
├── PriceAlarmEventHandlers.cs               # PriceAlarmTriggered → workflow → NotificationSent (cascade)
└── NotificationException.cs

src/agents/Mail.Mcp/                         # ilk standalone MCP server
├── Program.cs                               # AddMcpServer + MapMcp("/mcp")
├── Properties/launchSettings.json
├── Options/SmtpOptions.cs
└── MailTools.cs                             # send_mail (MailKit → Mailpit)

Değişen mevcut dosyalar:
├── src/others/Shared/IntegrationEvents.cs   # OldPrice (additive) + 2 yeni event
├── src/others/Shared/RabbitMqConstants.cs   # yeni exchange/queue adları
├── src/services/catalog/.../UpdateProduct.cs# OldPrice doldur
├── src/others/Identity.Server/{Config,Rbac/KnownScopes}.cs + Common/AuthorizationScopes.cs  # library.*
├── src/aspire/AppHost/AppHost.cs            # libraryDb + 3 proje + mailpit container
├── Directory.Packages.props                 # MailKit
├── ECommerceAgent.slnx         # 3 yeni proje
├── src/ui/WebApp/                           # ILibraryRefitService + Detail sayfası düğme + handler'lar
└── CLAUDE.md                                # BC haritasına library satırı + NotificationAgent/Mail.Mcp

tests/Library.Api.Tests/                     # PriceAlarm domain testleri (test-first)
```

**Structure Decision**: Mevcut düzen korunur — BC `src/services/*`, agent projeleri `src/agents/*` (Reviews.Moderation emsali), kontratlar `Shared`'da. Gateway route ve Library MCP endpoint'i YOK (R10, JIT).

## Complexity Tracking

İhlal yok — tablo boş.