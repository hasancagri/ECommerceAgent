# Implementation Plan: Dead-Letter İzleme / Replay / Alarm

**Branch**: `088-dead-letter-observability` | **Date**: 2026-10-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/088-dead-letter-observability/spec.md`

## Summary

Her BC'nin Wolverine durable message store'u, retry tükenince başarısız mesajı kendi
`<schema>.wolverine_dead_letters` tablosuna taşıyor (BC-izole, 12 şema). Üstünde yakalama-garantisi +
operatör yüzeyi + gözlemlenebilirlik + alarm YOK. Yaklaşım: native Wolverine 6.4.1 dead-letter API'sini
(`IDeadLetters` + `IMessageTracker.MovedToErrorQueue`) ince sarmala — yeni depo/BC açma. Paylaşılan
`Common` katmanında bir admin MCP tool tipi + bir gözlemci; `Shared`'da bir eşik-event; NotificationAgent
alarm mailini mevcut 060 boru hattıyla gönderir. Merkezi DLQ tablosu yok; birleştirme AgentPlatform
fasadında.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`+`ImplicitUsings`)

**Primary Dependencies**: WolverineFx 6.4.1 (+ .Marten/.Postgresql/.RabbitMQ), Marten, ModelContextProtocol
1.4.0 (`.WithTools<T>()` / `.WithToolsFromAssembly`), Microsoft.Extensions.AI (NotificationAgent),
OpenTelemetry (ServiceDefaults)

**Storage**: Her BC kendi Postgres'i; dead-letter = mevcut `wolverine_dead_letters` tablosu (BC şemasında,
`.IntegrateWithWolverine()` default). Yeni tablo YOK.

**Testing**: xUnit + Shouldly. Saf birim = gözlemci eşik/throttle mantığı (test-first, İlke VI). Tool
wrapper + policy + consumer = canlı/entegrasyon doğrulama.

**Target Platform**: Linux konteyner, Aspire AppHost orkestrasyonu

**Project Type**: Çok-servisli mikroservis sistemi (web-service × 12 + 1 worker + shared + host)

**Performance Goals**: Operatör dead-letter varlığından detaya tek yüzey çağrısıyla ulaşır; listeleme
limit/sayfalı (sınırsız dump yok). Gözlemci hot-path dışı (yalnız ölüm anında).

**Constraints**: Ham mesaj gövdesi yüzeye/log'a DÖNMEZ (yalnız metadata+exception). Alarm maili
best-effort (mesaj işlemeyi bloke etmez). BC izolasyonu korunur — merkezi depo yok.

**Scale/Scope**: 12 BC servisi + `Common` + `Shared` + NotificationAgent + ServiceDefaults + AppHost.
Yeni scope `ops.deadletter`. Yeni event `DeadLetterThresholdReached`. 4 MCP tool.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **I. BC İzolasyonu (NON-NEGOTIABLE)** — ✓ Her servis YALNIZ kendi dead-letter store'unu sorgular (kendi
  Wolverine runtime'ı/`IDeadLetters`). `Common`'daki tool/gözlemci = taşınabilir **altyapı**, domain modeli
  değil — BC modeli sızması yok. BC-arası tek kanal: `Shared.IntegrationEvents` eşik-event'i (sanksiyonlu)
  + fasat birleştirme. Merkezi DLQ tablosu açılmıyor.
- **II. Zengin Aggregate** — N/A. Domain aggregate YOK; bu çapraz-kesen operasyonel altyapı. Dead-letter
  envelope = Wolverine'in framework tipi, bizim aggregate'imiz değil.
