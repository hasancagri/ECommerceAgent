# Implementation Plan: ScoutAgent (Kâşif) — MCP Niyet-Routing Sohbet

**Branch**: `014-scout-agent-mcp` | **Date**: 2026-11-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/014-scout-agent-mcp/spec.md`

## Summary

ScoutAgent'ı (Kâşif) dirilt: LLM (OpenAI gpt-4o-mini, tool-calling) kullanıcının serbest metnini anlar, doğru BC'nin **MCP query tool'unu** seçip çağırır, cevabı tek sohbet akışında tool'a göre kart olarak döner. Agent BC'lere yalnız MCP ile erişir (first-party, agent-içi). Yazma yok. 007-mcp-agent branch'i başlangıç noktasıdır (ScoutAgent.Api + Catalog/Customer/Library/Order tool'ları zaten var); söküm-sonrası değişikliklerle uzlaştırılır (her BC `/mcp` ucu geri açılır), Basket+Reviews tool'ları yeni yazılır, React ChatWorkspace sohbete bağlanır, login arama-kaydı (012/013) agent yoluna taşınır.

## Technical Context

**Language/Version**: .NET 10 (C#), TypeScript/React (ECommerceWeb ayrı repo)

**Primary Dependencies**: Marten (scoutDb, Postgres doc store) · Wolverine (in-proc bus) · Microsoft.Agents.AI + Microsoft.Extensions.AI.OpenAI (gpt-4o-mini, `UseFunctionInvocation`) · ModelContextProtocol (MCP client ScoutAgent'ta + MCP server her BC'de) · OpenIddict JWT (inline, `ValidateAudience=false`)

**Storage**: scoutDb (yalnız login `Conversation`) · BC verisi MCP tool üstünden okunur (ScoutAgent DB'ye değmez)

**Testing**: xUnit + Shouldly saf unit — `Conversation` aggregate davranışı + `ConversationContext` + arama tool filtre/sıra (test-first, İlke VI)

**Target Platform**: Aspire AppHost (Linux container), gateway arkası

**Project Type**: Web — yeni backend servis (`src/agents/ScoutAgent.Api`) + React frontend (ECommerceWeb) + 6 BC'de MCP server yüzeyi

**Performance Goals**: Sohbet turu tek-parça yanıt (stream yok, v1). Bağlam maliyeti sabit (pencere + özet).

**Constraints**: Agent BC DB'sine değmez (yalnız MCP). Yazma tool'u yok. Anon kalıcı kayıt yok. Sır (OpenAI key) insan-yüzeyde görünmez (İlke VIII).

**Scale/Scope**: 1 yeni servis · 6 BC MCP yüzeyi (4'ü 007'de var, 2 yeni) · 7 query tool · React 1 ekran yeniden-bağlama + N kart bileşeni · 1 ADR.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden.*

- **İlke I (BC izolasyonu):** ✅ ScoutAgent kendi scoutDb'si; BC'lere **yalnız MCP** ile erişir (İlke I MCP'yi meşru kanal sayar: "integration event + MCP + sanksiyonlu gRPC"). Başka BC DB'sine değmez. **Not:** CLAUDE.md "MCP geri getirme" operasyonel kuralı tersine çevrilir — bu anayasa ihlali DEĞİL (İlke I MCP'yi zaten tanır); ADR + CLAUDE.md güncellemesiyle kayda alınır. Dış/BYO agent yüzeyi HÂLÂ kapalı.
- **İlke II (zengin aggregate):** ✅ `Conversation : AggregateRoot`; `Messages` private, yalnız metotla büyür (`Start`/`AddUserMessage`/`AddAssistantMessage`/`ApplySummary`).
- **İlke III (VSA+CQRS):** ✅ ScoutAgent `Features/Commands/SendMessage` + `Features/Queries/{List,Get}Conversation`; BC tool'ları `Domains/<X>/Features/Agents/Queries/<Name>.cs` (MCP sarmalayıcı aynı dosyada). Repository yok, handler `IDocument/QuerySession`.
- **İlke IV (Result):** ✅ Handler `FeatureObjectResultModel<T>`; aggregate `ResultDomain`.
- **İlke V (scope yetki):** ✅ Kişisel MCP uçları auth ister; isteğin kendi bearer'ı `UserTokenForwardingHandler` ile taşınır → uç scope'u doğrular. Anon araç kutusu kişisel tool'ları HİÇ görmez.
- **İlke VI (domain-TDD):** ✅ `Conversation` davranışı + arama tool sıralaması (yazar-önce) test-first; test task'ı implementasyondan önce.
- **İlke VII (FLOW.md):** ✅ ScoutAgent `FLOW.md` yazıldı; dokunulan BC FLOW'larına planned query-tool sticky'si eklenir (aynı PR).
- **İlke VIII (sır görünmez):** ✅ OpenAI key yalnız Options (user-secret/AppHost param), hiçbir yanıtta/log'da görünmez; MCP kuralları GUID/sır gizler.

**Gate: PASS.** Anayasa ihlali yok. Governance aksiyonu: ADR (MCP-içeri agent) + CLAUDE.md "Yapma listesi"/"Yüzey modeli" güncellemesi.

## Project Structure

### Documentation (this feature)

```text
specs/014-scout-agent-mcp/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── chat-api.md          # ScoutAgent HTTP yüzeyi (React → gateway → ScoutAgent)
│   └── mcp-tools.md         # 7 query tool imzası + auth-gating
└── checklists/requirements.md
```

### Source Code (repository root)

```text
src/agents/ScoutAgent.Api/            # YENİ servis (007'den dirilir)
├── Program.cs · FLOW.md · ScoutAgent.Api.csproj
├── Agents/{ScoutAgentFactory,McpToolSource,UserTokenForwardingHandler}.cs
├── Domains/Conversations/{Conversation.cs, ConversationContext.cs,
│   Features/Commands/SendMessage.cs, Features/Queries/{List,Get}Conversation.cs}
├── Options/ScoutOptions.cs           # ScoutOptions + McpEndpointsOptions (+basket,+reviews)
└── Extensions/{Marten,Messaging,Auth}Extensions.cs

