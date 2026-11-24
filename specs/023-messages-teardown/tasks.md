# Tasks: /messages emekliliği

Her silim ÖNCE `grep` ile canlı-referans YOK teyidi; her fazda build+test.

## Phase 1: Taşı (önce, kırmayı önle)
- [ ] T001 `Conversations/ChatReply.cs` YENİ — `ChatReply`(Text,Items) + `ReplyItem`(Tool,Result,Filters) +
  `ChatResponse`(ConversationId,Title?,Reply) tipleri (SendMessage'dan taşınan Response/Reply/ReplyItem).
- [ ] T002 `SearchTyped.cs` → yeni tiplere bağla (`SendMessage.Response/Reply/ReplyItem` yerine). Build.

## Phase 2: Backend sök (grep-teyit + build her adımda)
- [ ] T003 `ConversationEndpointExtension.cs`: `MapPost("/messages")` bloğu + `SendMessageRequest` record kaldır.
- [ ] T004 `Features/Commands/SendMessage.cs` SİL (taşındı; başka kullanan yok — teyit).
- [ ] T005 `Agents/ScoutAgentFactory.cs` SİL (router+RouterPrompt+GenerateTitleAsync; kullanan yalnız SendMessage).
- [ ] T006 `SubAgents/` SİL (CatalogSubAgent + Basket/Order/Library/Customer/Reviews/Personalization + ISubAgent).
- [ ] T007 `Agents/PaginationIntent.cs` + `tests/.../PaginationIntentTests.cs` SİL.
- [ ] T008 `Agents/ToolResults.cs`: `SimilarBooksResult`/`SimilarBookItem` kaldır (SearchBooksResult KORU — SearchTyped kullanıyor).
- [ ] T009 `dotnet build` 0 hata (DI Scrutor/Wolverine auto — Program.cs kaydı yok, teyit) + `dotnet test` yeşil.

## Phase 3: React sök
- [ ] T010 `src/api/chat.ts`: `sendMessage` + `HistoryTurn` + `LastSearch` kaldır (çağrılmıyor — teyit).
- [ ] T011 `ChatWorkspace.tsx`: `lastSearchRef`/`lastSearchFromItems` + ilgili ölü importlar kaldır (kullanılmıyorsa). tsc temiz.

## Phase 4: Belge + doğrula
- [ ] T012 `ScoutAgent.Api/FLOW.md`: /messages + planned-019 bölümlerini sök; 022 iki-faz tek yol + domain kuralları güncelle.
- [ ] T013 Canlı: Aspire restart → `/messages` 404 · `/extract`→`/search` Mo Yan 6/6 · `/conversations` çalışır (SC-002).
- [ ] T014 `grep -rE "ParseSearchFilters|ScoutAgentFactory|ISubAgent|PaginationIntent|SendMessage\."` → 0 (SC-004).

## Dependencies
Phase 1 (taşı) ÖNCE — yoksa SearchTyped kırılır. Sonra backend sök (T003→T009), React sök, belge, doğrula.
