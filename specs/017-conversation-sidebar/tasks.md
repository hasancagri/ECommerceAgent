# Tasks: Sohbet Kayıt Sadakati + Kalıcı Sidebar

**Feature**: 017-conversation-sidebar · **Branch**: `017-conversation-sidebar`
**Girdi**: plan.md · spec.md · data-model.md · contracts/ · research.md

Backend = `src/agents/ScoutAgent.Api` + `tests/ScoutAgent.Api.Tests`.
Frontend = `~/dev/ECommerceWeb/src` (ayrı repo, remote yok).
Test-first yalnız saf domain (İlke VI): aggregate/VO. Handler/endpoint/UI test-sonra/canlı.

---

## Phase 1: Setup

- [X] T001 `ScoutResourceConstants`'a yeni kodlar ekle (başlık boş, başlık çok uzun) — `src/agents/ScoutAgent.Api/Constants/ScoutResourceConstants.cs`
- [X] T002 [P] `ScoutOptions`'a `MaxTitleLength` (≈100) ekle — `src/agents/ScoutAgent.Api/Options/ScoutOptions.cs`

---

## Phase 2: Foundational

Yok — her story kendi dikeyini taşır; paylaşılan blocking prereq yalnız Setup sabitleri (Phase 1).

---

## Phase 3: US1 — Kayıt sadakati (P1) 🎯 MVP

**Hedef:** Login sohbeti tekrar açınca metin + kitap kartları aynen görünür.
**Bağımsız test:** Kart dönen soru sor → kapat → tekrar aç → kartlar metinle gelir (kart kaybı 0).

### Domain test-first (İlke VI)

- [X] T003 [US1] `ConversationItemsTests` yaz (başarısız): `AddAssistantMessage_WithItems_StoresItems`, `_WithoutItems_ItemsNull`, `Create` ItemsJson taşır — `tests/ScoutAgent.Api.Tests/ConversationItemsTests.cs`

### Implementasyon

- [X] T004 [US1] `ChatMessage` VO'ya `string? ItemsJson` + `Create(role,text,createdAt,itemsJson=null)` — `src/agents/ScoutAgent.Api/Domains/Conversations/ValueObjects/ConversationValueObjects.cs`
- [X] T005 [US1] `Conversation.AddAssistantMessage(text, string? itemsJson=null)` kartı mesaja yazar; test yeşile — `src/agents/ScoutAgent.Api/Domains/Conversations/Conversation.cs`
- [X] T006 [US1] `SendMessage`: `reply.Items`'ı JSON serialize edip `AddAssistantMessage`'a geçir; `BuildContext` DOKUNMA — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SendMessage.cs`
- [X] T007 [US1] `GetConversation.MessageView`'a `ItemsJson` ekle, assistant turunda doldur — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Queries/GetConversation.cs`
- [X] T008 [P] [US1] `ConversationDetail.messages[]`'a `itemsJson?: string` ekle — `~/dev/ECommerceWeb/src/api/chat.ts`
- [X] T009 [US1] `ChatWorkspace`: reload'da assistant mesajının `itemsJson`'ını parse edip mevcut kart renderer ile çiz — `~/dev/ECommerceWeb/src/pages/ChatWorkspace.tsx`

**Checkpoint:** US1 tek başına çalışır; kart reload sadakati doğrulanabilir.

---

## Phase 4: US2 — Kalıcı sidebar (P1)

**Hedef:** Sohbet listesi her ekranda kalıcı sol panelde; tıkla-aç-devam, yeni-sohbet.
**Bağımsız test:** Farklı rotalarda panel durur; sohbete tıkla → yüklenir.

- [X] T010 [US2] `ConversationSidebar` bileşeni: login `listConversations` + anon `listAnon`, en-son-üstte, yeni-sohbet düğmesi, satır = link `/sohbet?c=id`, aktif-vurgu URL param'dan — `~/dev/ECommerceWeb/src/components/ConversationSidebar.tsx`
- [X] T011 [US2] `PublicLayout` nav sidebar'ına `ConversationSidebar`'ı göm (her rota) — `~/dev/ECommerceWeb/src/layouts/PublicLayout.tsx`
- [X] T012 [US2] `ChatWorkspace`'in kendi iç sohbet sidebar'ını (satır ~356-375) kaldır; aktif-id'yi URL `?c=`'den oku — `~/dev/ECommerceWeb/src/pages/ChatWorkspace.tsx`

**Checkpoint:** Panel her ekranda; US1 + US2 birlikte MVP deneyimi.

---

## Phase 5: US3 — Rename / Sil (P2)

