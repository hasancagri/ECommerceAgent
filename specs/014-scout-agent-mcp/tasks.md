---
description: "Task list — ScoutAgent (Kâşif) MCP niyet-routing"
---

# Tasks: ScoutAgent (Kâşif) — MCP Niyet-Routing Sohbet

**Input**: `specs/014-scout-agent-mcp/` (plan.md, spec.md, research.md, data-model.md, contracts/)

**Tests**: İlke VI — saf domain (Conversation davranışı, ConversationContext, search_books sıralama) test-first; handler/endpoint/MCP/React canlı-doğrulanır.

**Kaynak**: `007-mcp-agent` branch'i ScoutAgent.Api + Catalog/Order/Library/Customer tool'larını taşır (dirilt + uzlaştır). Basket/Reviews + React yeni.

## Phase 1: Setup

- [X] T001 ScoutAgent.Api projesini `007-mcp-agent`'tan `src/agents/ScoutAgent.Api`'ye getir (csproj/GlobalUsings/Program.cs/launchSettings/appsettings/FLOW.md linked-file); `ECommerceAgent.slnx`'e ekle
- [X] T002 `tests/ScoutAgent.Api.Tests` projesi oluştur (xUnit+Shouldly); `.slnx`'e ekle
- [X] T003 `Directory.Packages.props`: ModelContextProtocol (client+server), Microsoft.Agents.AI, Microsoft.Extensions.AI.OpenAI sürümlerini ekle/hizala
- [X] T004 `src/aspire/AppHost/AppHost.cs`: ScoutAgent.Api + scoutDb (AddPostgres) + `ScoutOptions:ApiKey` secret param + 6 BC referansı
- [X] T005 Gateway (`src/others/Gateway`) config: `/api/chat/**` → `scout-agent` rotası (bearer taşı)
- [X] T006 [P] ADR `docs/adr/adr-mcp-inward-agent.md` — MCP-içeri first-party agent kararı (dış/BYO yüzey kapalı kalır; İlke I meşru kanal)
- [X] T007 [P] `CLAUDE.md` güncelle: Yapma-listesi MCP maddesi (agent-içi istisna), Yüzey modeli, BC haritası scout satırı, Teknoloji

## Phase 2: Foundational (ScoutAgent motoru — tüm story'leri bloklar)

**Amaç:** Sohbet hattı; her user story buradan geçer.

