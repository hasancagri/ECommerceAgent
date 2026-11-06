# Tasks: MCP Query-Only Dönüşü + ScoutAgent (Kâşif Sohbeti)

**Input**: Design documents from `/specs/007-mcp-agent/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Principle VI (Domain-TDD) → pure domain test tasks MANDATORY and ordered before impl
(`Conversation` aggregate + `ConversationContext`). MCP tools / agent / endpoints / React = canlı
doğrulama, no test tasks.

**Organization**: by user story for independent implementation + testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no incomplete deps)
- **[Story]**: US1/US2/US3/US4
- Exact file paths included

---

## Phase 1: Setup (Altyapı hizası)

**Purpose**: Paket hizası + yeni servis iskeleti (kod yazımı öncesi).

- [X] T001 Align MAF packages to stable 1.x hattı + add `ModelContextProtocol.AspNetCore` and Ollama `IChatClient` package in `Directory.Packages.props` (sürümler yalnız props — CPM)
- [X] T002 Remove dead `Microsoft.Agents.AI.A2A` reference (024 taksit-quote artığı) from `src/services/order/Order.Api/Order.Api.csproj`
- [X] T003 [P] Create ScoutAgent service project `src/agents/ScoutAgent.Api/ScoutAgent.Api.csproj` (.NET 10, Nullable+ImplicitUsings; `<Bc>.Api` konvansiyonu) + add to `ECommerceAgent.slnx`
- [X] T004 [P] Create test project `tests/ScoutAgent.Api.Tests/ScoutAgent.Api.Tests.csproj` (xUnit + Shouldly) + add to `ECommerceAgent.slnx`

---

## Phase 2: Foundational (Blocking — tüm sohbet hikâyelerini kilitler)

**Purpose**: ScoutAgent servis kablosu. Bu bitmeden hiçbir chat story çalışmaz.

**⚠️ CRITICAL**: No user story work begins until this phase completes.

- [X] T005 Marten `scoutDb` wiring + `opts.Schema.For<Conversation>().DocumentAlias("conversation")` (tr-TR ı TUZAĞI — lowercase/I'sız elle) + `ApplyAllDatabaseChangesOnStartup` in `src/agents/ScoutAgent.Api/Extensions/MartenExtensions.cs`
- [X] T006 Ollama `IChatClient` + MAF agent host wiring; model + `num_ctx` Options'tan (DEFAULT: model `qwen2.5` — tool-calling destekli; `num_ctx=8192`; Aspire'da T042'de doğrulanır, SC-003 kilidi) in `src/agents/ScoutAgent.Api/Program.cs`
- [X] T007 `ScoutAgentFactory` (Singleton — agent başlangıçta yakalanır, per-user token çağrı anında) in `src/agents/ScoutAgent.Api/Agents/ScoutAgentFactory.cs`
- [X] T008 `UserTokenForwardingHandler` DelegatingHandler (`IHttpContextAccessor` bearer → downstream MCP `Authorization`) in `src/agents/ScoutAgent.Api/Agents/UserTokenForwardingHandler.cs`
- [X] T009 `McpToolSource` skeleton: McpClient per uç (service discovery statik liste, fasat YOK) + `ListToolsAsync` → `AITool` + auth-state toolset gating kancası in `src/agents/ScoutAgent.Api/Agents/McpToolSource.cs`
- [X] T010 Options POCOs: Ollama (model/num_ctx), MCP uç listesi, hafıza (`WindowSize=10` son ham mesaj + `SummaryThreshold=20` eşiği aşınca eskiler özetlenir) in `src/agents/ScoutAgent.Api/Options/` + `BindConfiguration().ValidateOnStart()`; add `Properties/launchSettings.json` (launchSettings TUZAĞI — yoksa Production açılır, her istek 500)
- [X] T011 AppHost: `scoutDb` + `scout-agent` kaydı + 4 BC MCP referansı (WaitFor) in `src/aspire/AppHost/AppHost.cs`
- [X] T012 Gateway `/api/chat/**` rotası → `scout-agent` kümesi in `src/others/Gateway/appsettings.json`

**Checkpoint**: ScoutAgent ayağa kalkar; MCP uçları eklenince story'ler başlayabilir.

---

## Phase 3: User Story 1 - Sohbetten kitap sorgusu (anonim) (Priority: P1) 🎯 MVP

**Goal**: Girişsiz ziyaretçi chat'e kitap/yazar yazar → katalog aranır, kart listesi döner; yoksa dürüst "bulunamadı".

**Independent Test**: Sistem ayakta, girişsiz; "Sabahattin Ali kitapları" → doğru kitaplar; olmayan şey → "bulunamadı"; "hava nasıl?" → kapsam dışı nazik cevap (tool çağrılmaz).

### Implementation for User Story 1

- [X] T013 [P] [US1] `search_books` query slice + `[McpServerToolType]` tool (ad/yazar `ILIKE` + `IsSalable` + sayfalama; `page=1`/`pageSize=10` DEFAULT — optional-param tuzağı; GUID id iç) in `src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Queries/SearchBooks.cs`
- [X] T014 [P] [US1] `get_book` query slice + tool (`isbn` VEYA `id`; bulunamadı dönüşü) in `src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Queries/GetBook.cs`
- [X] T015 [US1] Catalog `Program.cs`: `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` + `app.MapMcp("/mcp")` (anonim — token gerekmez) in `src/services/catalog/Catalog.Api/Program.cs`
- [X] T016 [US1] ScoutAgent: Catalog `/mcp`'ye MCP client bağla (`McpToolSource` statik uç) + tool'ları `AITool` olarak agent'a ver in `src/agents/ScoutAgent.Api/Agents/McpToolSource.cs`
- [X] T017 [US1] `SendMessage` command: bağlam→agent çalıştır→tool sonucunu `{text, items}` map (anonim yol: kalıcı kayıt YOK, oturumluk conversationId) in `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SendMessage.cs`
- [X] T018 [US1] Chat endpoint `POST /api/chat/v1/messages` (opsiyonel bearer; model/MCP erişilemezse `503` FR-008) in `src/agents/ScoutAgent.Api/ChatEndpointExtension.cs`
- [X] T019 [US1] `items` yapılı öğeler tool dönüşünden BİREBİR (uydurma 0, SC-004); fiyat/kapak tool'dan, GUID yüzeyde yok (FR-006) — SendMessage içinde map doğrulanır

**Checkpoint**: US1 bağımsız çalışır — anonim kitap sohbeti uçtan uca (MVP).

---

## Phase 4: User Story 2 - Kişisel verini sohbetten isteme (Priority: P1)

**Goal**: Girişli kullanıcı adres/sipariş/kitaplık sorar → doğru BC'den KENDİ verisi; girişsiz → "giriş gerekli".

**Independent Test**: Girişli üç soruyu sorar → her biri doğru BC'den kendi verisini döner; başka kullanıcının verisi asla; girişsiz kişisel soru → veri yok, giriş daveti.

### Implementation for User Story 2

- [X] T020 [P] [US2] `get_my_addresses` slice + tool (UserId token'dan; `label/recipient/line/district/city/isDefault`; addressId DÖNMEZ) in `src/services/customer/Customer.Api/Domains/AddressBooks/Features/Agents/Queries/GetMyAddresses.cs`
- [X] T021 [P] [US2] `get_my_library` slice + tool (çağıranın `UserPurchase` kayıtları, UserId index) in `src/services/library/Library.Api/Domains/.../Features/Agents/Queries/GetMyLibrary.cs`
- [X] T022 [P] [US2] `get_my_orders` slice + tool (BuyerId; `status?` DEFAULT null; `orderNo/status/placedAt/total/lineCount`) in `src/services/order/Order.Api/Domains/.../Features/Agents/Queries/GetMyOrders.cs`
- [X] T023 [US2] Her BC `Program.cs`: `AddMcpServer()...WithToolsFromAssembly()` + `app.MapMcp("/mcp")` + kişisel uçlar `.RequireAuthorization` (`customer.read` / `library.read` / `order.read`) — Customer/Library/Order `Program.cs`
- [X] T024 [US2] ScoutAgent `McpToolSource`: 3 kişisel uca bağlan + auth-state toolset gating (girişsizse kişisel tool'lar araç kutusuna HİÇ konmaz — FR-005a) in `src/agents/ScoutAgent.Api/Agents/McpToolSource.cs`
- [X] T025 [US2] `UserTokenForwardingHandler`'ı kişisel MCP client'lara tak (`AddHttpMessageHandler`); girişsiz/bayat token → sunucu reddi "giriş gerekli"ye çevrilir (iki katman) in `src/agents/ScoutAgent.Api/Program.cs`

**Checkpoint**: US1 + US2 bağımsız çalışır — metinden doğru BC'ye yönlendirme + kimlik taşıma.

---

## Phase 5: User Story 4 - Sohbet geçmişi + hafıza (Priority: P2)

**Goal**: Girişli kullanıcının sohbetleri kalıcı; geçmiş listeli, açılıp devam edilir; uzun sohbette ana konu kaybolmaz (özet katmanı). Anonim kalıcı değil.

**Independent Test**: İki sohbet → geçmişte ikisi (yalnız kendi); biri açılıp "az önceki yazar kimdi?" → doğru cevap. 30+ mesaj → baştaki atıf hatırlanır, süre makul.

### Tests for User Story 4 (Principle VI — test-first, FAIL önce) ⚠️

- [X] T026 [P] [US4] TEST `Conversation` aggregate: `Start`/`AddUserMessage`/`AddAssistantMessage`/`ApplySummary` (Method_Condition_Outcome; UserId değişmez invariant) in `tests/ScoutAgent.Api.Tests/ConversationTests.cs`
- [X] T027 [P] [US4] TEST `ConversationContext` saf kurucu: pencere kırpma + özet başa ekleme + tool-sonucu dışlama in `tests/ScoutAgent.Api.Tests/ConversationContextTests.cs`

### Implementation for User Story 4

- [X] T028 [US4] `Conversation` AggregateRoot (alias `conversation`) + `ChatMessage` VO (Role/Text/CreatedAt, parametresiz private ctor — Marten record VO tuzağı) in `src/agents/ScoutAgent.Api/Domains/Conversations/Conversation.cs` + `.../ValueObjects/ConversationValueObjects.cs`
- [X] T029 [US4] `ConversationContext` saf fonksiyon (`(Summary, Messages, WindowSize)` → `[özet]+[son WindowSize ham mesaj]`; WindowSize default 10) in `src/agents/ScoutAgent.Api/Domains/Conversations/ConversationContext.cs`
- [X] T030 [US4] `SendMessage`: girişli yol — kayıt yükle/oluştur, bağlamı DB'den kur (FR-012, framework state'ine güvenme), kaydet, rolling summary (mesaj sayısı `SummaryThreshold=20` aşınca model eskiyi özetler → `ApplySummary`, `SummarizedUpTo` ilerler) in `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SendMessage.cs`
- [X] T031 [P] [US4] `ListConversations` query + `GET /api/chat/v1/conversations` (yalnız çağıranın, UserId index) in `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Queries/ListConversations.cs`
- [X] T032 [P] [US4] `GetConversation` query + `GET /api/chat/v1/conversations/{id}` (sahiplik → başkasınınki `404`) in `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Queries/GetConversation.cs`
- [X] T033 [US4] ScoutAgent `FLOW.md` (sohbet süreci EventStorming notasyonlu) + `.csproj` linked-file in `src/agents/ScoutAgent.Api/FLOW.md`

**Checkpoint**: US1 + US2 + US4 — kalıcı geçmiş + katmanlı hafıza.

---

## Phase 6: User Story 3 - Keşif kartları sohbeti başlatır (Priority: P2)

**Goal**: DiscoverWelcome genel kartları (commit'li TS config) sohbeti başlatır; aktif kartlar çalışır, motor bekleyenler "yakında" rozetli pasif.

**Independent Test**: Aktif kart tıkla → chat kartın sorusuyla açılır, cevap gelir; "yakında" kart tıkla → sohbet başlatmaz.

**Repo**: `~/dev/ECommerceWeb` (React, ayrı repo).

### Implementation for User Story 3

- [X] T034 [P] [US3] Commit'li kart yapılandırması (etiket + soru + durum aktif|yakında) in `~/dev/ECommerceWeb/src/config/discoverCards.ts`
- [X] T035 [US3] DiscoverWelcome: aktif kart tık → sorusu chat'e önceden doldurulur; "yakında" kart pasif/rozetli (tıkla sohbet başlatmaz) in `~/dev/ECommerceWeb/src/.../DiscoverWelcome.tsx`
- [X] T036 [US3] Chat kutusu + cevap akışı (tek-parça, FR-013) + "düşünüyor" göstergesi + `items` kart render (kapak tıklanabilir link) in `~/dev/ECommerceWeb/src/.../Chat*.tsx`
- [X] T037 [US3] Token iletimi: oturum bearer'ı `/api/chat` isteğine; `503` anlaşılır hata gösterimi (FR-008) in `~/dev/ECommerceWeb/src/.../chatApi.ts`
- [X] T038 [US3] ChatHistory sayfası `/api/chat/v1/conversations` listesine + aç/devam akışına bağlanır in `~/dev/ECommerceWeb/src/.../ChatHistory.tsx`

**Checkpoint**: Tüm hikâyeler bağımsız çalışır; React Kâşif yüzeyi canlı.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T039 [P] CLAUDE.md güncelle: MCP dönüşü (Yapma listesi "MCP parça parça geri getirme" maddesi revize — bu spec bilinçli dönüş), Yüzey modeli, BC haritasına `scout` satırı
- [X] T040 [P] constitution/ADR notu: MCP query-only dönüşü = bilinçli mimari karar (plan Constitution Check referansı) in `docs/adr/`
- [X] T041 Wolverine keşif kontrolü: `*Handler`/`*Consumer` ile bitmeyen query handler varsa `opts.Discovery.IncludeType(...)` ekle (sessiz yutma tuzağı) in ilgili `Program.cs`'ler
- [ ] T042 `dotnet build && dotnet test` yeşil + quickstart.md Aspire elle doğrulama (US1-US4 + Ollama-kapalı dayanıklılık)

---

## Dependencies & Execution Order

### Phase Dependencies
- Setup (P1) → no deps.
- Foundational (P2) → Setup; BLOCKS all stories.
- US1 (P3) → Foundational. MVP.
- US2 (P4) → Foundational (US1 ScoutAgent kablosunu paylaşır ama bağımsız test edilir).
- US4 (P5) → Foundational + US1 SendMessage yolu (persistence'ı ekler).
- US3 (P6) → US1/US2/US4 endpointleri (React tüketir).
- Polish (P7) → istenen tüm story'ler.

### Within stories
- US4: T026/T027 testleri T028/T029 impl'inden ÖNCE (Principle VI).
- Query slice'lar → MCP host kablosu → ScoutAgent client → endpoint.

### Parallel Opportunities
- T003/T004 (ayrı projeler) paralel.
- US1: T013/T014 paralel (ayrı dosya); T020/T021/T022 US2'de paralel (ayrı BC).
- US4: T026/T027 paralel; T031/T032 paralel.
- Farklı BC'lerin MCP slice'ları paralel (BC izolasyonu) — 1-servis-1-agent deseni onaylı.

---

## Parallel Example: US2 kişisel MCP tool'ları

```bash
Task: "get_my_addresses slice+tool in Customer.Api/.../GetMyAddresses.cs"
Task: "get_my_library slice+tool in Library.Api/.../GetMyLibrary.cs"
Task: "get_my_orders slice+tool in Order.Api/.../GetMyOrders.cs"
```

---

## Implementation Strategy

### MVP First (US1)
1. Setup → 2. Foundational → 3. US1 (Catalog MCP + ScoutAgent + anonim chat) → DOĞRULA → demo.

### Incremental
US1 (anonim kitap) → US2 (kişisel yönlendirme) → US4 (geçmiş+hafıza) → US3 (React yüzey). Her biri önceki kırmadan değer ekler.

---

## Notes
- [P] = ayrı dosya, dep yok.
- Pure domain (Conversation/ConversationContext) test-first; gerisi canlı doğrulama (İlke VI).
- MCP optional param DEFAULT şart (tuzak); Marten yeni belge DocumentAlias I'sız (tuzak); yeni servis launchSettings (tuzak).
- Her task sonrası / mantıklı grupta commit.
- Scope'lar DOĞRULANDI (analiz): `customer.read`/`library.read`/`order.read` zaten `AuthorizationScopes`'ta + `customer` rolünün scope demetinde (AppRegistry seed) + üç serviste wired — yeni scope/seed task YOK.
- Defaultlar: Ollama `qwen2.5`/`num_ctx=8192`; hafıza `WindowSize=10` + `SummaryThreshold=20` (T042'de ayarlanır).