**Hedef:** Satır-başı yeniden adlandır (guard'lı, sıra bozmaz) + HARD sil (sahiplik).
**Bağımsız test:** Rename → başlık değişir, sıra sabit; sil → listeden+DB'den kalkar; açık silinirse pane sıfırlanır.

### Domain test-first (İlke VI)

- [X] T013 [US3] `ConversationRenameTests` yaz (başarısız): `Rename_EmptyTitle_ReturnsError`, `_Trims`, `_TooLong_ReturnsError`, `_SetsTitle`, `_DoesNotChangeUpdatedTime` — `tests/ScoutAgent.Api.Tests/ConversationRenameTests.cs`

### Backend

- [X] T014 [US3] `Conversation.Rename(title)`: boş→Error, trim, `MaxTitleLength` aşımı→Error, **UpdatedTime DOKUNMA**; test yeşile — `src/agents/ScoutAgent.Api/Domains/Conversations/Conversation.cs`
- [X] T015 [US3] `RenameConversation` slice (command+Response+Handler, sahiplik, `IDocumentSession`) — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/RenameConversation.cs`
- [X] T016 [US3] `DeleteConversation` slice (HARD `session.Delete<Conversation>`, sahiplik guard) — `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/DeleteConversation.cs`
- [X] T017 [US3] `ChatEndpointExtension`: `PATCH /conversations/{id}` (body title, 200/400/404) + `DELETE /conversations/{id}` (204/404), `RequireAuthorization` + sahiplik — `src/agents/ScoutAgent.Api/ChatEndpointExtension.cs`
- [X] T018 [US3] Wolverine keşif: yeni handler'lar `*Handler` adıyla taranır — doğrula; gerekirse `Program.cs`'e `IncludeType` — `src/agents/ScoutAgent.Api/Program.cs`

### Frontend

- [X] T019 [P] [US3] `renameConversation(id,title,token)` + `deleteConversation(id,token)` ekle — `~/dev/ECommerceWeb/src/api/chat.ts`
- [X] T020 [US3] `ConversationSidebar` satırına "..." menü: rename (inline/modal) + sil (onay); login REST, anon local; **açık sohbet silinirse** pane yeni-sohbet'e sıfırla + `conversationId.ref` temizle — `~/dev/ECommerceWeb/src/components/ConversationSidebar.tsx` (+ `ChatWorkspace.tsx` reset kancası)

**Checkpoint:** Tam Claude-tarzı sidebar aksiyonları çalışır.

---

## Phase 6: US4 — Anon oturumluk (P3)

**Hedef:** Girişsiz sohbetler sidebar'da oturumluk; rename/sil yerel; sekme kapanınca gider.
**Bağımsız test:** Anon iki sohbet panelde; rename/sil yerel; sekme kapat-aç → gittiler.

- [X] T021 [US4] `renameAnon(id,title)` ekle (`deleteAnon` var) — `~/dev/ECommerceWeb/src/lib/anonChats.ts`
- [X] T022 [US4] `ConversationSidebar` anon dalını bağla: `listAnon`/`renameAnon`/`deleteAnon`, sunucu çağrısı yok — `~/dev/ECommerceWeb/src/components/ConversationSidebar.tsx`

**Checkpoint:** Login + anon parite.

---

## Phase 7: Polish & Cross-Cutting

- [X] T023 [P] Eski sayfayı kaldır: `ChatHistory.tsx` sil + `/sohbet-gecmisi` rotasını `App.tsx`'ten çıkar — `~/dev/ECommerceWeb/src/pages/ChatHistory.tsx`, `~/dev/ECommerceWeb/src/App.tsx`
- [X] T024 013 "Aramaların" canlı mantığını (sayfalama/sil/temizle + token) `AccountSettings`'e taşı; `PublicLayout` nav linkini güncelle (ölü link yok) — `~/dev/ECommerceWeb/src/pages/AccountSettings.tsx`, `~/dev/ECommerceWeb/src/layouts/PublicLayout.tsx`
- [X] T025 FLOW.md güncelle (İlke VII): yeni Rename/Delete komutları + assistant mesajı kart taşır — `src/agents/ScoutAgent.Api/FLOW.md`
- [X] T026 `dotnet build && dotnet test` yeşil; Aspire + ECommerceWeb ile quickstart US1-US4 elle doğrula

---

## Bağımlılıklar

- Setup (T001-T002) → her şeyden önce.
- US1 (T003-T009): T003→T004→T005; T006/T007 T005 sonrası; T008/T009 frontend.
- US2 (T010-T012): US1 frontend (T009) kart renderer'ına dayanır; sonra.
- US3 (T013-T020): T013→T014; T015-T017 T014 sonrası; T019-T020 sidebar (T010) sonrası.
- US4 (T021-T022): sidebar (T010) + US3 frontend sonrası.
- Polish (T023-T026): tüm story'ler sonrası; T025 FLOW aynı PR.

## Paralel fırsatlar

- T002 ‖ T001. T008 ‖ backend US1. T019 ‖ backend US3.
- Backend (ScoutAgent) ‖ frontend (ECommerceWeb) büyük ölçüde ayrı repo → eşzamanlı sürülebilir
  (kontrat `contracts/conversation-endpoints.md` sabit).

## MVP

US1 (kart sadakati) + US2 (kalıcı sidebar) = kullanıcının asıl istediği çekirdek. US3/US4 artımlı.
