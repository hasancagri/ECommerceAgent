# Implementation Plan: Sohbet Kayıt Sadakati + Kalıcı Sidebar

**Branch**: `017-conversation-sidebar` | **Date**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Input**: `specs/017-conversation-sidebar/spec.md` + tasarım `docs/superpowers/specs/2026-11-17-scout-conversation-persistence-sidebar-design.md`

## Summary

ScoutAgent `Conversation` aggregate'i assistant turunda **yapılı tool sonuçlarını (kitap
kartları)** opak `ItemsJson` ile saklar → reload'da birebir render. Bağlam kurulumu metin-only +
sınırlı kalır (zehirlenme yok, mevcut davranış). Kullanıcı-tetikli **Rename** (guard'lı, sıralama
bozmaz) + **HARD Delete** (sahiplik) command/endpoint'leri eklenir. Frontend: mevcut `PublicLayout`
nav sidebar'ına **kalıcı sohbet listesi** (her rota; login DB + anon sessionStorage), satır-başı
aç/rename/sil; eski `/sohbet-gecmisi` kaldırılır, 013 arama-geçmişi görünümü `AccountSettings`'e taşınır.

**Ölçek kademesi:** Tam akış — yeni endpoint kontratı (PATCH/DELETE) + aggregate şekil değişimi +
FLOW.md süreç güncellemesi var. (Yeni tablo/integration-event YOK; Marten alanı otomatik.)

## Technical Context

**Language/Version**: C# / .NET 10 (backend) · TypeScript / React (ECommerceWeb, Vite)

**Primary Dependencies**: Marten (scoutDb document store) · Wolverine (in-proc bus) ·
Microsoft.Agents.AI + MCP (mevcut, dokunulmaz) · React Router · lucide-react

**Storage**: Postgres `scoutDb`, şema `scout`, belge `mt_doc_conversation`. Anon: tarayıcı sessionStorage.

**Testing**: xUnit + Shouldly, `tests/ScoutAgent.Api.Tests` (saf domain, test-first — İlke VI).

**Target Platform**: Aspire AppHost tam-stack (backend) · tarayıcı SPA (frontend).

**Project Type**: Web (backend servis + frontend SPA).

**Performance/Constraints**: Bağlam maliyeti SABİT kalmalı (SC-005) — `BuildContext` dokunulmaz,
kartlar bağlama girmez. Reload kart kaybı 0 (SC-001).

**Scale/Scope**: Tek BC (ScoutAgent) + tek frontend repo. ~2 aggregate metodu, 2 yeni command/endpoint,
2 query genişlemesi, 1 sidebar bileşeni + layout entegrasyonu, 1 sayfa kaldırma + 1 taşıma.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar.*

- **İlke I (BC izolasyonu):** ✅ Her şey ScoutAgent BC içinde (scoutDb). Yeni BC-arası event/çağrı
  YOK. 013 arama-geçmişi backend'ine dokunulmaz (yalnız frontend görünümü taşınır).
- **İlke II (zengin aggregate):** ✅ `Rename`/`AddAssistantMessage(items)` davranışı aggregate'te;
  invariant (boş başlık red, sahiplik) kökte. `ItemsJson` VO alanı (opak string).
- **İlke III (VSA+CQRS, repo yok):** ✅ Yeni slice'lar `Features/Commands/{RenameConversation,
  DeleteConversation}`; handler `IDocumentSession`. Query genişlemesi `Features/Queries`.
- **İlke IV (Result):** ✅ Aggregate `ResultDomain`, handler `FeatureObjectResultModel`/
  `FeatureResultModel`. Hata kodları `ScoutResourceConstants`.
- **İlke V (scope yetki):** ✅ 013 deseni — kişisel veri, `RequireAuthorization` + sahiplik, kimlik
  JWT `sub`; named-scope YOK. Anon yazma yok (local).
- **İlke VI (Domain-TDD):** ✅ `ChatMessage.Create(items)`, `AddAssistantMessage(items)`, `Rename`
  guard'ları test-first; tasks'ta test implementasyondan önce.
- **İlke VII (FLOW.md):** ⚠️ Domain süreci değişiyor (yeni Rename/Delete komutları, assistant mesajı
  artık kart taşır) → `src/agents/ScoutAgent.Api/FLOW.md` **aynı PR'da** güncellenir (task eklendi).
- **İlke VIII (sır görünmezliği):** ✅ Kart verisi ürün künyesi — sır değil. İhlal yok.

**Sonuç:** Geçti. Complexity Tracking gerekmez.

## Project Structure

### Documentation (this feature)

```text
specs/017-conversation-sidebar/
├── plan.md
├── spec.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── conversation-endpoints.md
└── checklists/requirements.md
```

### Source Code (repository root)

```text
src/agents/ScoutAgent.Api/
├── FLOW.md                                   # süreç güncellemesi (İlke VII)
└── Domains/Conversations/
    ├── Conversation.cs                       # Rename() + AddAssistantMessage(items)
    ├── ValueObjects/ConversationValueObjects.cs  # ChatMessage.ItemsJson
    └── Features/
        ├── Commands/
        │   ├── SendMessage.cs                # reply.Items → saklama
        │   ├── RenameConversation.cs         # YENİ
        │   └── DeleteConversation.cs         # YENİ
        └── Queries/GetConversation.cs        # ItemsJson döndür
    └── ChatEndpointExtension.cs              # PATCH + DELETE /conversations/{id}

tests/ScoutAgent.Api.Tests/
├── ConversationItemsTests.cs                 # YENİ (kart saklama)
└── ConversationRenameTests.cs                # YENİ (guard'lar)

# Frontend
~/dev/ECommerceWeb/src/
├── layouts/PublicLayout.tsx                  # kalıcı sohbet listesi gömülür
├── components/ConversationSidebar.tsx        # YENİ (liste + aç/rename/sil)
├── pages/ChatWorkspace.tsx                   # iç sidebar kalkar; reload kart render
├── pages/ChatHistory.tsx                     # KALDIRILIR
├── pages/AccountSettings.tsx                 # 013 arama-geçmişi buraya
├── api/chat.ts                               # rename/deleteConversation + items tipi
└── lib/anonChats.ts                          # renameAnon eklenir
```

**Structure Decision**: Web — backend `src/agents/ScoutAgent.Api` (mevcut ScoutAgent BC), frontend
`~/dev/ECommerceWeb` (ayrı repo, remote yok). İkisi ayrı commit; backend `.slnx` içinde.

## Complexity Tracking

> Constitution Check temiz — gerek yok.