src/services/<bc>/<Bc>.Api/
├── Program.cs                        # MapMcp + WithToolsFromAssembly geri (6 BC)
└── Domains/<X>/Features/Agents/Queries/<Tool>.cs
    # Catalog search_books/get_book (var, ILIKE uzlaştır) · Order get_my_orders (var)
    # Customer get_my_addresses (var) · Library get_my_library (var)
    # Basket get_basket (YENİ) · Reviews get_book_reviews (YENİ)

src/aspire/AppHost/AppHost.cs         # ScoutAgent.Api + scoutDb + OpenAI key param + gateway rota
src/others/Gateway/                   # /api/chat/** → scout-agent rotası
docs/adr/adr-mcp-inward-agent.md      # YENİ — MCP-içeri agent kararı

# ECommerceWeb (ayrı repo)
src/pages/ChatWorkspace.tsx           # /search yerine /api/chat/v1/messages; tool→kart render
src/pages/ChatHistory.tsx             # "tekrar ara" → sohbete mesaj (URL değil)
src/api/chat.ts                       # mevcut (SendMessage/list/get)
src/components/chat/<ToolCard>.tsx     # tool başına kart (kitap/sepet/sipariş/kitaplık/adres/yorum)
```

**Structure Decision**: 007'nin `src/agents/ScoutAgent.Api` düzeni korunur (ScoutAgent = agent katmanı, BC değil ama BC desenini izler: kendi DB + VSA slice'lar). BC tarafı conventions VSA: query tool'u `Agents/Queries` slice'ında, MCP sarmalayıcı aynı dosyada. React tarafı tek ChatWorkspace + tool-kart bileşenleri.

## Complexity Tracking

> Anayasa ihlali yok — bu tablo yalnız governance izi.

| Aksiyon | Neden | Not |
|---|---|---|
| CLAUDE.md "MCP geri getirme" tersine | Agent kimliği = MCP tüketicisi (İlke I meşru kanal) | ADR `adr-mcp-inward-agent`; dış/BYO yüzey kapalı kalır |
| Catalog 2 arama yolu (ILIKE + BM25) | Yazar-araması ILIKE ister; BM25 yazar aramaz | Bilinçli tekrar; `search_books`=ILIKE, hibrit=konu/semantik |
