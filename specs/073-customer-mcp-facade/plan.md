# Implementation Plan: Tek Müşteri MCP Fasadı

**Branch**: `073-customer-mcp-facade` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/073-customer-mcp-facade/spec.md`

## Summary

Müşterinin kendi AI istemcisinden (Claude Desktop vb.) mağazaya **tek bir MCP kapısından** bağlanmasını
sağlayan izole bir **fasad servisi** (`Mcp.Gateway`, DB'siz). İki korumalı-yüzey uç: `/mcp` (müşteri) +
`/mcp-admin` (yönetim). Fasad, alt BC'lerin (`basket/order/customer/payment/storefront/catalog`) müşteri
MCP tool'larını **oturum anında (lazy)** toplar, isme göre sahibi BC'ye **kullanıcı bearer'ını taşıyarak**
yönlendirir (ChatAgent'ın `PerUserMcpTool` deseninin server-tarafı ikizi). Kimlik OAuth-native: fasad =
OAuth resource, tek PRM + tek consent (yeni seed istemci `external-customer-agent`); giriş **girişsiz gezme
+ anonim sepetten sonra, checkout'ta bir kez (step-up)** tetiklenir; anonim sepet giriş anında kullanıcıya
devredilir. Kart PAN'ı fasaddan geçmez (yeni kart = PG hosted form). Fasad iş mantığı taşımaz (yalnız
keşif + proxy); mevcut BC MCP uçları + ChatAgent iç keşfi değişmez. UCP (REST, dış platform) ayrı kalır.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings`)

**Primary Dependencies**: `ModelContextProtocol.AspNetCore` (fasad = MCP **server**) + `ModelContextProtocol.Core`
(downstream MCP **client**, `McpClient`/`HttpClientTransport`); OpenIddict (yeni seed istemci + tek consent,
mevcut Identity.Server); `Common/McpResourceMetadataExtension` (RFC 9728 PRM, /mcp + /mcp-admin zaten destekli);
YARP gateway (fasada route). ChatAgent `PerUserMcpTool` / tool-collection deseni yeniden kullanılır (kopya/miras).