- **III. VSA+CQRS, Repository Yok** — Kısmi sapma (bkz. Complexity Tracking): DLQ tool'ları `Domains/`
  altında domain slice DEĞİL (kullanıcı/domain feature'ı değil, ops infra) → `Common`'da paylaşılan tool.
  Yine de Result pattern + `[RequiredScope]` + ince-sarmalayıcı korunur; repository yok (`IDeadLetters`
  doğrudan).
- **IV. Result Pattern** — ✓ Tool handler'ları `FeatureObjectResultModel<T>`/`FeatureListResultModel`/
  `FeatureResultModel` döner; hata kodu resource sabiti.
- **V. Scope Yetki** — ✓ Yeni `ops.deadletter` scope (`AuthorizationScopes`), her tool `[RequiredScope]`;
  rol→scope map DB'de (AgentPlatform). Son savunma 403.
- **VI. Domain-TDD** — ✓ Tek saf mantık = gözlemci pencere-sayaç/throttle → test-first. Gerisi altyapı
  (test-sonra/canlı).
- **VII. FLOW.md Legibility** — ✓ Hiçbir BC'nin domain süreci değişmiyor (command-event-policy sırası aynı)
  → FLOW.md güncellemesi gerekmez. Guard tetiklenmez.

**Sonuç:** III dışında tüm gate'ler temiz; III sapması Complexity Tracking'de gerekçeli. GEÇER.

## Project Structure

### Documentation (this feature)

```text
specs/088-dead-letter-observability/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — API kararları + belirsizlik çözümü
├── data-model.md        # Phase 1 — envelope görünümü + event + config
├── quickstart.md        # Phase 1 — uçtan-uca doğrulama senaryosu
├── contracts/           # Phase 1 — MCP tool + integration event sözleşmeleri
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/others/Common/Utils/
├── DeadLetters/
│   ├── DeadLetterAdminTools.cs        # [McpServerToolType] — list/get/replay/discard (IDeadLetters sarmalar)
│   └── DeadLetterObserver.cs          # IMessageTracker.MovedToErrorQueue → log + metrik + eşik-event
├── Constants/AuthorizationScopes.cs   # + OpsDeadletter sabiti
└── Diagnostics/DeadLetterMetrics.cs   # Meter "ECommerceAgent.DeadLetters" + dead_letter_total counter

src/others/Shared/
├── IntegrationEvents.cs               # + DeadLetterThresholdReached record
├── RabbitMqConstants.cs               # + DeadLetterThresholdReached exchange/queue adları
├── McpToolNames.cs                    # + DeadLetterAdminTools adları
└── McpToolDescriptions.cs             # + DeadLetterAdminTools açıklamaları (standart)

src/services/<her-bc>/<Bc>.Api/
├── Program.cs                         # + .WithTools<DeadLetterAdminTools>() + Meter kaydı + ops.deadletter scope
└── Extensions/MessagingExtensions.cs  # global OnException policy + DeadLetterObserver DI + eşik-event publish

src/agents/NotificationAgent/
├── DeadLetterAlarmHandlers.cs         # DeadLetterThresholdReached → MailAgent → NotificationSent
├── MailAgent.cs                       # + SendDeadLetterAlarmMailAsync
└── Extensions/MessagingExtensions.cs  # eşik-event binding + listen + IncludeType

src/aspire/ServiceDefaults/Extensions.cs   # .WithMetrics(... AddMeter("ECommerceAgent.DeadLetters"))
```

**Structure Decision**: Mevcut çok-servis yapısı korunur. Çapraz-kesen kod `src/others/Common` (taşınabilir
altyapı) + `src/others/Shared` (sözleşmeler); BC başına yalnız kayıt satırı (`Program.cs`/
`MessagingExtensions.cs`). Yeni proje/servis YOK (eski merkezi-izleme-BC fikri reddedildi — bkz. research).

## Complexity Tracking

| İhlal | Neden gerekli | Reddedilen daha-basit alternatif |
|-------|---------------|-----------------------------------|
| DLQ tool'ları `Domains/` yerine `Common`'da (İlke III slice konumu) | Dead-letter yönetimi herhangi bir BC'nin domain'i değil; çapraz-kesen operasyonel altyapı. 12 BC'de birebir kopya = bilinçli-tekrar sınırını aşan bakım yükü. Tek paylaşılan infra tool, `IDeadLetters`'ı her servisin kendi runtime'ından çözer (DB izolasyonu korunur). | Her BC'ye ayrı `Domains/DeadLetters/` slice: 12× kopya, domain olmayan kavramı 12 aggregate klasörüne sokar, İlke II/III'ü daha çok zorlar. |
| `DeadLetterObserver` süreç-güdümlü ama `Process/` yerine `Common` altyapısı | Tek BC'nin süreci değil; TÜM servislerin paylaştığı framework-kancası (IMessageTracker). | Her BC'de ayrı Process/ sınıfı: 12× aynı kanca kodu. |