- [X] T008 [P] TEST `Conversation` davranış testleri (`Start` boş-userId→Error, `AddUserMessage` boş→Error, `AddAssistantMessage`, `ApplySummary` geri-gitmez) `tests/ScoutAgent.Api.Tests/ConversationTests.cs`
- [X] T009 `Conversation.cs` + `ValueObjects/ConversationValueObjects.cs` + `ContextRole` enum (007'den; `: AggregateRoot`, Messages private, ResultDomain)
- [X] T010 [P] TEST `ConversationContext.Build` testleri ([özet]+[son N] kesim) `tests/ScoutAgent.Api.Tests/ConversationContextTests.cs`
- [X] T011 `Domains/Conversations/ConversationContext.cs` (007'den; saf domain-service)
- [X] T012 `Agents/ScoutAgentFactory.cs` (007'den; OpenAI gpt-4o-mini, `UseFunctionInvocation`, system prompt + sayfa-izi talimatı)
- [X] T013 `Agents/McpToolSource.cs` + `Agents/UserTokenForwardingHandler.cs` (007'den; config-güdümlü uç listesi, auth-gating, token-forward)
- [X] T014 `Features/Commands/SendMessage.cs` (007'den) + **anon `history` (istemci-transkript) parametresi** ekle (anon bağlam/sayfalama)
- [X] T015 `Features/Queries/ListConversations.cs` + `GetConversation.cs` (007'den; sahiplik UserId süzer)
- [X] T016 `ChatEndpointExtension.cs` (007'den) + istek gövdesine `history` alanı; `/messages` opsiyonel bearer, `/conversations*` auth
- [X] T017 `Extensions/{Marten,Messaging,Auth}Extensions.cs` + `Options/ScoutOptions.cs` (ScoutOptions + McpEndpointsOptions) + `Program.cs` wiring (handler keşfi `IncludeType` gerekiyorsa ekle)

---

## Phase 3: US1 — Doğal dille kitap keşfi (P1) 🎯 MVP

**Hedef:** "Sabahattin Ali'nin kitaplarını listeler misin" → kitaplar sohbette kart. Anon çalışır.

**Bağımsız test:** Çıkışta doğal cümleyle yazar ara → çıplak sorguyla AYNI kitaplar döner (bug kapalı).

- [X] T018 [US1] `search_books` sıralama (yazar-önce) CANLI doğrulanır — ILIKE+jsonb Marten yolu saf-unit edilemez (conventions: Marten yolu Aspire elle; İlke VI yalnız saf domain). Quickstart US1 senaryosu kanıt
- [X] T019 [US1] Catalog `search_books` slice'ı 007'den `Domains/Products/Features/Agents/Queries/SearchBooks.cs`'e getir + 011-sonrası uzlaştır (ILIKE yazar+başlık; AuthorIds `$values` jsonb yolu)
- [X] T020 [P] [US1] Catalog `get_book` slice 007'den `Domains/Products/Features/Agents/Queries/GetBook.cs`
- [X] T021 [US1] Catalog `Program.cs`: `MapMcp` + `WithToolsFromAssembly` geri (ModelContextProtocol server) — `/mcp` ucu açık (anon)
- [X] T022 [US1] `McpEndpointsOptions`: `catalog` ucu (anon) teyit; Aspire'da ScoutAgent→catalog bağlanır
- [X] T023 [P] [US1] ECommerceWeb `ChatWorkspace.tsx`: `/api/catalog/products/search` yerine `/api/chat/v1/messages`; `?q=` URL kaldır
- [X] T024 [P] [US1] ECommerceWeb kitap-kart bileşeni (`search_books`/`get_book` result→kart) + `item.tool`→kart dağıtıcı
- [X] T025 [US1] ECommerceWeb `ChatHistory.tsx` "tekrar ara": `/sohbet?q=` yerine sohbete `message` POST

**Checkpoint:** US1 tek başına çalışır — anon kitap keşfi + detay sohbette.

---

## Phase 4: US2 — Çok-alanlı niyet yönlendirme (P2)

**Hedef:** Aynı sohbette sepet/sipariş/kitaplık/adres/yorum; doğru alana yönlenir; kişiseller login.

**Bağımsız test:** Login kullanıcı 6 alanı sırayla sorar → her biri doğru kartta, yalnız kendi verisi.

- [X] T026 [US2] Order `get_my_orders` 007'den `Domains/Orders/Features/Agents/Queries/GetMyOrders.cs` + `Program.cs` `MapMcp`
- [X] T027 [US2] Library `get_my_library` 007'den `Domains/UserPurchase/Features/Agents/Queries/GetMyLibrary.cs` + `Program.cs` `MapMcp`
- [X] T028 [US2] Customer `get_my_addresses` 007'den `Domains/AddressBooks/Features/Agents/Queries/GetMyAddresses.cs` + `Program.cs` `MapMcp`
- [X] T029 [P] [US2] Basket `get_basket` YENİ slice `Domains/Baskets/Features/Agents/Queries/GetBasket.cs` (sahip=sub) + `Program.cs` `MapMcp`
- [X] T030 [P] [US2] Reviews `get_book_reviews` YENİ slice `Domains/Reviews/Features/Agents/Queries/GetBookReviews.cs` (anon; özet ort/adet) + `Program.cs` `MapMcp`
- [~] T031 [US2] (013-BLOCKED — personalization BC master'a merge olunca) Personalization `get_search_history` YENİ slice `Domains/SearchHistory/Features/Agents/Queries/GetSearchHistory.cs` (login; 013 `SearchHistoryProjection` okuması paylaş) + `Program.cs` `MapMcp`
- [X] T032 [US2] `McpEndpointsOptions`: `basket`(login)+`reviews`(anon)+`personalization`(login) ekle; order/library/customer(login) teyit
- [X] T033 [P] [US2] ECommerceWeb kart bileşenleri: sepet/sipariş/kitaplık/adres/yorum/arama-geçmişi (tool→kart dağıtıcıya kayıt)
- [X] T034 [US2] Anon kişisel istek deneyimi doğrula (tool kutuda yok → model "giriş gerekli"); "arama geçmişimi sil" → agent ekrana yönlendirir (yazma yok)

**Checkpoint:** Tek sohbet yüzeyinden 6 alan okunur; auth-gating çalışır.

---

## Phase 5: US3 — Sonuç sayısı ve devam (P3)

**Hedef:** 20+ sonuçta toplam gösterilir; "devamını getir" → sonraki sayfa akışa eklenir (anon dahil).

**Bağımsız test:** 20+ sonuçlu arama → toplam görünür → "devamını getir" → 21–40 eklenir, tekrar yok.

- [ ] T035 [US3] `ScoutAgentFactory` system prompt: cevap metnine sayfa-izi ("… 1. sayfa") + "devamını getir"→`search_books page+1` talimatı + "arama geçmişimi sil"→ekrana yönlendir (yazma yok)
- [ ] T036 [P] [US3] ECommerceWeb kitap-kart altlığı: `total` + gösterilen aralık (tool verisinden, deterministik)
- [ ] T037 [US3] ECommerceWeb `ChatWorkspace`: anon son N turu `history`'de POST'lar (devam bağlamı); login'de sunucu kaydı

**Checkpoint:** Sayfalama konuşmayla çalışır; sayı tool'dan birebir.

---

## Phase 6: US4 — Login arama kaydı korunur (P3)

**Hedef:** Login+dolu-sorgu `UserSearched` yayar (013'e akar); anon kaydolmaz. Yayın agent yoluna taşınır.

**Bağımsız test:** Login sohbetten arama → arama geçmişinde (013) görünür; anon arama → hiç kayıt yok.

- [ ] T038 [US4] Catalog `search_books` handler: login+dolu-query → `UserSearched` publish (fanout); anon → yayınlama
- [ ] T039 [US4] Eski REST `/products/search` `UserSearched` yayınını **kaldır** (ChatWorkspace artık kullanmıyor; tek yayın kaynağı `search_books` tool'u — çift-yayın önlenir) [F1 kararı]
- [ ] T040 [P] [US4] `catalog/FLOW.md` + Personalization dikişi güncelle (yayın noktası REST→agent/tool taşındı)

---

## Phase 7: Polish & Cross-Cutting

- [ ] T041 [P] Dokunulan BC FLOW.md'lerine query-tool sticky (Catalog/Basket/Order/Library/Customer/Reviews/Personalization) — planned→as-built anchor
- [ ] T042 [P] ScoutAgent `FLOW.md` planned eklerini düş (as-built kenar-anchor)
- [ ] T043 [P] Yeni servis `launchSettings` teyit (Production açılma tuzağı — her istek 500)
- [ ] T044 `dotnet build && dotnet test` — tüm çözüm yeşil
- [ ] T045 Quickstart canlı senaryolar Aspire'da doğrula (US1-US4 + arama-geçmişi tool + dayanıklılık + sır-gizli)

---

## Dependencies & Execution

- **Setup (T001-T007)** → **Foundational (T008-T017)** her story'den ÖNCE.
- **US1 (T018-T025)** foundational sonrası; **MVP burada biter** — tek başına sunulabilir.
- **US2 (T026-T034)** foundational'a bağlı; US1'den bağımsız (ayrı tool'lar/kartlar).
- **US3 (T035-T037)** search_books (US1) üstüne; sayfalama.
- **US4 (T038-T040)** search_books (US1) üstüne; arama kaydı.
- **Polish (T041-T045)** en son.
- Test-first: T008→T009, T010→T011, T018→T019 (test task implementasyondan önce).

**Paralel fırsatlar:** T006/T007 (docs) · T008/T010 (test'ler) · US1 React T023/T024 · US2 tool'lar T029/T030/T031 + kartlar T033 · ADR/CLAUDE.md setup'ta.

**MVP:** Phase 1+2+3 (US1) — anon doğal-dil kitap keşfi; başlangıç bug'ı burada kapanır.