**Storage**: YOK (DB'siz). Yalnız süreç-içi kısa-TTL tool-katalog cache (opsiyonel, oturum anı keşfi).

**Testing**: xUnit + Shouldly — saf birimler (tool-adı→BC yönlendirme kaydı, yüzey süzme müşteri/admin)
test-first (İlke VI). Discovery/proxy/auth wiring test-sonra + Claude Desktop canlı doğrulama (quickstart).

**Target Platform**: Aspire AppHost üzerinde container servis; YARP gateway arkasında tek dış giriş.

**Project Type**: Agent-facing MCP fasad/proxy servisi (Mail.Mcp emsali — `src/agents`, DB'siz standalone MCP).

**Performance Goals**: Etkileşimli; tool listeleme kullanıcı-algısı anında (kısa cache). Ölçek demo/sandbox.

**Constraints**: Başka BC DB/aggregate'ine ERİŞMEZ (yalnız MCP proxy); kullanıcı token'ı KALICI saklanmaz
(çağrı başına taşınır); kart PAN'ı fasaddan/agent'tan/LLM'den geçmez; UCP REST kanalı değişmez.

**Scale/Scope**: Tek yeni servis + yeni seed OAuth istemci + gateway route + AppHost wiring. Yeni DB/tablo/
event YOK. ChatAgent söküm KAPSAM DIŞI (ayrı feature).

## Constitution Check

*GATE: Phase 0'dan önce geçmeli; Phase 1 sonrası yeniden.*

- **İlke I — BC İzolasyonu**: ✅ Fasad bir BC DEĞİL (domain/DB yok); gateway gibi agent-transport altyapısı.
  Başka BC'nin DB/aggregate'ine dokunmaz; alt BC'lere yalnız **MCP (sanksiyonlu agent kanalı)** ile erişir.
  Mevcut BC uçlarıyla birlikte yaşar. İş mantığı taşımaz.
- **İlke II — Zengin Aggregate**: ➖ Uygulanamaz — fasad domain aggregate içermez (proxy). Domain kuralları
  alt BC'lerde kalır.
- **İlke III — VSA + CQRS, Repository yok**: ✅ Fasadda command/query handler yok; saf yönlendirme. Repository
  yok. (Tool-adı→BC kaydı + yüzey süzme saf birimler.)
- **İlke IV — Result Pattern**: ➖ Fasad, MCP tool-error semantiğini kullanır (istemciye MCP hata sonucu);
  domain Result modeli alt BC'lerde. Fasad hatası = MCP error content (çökme yok).
- **İlke V — Scope Yetki**: ✅ Fasad = OAuth resource; iki PRM (müşteri/admin scope demetleri). Gelen bearer
  alt BC'lere aynen forward; her BC kendi scope'unu doğrular. Yeni seed istemci `external-customer-agent`
  (public+PKCE, Explicit consent, müşteri scope demeti); yönetim için mevcut `external-admin-agent`.
- **İlke VI — Domain-TDD**: ✅ Saf birimler (yönlendirme kaydı + yüzey süzme + anonim/step-up karar mantığı)
  test-first; discovery/proxy/wiring test-sonra + canlı.
- **İlke VII — FLOW.md**: ➖ Fasad BC değil (gateway gibi domain-süreçsiz altyapı) → FLOW.md gerekmez.
- **Teknoloji kısıtları**: ✅ .NET 10, Aspire'dan çalıştırma, Options pattern, Scrutor DI, tek GlobalUsings.

**Gerekçelendirilmiş sapma** (Complexity Tracking'e işlendi): fasad, alt BC'lere **imperatif MCP `CallTool`**
sürer — "MCP yalnız agent tüketir; agent-olmayan kod imperatif MCP sürmez" kuralının bilinçli istisnası.
Fasadın var oluş nedeni MCP toplama/proxy'lemektir (agent-transport altyapısı); 070'in imperatif MCP
istemcisi emsali. İş-mantığı kodu MCP'ye uzanmıyor — bu altyapının kendisi transport.

## Project Structure

### Documentation (this feature)

```text
specs/073-customer-mcp-facade/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1
│   ├── facade-endpoints.md      # /mcp + /mcp-admin davranışı + PRM + tek-login/step-up
│   ├── downstream-registry.md   # tool-adı → BC + yüzey eşleme kaydı
│   └── discovery-proxy.md       # ListTools (lazy toplama) + CallTool (token-forward proxy)
└── tasks.md             # /speckit-tasks (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/agents/Mcp.Gateway/                       # DB'siz fasad MCP server (Mail.Mcp emsali src/agents)
├── Mcp.Gateway.csproj
├── GlobalUsings.cs
├── Program.cs                                # iki MapMcp (/mcp + /mcp-admin) + PRM + auth + downstream client'lar
├── Options/
│   ├── FacadeOption.cs                       # downstream BC listesi + yüzey + discovery makine kimliği
│   └── (McpResourceMetadata Common'dan)
├── Routing/
│   ├── ToolRoutingRegistry.cs                # tool-adı → BC + yüzey (saf; test-first)
│   └── SurfaceFilter.cs                       # müşteri/admin yüzey süzme (saf; test-first)
├── Aggregation/
│   ├── ToolCatalogCollector.cs               # lazy ListTools toplama (makine token'ı; kısa-TTL cache)
│   └── ProxyToolInvoker.cs                    # CallTool → sahibi BC'ye kullanıcı bearer forward
├── Auth/
│   └── (external-customer-agent Identity.Server seed'de)
└── Dependencies/DependencyExtensions.cs

tests/Mcp.Gateway.Tests/
├── ToolRoutingRegistryTests.cs               # ad→BC yönlendirme (test-first)
└── SurfaceFilterTests.cs                     # müşteri/admin süzme (test-first)

# Wiring
src/others/Identity.Server/Config.cs          # external-customer-agent (public+PKCE, müşteri scope demeti)
src/services/gateway/Gateway/appsettings*     # /mcp + /mcp-admin → mcp-gateway route (tek dış giriş)
src/aspire/AppHost/AppHost.cs                 # mcp-gateway kaydı + tool topladığı BC'leri WaitFor
ECommerceAgent.slnx              # 1 yeni proje (+ test)
```

**Structure Decision**: Yeni izole servis `src/agents/Mcp.Gateway` (agent-facing, DB'siz standalone MCP —
Mail.Mcp emsali). BC değil; `src/services` altına konmaz (domain/DB yok). Gateway yalnız route eder (saf
proxy doğası korunur). Fiziksel klasörler solution klasörleriyle birebir.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Fasad imperatif MCP `CallTool` sürer ("MCP yalnız agent tüketir" istisnası) | Fasadın işi = MCP toplama/proxy; agent-transport altyapısının özü budur (ChatAgent tool sarma deseninin server ikizi) | REST/gRPC ile yapmak = her BC'nin MCP tool sözleşmesini ikinci kez REST olarak yeniden tanımlamak; MCP'nin tek-yüzey değerini yok eder. 070 imperatif MCP istemcisi emsali (bilinçli, dar). |