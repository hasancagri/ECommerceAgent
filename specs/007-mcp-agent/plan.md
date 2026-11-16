# Implementation Plan: MCP Query-Only Dönüşü + ScoutAgent (Kâşif Sohbeti)

**Branch**: `007-mcp-agent` | **Date**: 2026-11-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-mcp-agent/spec.md`

## Summary

Müşteri serbest metinle kitap arar ve kişisel verisini (adres/kitaplık/sipariş) sorar. Her BC kendi
query-only MCP ucunu host eder (`ModelContextProtocol.AspNetCore`); yeni first-party `ScoutAgent` servisi
(MAF + yerel Ollama) React chat'ten gelen metni alır, LLM tool-seçimiyle doğru BC MCP tool'unu çağırır,
sonucu `{text, items}` olarak döner. Kullanıcı token'ı çağrı anında downstream MCP'ye taşınır; girişsiz
yalnız anonim katalog tool'una erişir. Sohbet transkripti ScoutAgent'ın kendi DB'sinde kalıcı; hafıza
pencere+özet+arşiv katmanlı. Fasat/keşif katmanı YOK — ScoutAgent 4 uca doğrudan bağlanır.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`+`ImplicitUsings`)

**Primary Dependencies**: Microsoft Agent Framework 1.x (MCP + IChatClient), `ModelContextProtocol.AspNetCore`
(MCP server), yerel Ollama (IChatClient), Marten (ScoutAgent sohbet deposu + BC query'leri), Wolverine
(mevcut), YARP Gateway (mevcut).

**Storage**: ScoutAgent → yeni `scoutDb` (Marten, `Conversation` belgesi). BC'ler → mevcut DB'leri (yalnız okunur).

**Testing**: xUnit + Shouldly, saf birim (host harness yok — İlke VI). Kapsam: `Conversation` aggregate +
`ConversationContext` bağlam kurucu (test-first). MCP tool/agent/endpoint = canlı doğrulama (Aspire).

**Target Platform**: Aspire AppHost (lokal), Linux container (prod yolu mevcut).

**Project Type**: Çok-servisli backend (yeni servis + 4 BC'ye MCP uç eklentisi) + mevcut React (ECommerceWeb, ayrı repo).

**Performance Goals**: Basit sohbet cevabı < 10 sn (yerel model dahil, SC-003). 30+ mesaj bağlam maliyeti sabit (SC-005).

**Constraints**: Yerel model (bulut LLM yok); num_ctx modele göre; tek-parça yanıt (stream v2). Uydurma 0 (SC-004).

**Scale/Scope**: 23.600 kitap katalog; sohbet başına birkaç tool çağrısı; 4 MCP uç + 1 agent servisi + React chat.

## Constitution Check

*GATE: Phase 0 öncesi + Phase 1 sonrası.*

- **İlke I (BC izolasyonu):** ✓ Her BC kendi MCP ucunu host eder; ScoutAgent başka BC DB'sine DOKUNMAZ —
  sanksiyonlu MCP kanalıyla konuşur ("Context'ler arası: event + MCP + gRPC"). ScoutAgent kendi verisini
  (sohbet) kendi `scoutDb`'sinde tutar. MCP yalnız agent tüketir — ScoutAgent agent'tır. ✓
- **İlke II (zengin aggregate):** ✓ `Conversation` AggregateRoot, invariant içeride (UserId değişmez,
  Messages yalnız metottan). BC aggregate'lerine davranış EKLENMEZ (yalnız yeni okuma slice'ı).
- **İlke III (VSA/CQRS, repository yok):** ✓ MCP tool'ları `Features/Agents/Queries/` slice'ında ince
  sarmalayıcı; `IQuerySession`/`IMessageBus`. Repository yok.
- **İlke IV (Result):** ✓ Query handler'lar `Feature*ResultModel` döner.
- **İlke V (scope yetki):** ✓ Kişisel MCP uçları `.RequireAuthorization(scope)`; katalog anonim (meşru —
  "anonim gezinme meşru"). ScoutAgent kullanıcı token'ını taşır.
- **İlke VI (domain-TDD):** ✓ `Conversation` + `ConversationContext` saf → test-first (tasks'ta testler önce).
  Agent/MCP/endpoint infra → canlı doğrulama.
- **İlke VII (FLOW.md):** ScoutAgent'a sohbet süreci FLOW.md eklenir; 4 BC'de domain süreci DEĞİŞMEZ
  (yalnız okuma yüzeyi) → onların FLOW'u tetiklenmez.
- **İlke VIII (sır insan-yüzeyde yok):** ✓ Dönen veri kullanıcının KENDİ verisi (sır değil); GUID gizli
  (FR-006); adres PII yalnız sahibine. Sır/credential dönmez.

**MCP dönüşü:** CLAUDE.md "MCP'yi parça parça geri getirme" maddesi bilinçli mimari karar + spec istiyordu —
bu feature o. Yapma-listesi + yüzey modeli revize edilir (polish task'ı).

**Sonuç:** İHLAL YOK. Yeni servis god-service değil (kendi dar sorumluluğu: sohbet orkestrasyonu).

## Project Structure

### Documentation (this feature)

```text
specs/007-mcp-agent/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/ (mcp-tools.md, chat-api.md)
└── checklists/requirements.md
```

### Source Code

```text
src/services/scout/ScoutAgent/              # YENİ servis
├── Program.cs                              # MAF agent + Ollama IChatClient + 4 McpClient + Marten(scoutDb)
├── Domains/Conversations/
│   ├── Conversation.cs                     # AggregateRoot (alias "conversation")
│   ├── ConversationContext.cs              # saf bağlam kurucu (test-first)
│   ├── ValueObjects/ConversationValueObjects.cs   # ChatMessage
│   └── Features/
│       ├── Commands/SendMessage.cs         # sohbet turu: bağlam kur → agent çalıştır → kaydet
│       └── Queries/{ListConversations,GetConversation}.cs
├── Agent/                                  # MAF kablosu: ScoutAgentFactory (Singleton), tool toplama
│   ├── McpToolSource.cs                    # 4 uca McpClient + ListToolsAsync + auth-state toolset gating
│   └── UserTokenForwardingHandler.cs       # DelegatingHandler: istek bearer'ı → downstream MCP
├── ChatEndpointExtension.cs                # /api/chat/v1/* (chat-api.md)
├── Extensions/ (MartenExtensions, MessagingExtensions)
└── FLOW.md

tests/ScoutAgent.Api.Tests/                 # Conversation + ConversationContext birim testleri

# 4 BC'ye MCP uç eklentisi (her birinde):
src/services/catalog/Catalog.Api/
├── Domains/Products/Features/Agents/Queries/{SearchBooks,GetBook}.cs   # [McpServerToolType], anonim
src/services/customer/Customer.Api/Domains/AddressBooks/Features/Agents/Queries/GetMyAddresses.cs
src/services/library/Library.Api/.../Features/Agents/Queries/GetMyLibrary.cs
src/services/order/Order.Api/.../Features/Agents/Queries/GetMyOrders.cs
# her BC Program.cs: AddMcpServer().WithHttpTransport().WithToolsFromAssembly() + app.MapMcp("/mcp")
#   + kişisel uçlar .RequireAuthorization(scope)

src/aspire/AppHost/AppHost.cs               # scoutDb + scout-agent kaydı + MCP referansları
src/others/Gateway/appsettings.json         # /api/chat/** rotası → scout-agent kümesi
```

**Structure Decision:** ScoutAgent `src/services/scout/` altında tam servis (kendi DB + testler, `.slnx`'e).
BC'lere eklenen yalnız okuma slice'ları + MCP host kablosu. React değişikliği ayrı repo (ECommerceWeb).

## Faz sırası (tasks özeti — detay /speckit-tasks)

1. **Altyapı hizası:** MAF paket sürümleri stabil hatta; Order.Api ölü A2A referansı sil; `ModelContextProtocol.AspNetCore` + Ollama IChatClient paketi props'a.
2. **Catalog MCP (anonim, US1):** SearchBooks + GetBook query slice + MCP tool + MapMcp. İlk uçtan uca.
3. **ScoutAgent iskelet:** servis + scoutDb + MAF agent (Ollama) + Catalog MCP client + chat endpoint → US1 canlı.
4. **Hafıza + geçmiş (US4):** Conversation aggregate + ConversationContext (TEST-FIRST) + kalıcı kayıt + list/get.
5. **Kişisel MCP uçları (US2):** Customer/Library/Order tool'ları + scope koruma + UserTokenForwardingHandler + toolset gating.
6. **React (US3):** chat kutusu + cevap kartları + DiscoverWelcome kartları (commit'li TS config) + token iletimi.
7. **Polish:** CLAUDE.md (MCP dönüşü, yüzey modeli, BC haritası scout satırı) + constitution notu + FLOW.md + build/test yeşil.

## Complexity Tracking

İhlal yok — tablo boş.
