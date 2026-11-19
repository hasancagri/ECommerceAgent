# Implementation Plan: ScoutAgent Router → Sub-Agent Routing

**Branch**: `019-scout-subagent-routing` | **Date**: 2026-11-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/019-scout-subagent-routing/spec.md`

## Summary

ScoutAgent'ı tek düz `ChatClientAgent "Kasif"`'ten **router + BC-başına 7 sub-agent** mimarisine
çevir. Router (ucuz model) sub-agent'ları MAF `AsAIFunction` ile *tool* olarak görür; LLM hangi
sub-agent'ı çağıracağını seçer (D3 LLM-routing korunur). Her sub-agent YALNIZ kendi `/mcp` ucuna
bağlanır (kör toplama biter), kendi modelini kullanır (model-keyed `IChatClient` factory) ve tipli
çıktı üretir. Mevcut `CapturingFunction`/`ToolResultCollector` (→ `reply.items`) ve
`UserTokenForwardingHandler` (bearer forward) desenleri yeniden kullanılır; BC MCP tool'ları değişmez.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Microsoft.Agents.AI 1.13.0 (`ChatClientAgent`, `AIAgent.AsAIFunction`),
Microsoft.Extensions.AI 10.7.0 (`IChatClient`, `UseFunctionInvocation`, `ChatOptions.ResponseFormat`),
Microsoft.Extensions.AI.OpenAI 10.7.0, ModelContextProtocol 2.3.1 (`McpClient`/`ListToolsAsync`)

**Storage**: Değişmez — login sohbeti Marten (`Conversation`, scoutDb). Bu feature kalıcı şema eklemez;
yalnız agent-içi yapı + Options.

**Testing**: xUnit + Shouldly, saf unit (`tests/ScoutAgent.Api.Tests`). LLM/MCP yolları test edilmez
(İlke VI; mevcut kural). Test edilen saf birimler: endpoint→sub-agent eşleme, model çözümleme,
router tool-kümesi kurulumu, result-merge sırası.

**Target Platform**: Aspire AppHost üstünde servis (service discovery ile BC `/mcp` uçları).

**Project Type**: Agent servisi (tek proje) — `src/agents/ScoutAgent.Api`.

**Performance Goals**: Niyet-dağıtımı ek bir LLM turu ekler; kabul edilen takas (seçici bağlanma +
küçük bağlam). Tek-alanlı turda seçilmeyen 6 uca bağlantı **sıfır** (SC-001).

**Constraints**: MCP-only (İlke I), query-only tool'lar, yetki downstream BC'de scope. Config = Options
pattern (IConfiguration'dan doğrudan okuma yok). Agent tipleri Singleton; kullanıcı token'ı çağrı anında.

**Scale/Scope**: 7 sub-agent + 1 router. Mevcut 8 MCP tool / 7 uç. Yeni BC/tablo/event YOK.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli. Phase 1 sonrası yeniden bakılır.*

- **İlke I (BC izolasyonu, MCP-only agent)**: ✅ Agent hâlâ yalnız MCP konuşur, query-only. Sub-agent'lar
  da MCP tüketir (first-party agent-içi). Senkron RPC/DB erişimi eklenmez. Kör toplama → seçici, sınır aynı.
- **İlke III (VSA + CQRS)**: ✅ Agent altyapısı `Agents/` altında (feature slice değil — mevcut
  `ScoutAgentFactory` konumu). `Domains/` dokunulmaz; chat feature slice (`SendMessage`) korunur.
- **İlke VI (Domain-TDD)**: ✅ Saf birimler (eşleme/model-çözümleme/tool-kümesi/merge-sırası) test-first;
  `tasks.md`'de test task'ları implementasyondan önce. LLM/MCP yolu kapsam dışı (harness yok).
- **İlke VII (FLOW legibility)**: ✅ FLOW.md mini-storm eklendi (planlanan süreç + D3 hotspot open);
  implement bitince hotspot closed + planned ekler düşer (as-built).
- **İlke VIII (sır insan-yüzeyde yok)**: ✅ Sır/PII dönmez; değişiklik yapısal. Etkisiz.
- **Config = Options (CLAUDE.md)**: ✅ Model haritası `ScoutOptions`'a POCO; IConfiguration doğrudan okunmaz.
- **DI = Scrutor**: ⚠️ `IChatClient`/factory `Program.cs`'te elle kaydedilir (orchestration istisnası —
  mevcut tek `IChatClient` zaten elle). Agent'lar Singleton (İlke teknoloji kuralı). İhlal değil.

**Sonuç: Gate geçer — gerekçelendirilecek ihlal YOK.** (Complexity Tracking boş.)

## Project Structure

### Documentation (this feature)

```text
specs/019-scout-subagent-routing/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1 (config/registry — kalıcı aggregate yok)
├── quickstart.md        # Phase 1 (canlı doğrulama senaryoları)
├── contracts/           # Phase 1 (HTTP kontratı DEĞİŞMEZ notu + config şeması)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/agents/ScoutAgent.Api/
├── Agents/
│   ├── ScoutAgentFactory.cs        # router kur; IEnumerable<ISubAgent> enjekte, her birinin lazy tool'u
│   ├── ChatClientFactory.cs        # YENİ: model-keyed IChatClient (UseFunctionInvocation, Scrutor-auto)
│   ├── ScoutModelResolver.cs       # YENİ: saf model çözümleme (Resolve/ResolveRouter)
│   ├── McpToolSource.cs            # endpoint-başına tool getirme (GetToolsForEndpointAsync; kör kaldırıldı)
│   ├── ToolResultCapture.cs        # KORUNUR: CapturingFunction + ToolResultCollector
│   ├── UserTokenForwardingHandler.cs  # KORUNUR
│   └── SubAgents/                  # YENİ: ISubAgent + 7 kendi-kendine-yeten sınıf (teker teker)
│       ├── ISubAgent.cs            # arayüz (ToolName + AsRouterTool)
│       ├── CatalogSubAgent.cs … PersonalizationSubAgent.cs  # her biri Singleton + lazy, kendi prompt'u
├── Options/ScoutOptions.cs         # RouterModel + per-sub-agent model haritası (Agents)
├── Program.cs                      # tek IChatClient kaldırıldı (ChatClientFactory Scrutor-auto)
└── Domains/Conversations/Features/Commands/SendMessage.cs  # router agent'ını çağırır
tests/ScoutAgent.Api.Tests/ModelResolutionTests.cs  # saf model-çözümleme (İlke VI)
src/agents/ScoutAgent.Api/FLOW.md  # GÜNCEL: hotspot closed + as-built (implement sonu)
```

**Structure Decision**: Tek agent servisi. Sub-agent'lar `Agents/SubAgents/`'ta her biri KENDİ sınıfı
(ISubAgent; Scrutor-auto Singleton) — generic data-list DEĞİL (bilinçli tekrar, repo ethos'u). Router
`IEnumerable<ISubAgent>` enjekte alır. Yeni BC/proje yok.

## Complexity Tracking

*(Constitution Check ihlali yok — boş.)*
