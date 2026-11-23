# Kâşif Sayfalama ("devamını getir") Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** "devamını getir" sohbette son aramanın AYNI filtreleriyle page+1 döndürür; LLM filtre-türetmesi bu yolda devre dışı → sıfır drift.

**Architecture:** React son aramanın `{filters, page}`'ini saklar, HER mesajda `lastSearch` olarak taşır. Backend saf bir kuralla "devamını getir" niyetini yakalar; eşleşir + `lastSearch` doluysa router/LLM'i ATLAR, catalog MCP `search_books`'u verilen filtre + page+1 ile doğrudan çağırır, kartı deterministik metinle normal asistan turu olarak kaydeder. Eşleşmezse normal LLM akışı.

**Tech Stack:** .NET 10, Microsoft.Agents.AI + MCP, Marten, xUnit+Shouldly (backend); React+TS+Vite (ECommerceWeb, ayrı repo).

**Spec:** `docs/superpowers/specs/2026-11-23-scout-pagination-design.md`

## Global Constraints

- Backend değişiklikleri ScoutAgent.Api içinde; contract alanları ADDITIVE (opsiyonel, default'lu) — eski istemci kırılmaz.
- Test disiplini (CLAUDE.md): yalnız SAF domain birimi test-first (pagination-niyet kuralı). Handler/MCP/React = test-sonra, Aspire'da canlı doğrulanır (WebApplicationFactory/Testcontainers YOK).
- `IConfiguration` doğrudan okuma YOK; Options pattern. Filtre sözlüğü string-keyed `Dictionary<string, object?>` (ParseSearchFilters ile aynı şekil).
- React repo: `~/dev/ECommerceWeb`. Chat API tek origin gateway `/api/chat/**`.

## Review Focus

- Pagination ifadesi var AMA `lastSearch` yok (ör. ilk mesaj "devamını getir") → çökmeden normal LLM akışına düş. → Task 3 testi.
- `lastSearch` dolu ama mesaj pagination DEĞİL (yeni arama) → lastSearch yok sayılır, normal akış. → Task 1 (kural false) + Task 3.
- page+1 toplamı aşıyor (son sayfa) → `search_books` 0 item → deterministik "başka sonuç yok" metni, boş kart değil çökme. → Task 3 testi.
- Türkçe casing/fold ("Devamını Getir", "DEVAMINI", "diğer sonuçları getir") → normalize eşleşme. → Task 1 testleri.
- Anon (kayıt persist yok) → sayfalı kartlar yine döner, client-transkript korunur. → Task 3 canlı doğrulama.

---

### Task 1: Pagination-niyet kuralı (saf, test-first)

**Files:**
- Create: `src/agents/ScoutAgent.Api/Agents/PaginationIntent.cs`
- Test: `tests/ScoutAgent.Api.Tests/PaginationIntentTests.cs`

**Interfaces:**
- Produces: `static class PaginationIntent { static bool IsMore(string message) }` — Türkçe-fold + trim ile mesajı "devamını getir / devam / sonraki / diğer sonuç(lar)(ı) / daha fazla / devamı" kümesine karşı eşler (içerir-mantığı, noktalama toleranslı). Boş/null → false.

- [ ] **Step 1: Failing testleri yaz** — `tests/ScoutAgent.Api.Tests/PaginationIntentTests.cs`

```csharp
[Theory]
[InlineData("devamını getir")]
[InlineData("Devamını Getir")]
[InlineData("DEVAMINI GETİR")]
[InlineData("diğer sonuçları getir")]
[InlineData("sonraki")]
[InlineData("daha fazla göster")]
public void IsMore_PaginationPhrase_ReturnsTrue(string msg) =>
    PaginationIntent.IsMore(msg).ShouldBeTrue();

[Theory]
[InlineData("Can Yayınları'ndan roman")]
[InlineData("tarihi kitaplar")]
[InlineData("")]
[InlineData(null)]
public void IsMore_NonPaginationOrEmpty_ReturnsFalse(string? msg) =>
    PaginationIntent.IsMore(msg!).ShouldBeFalse();
```

- [ ] **Step 2: Testi çalıştır, FAIL gör** — `dotnet test tests/ScoutAgent.Api.Tests/ScoutAgent.Api.Tests.csproj --filter "FullyQualifiedName~PaginationIntent"` → FAIL (PaginationIntent yok).

- [ ] **Step 3: `PaginationIntent.IsMore` uygula** — `src/agents/ScoutAgent.Api/Agents/PaginationIntent.cs`. Mesajı lower-invariant + Türkçe İ/ı fold (var olan `NameNormalization` deseni varsa reuse; yoksa inline `ToLowerInvariant` + 'İ'→'i','I'→'i' değişimi) ile normalize et, kapalı ifade listesine karşı `Contains` kontrol. Liste `Program.cs`'te değil bu dosyada static.

- [ ] **Step 4: Testi çalıştır, PASS gör** — aynı filtre komutu → PASS.

- [ ] **Step 5: Commit**

```bash
git add src/agents/ScoutAgent.Api/Agents/PaginationIntent.cs tests/ScoutAgent.Api.Tests/PaginationIntentTests.cs
git commit -m "feat(scout): pagination-niyet kuralı (devamını getir)"
```

---

### Task 2: Filtre echo (search_books cevabı kullanılan filtreleri taşır)

**Files:**
- Modify: `src/agents/ScoutAgent.Api/Agents/ToolResultCapture.cs`
- Modify: `src/agents/ScoutAgent.Api/SubAgents/CatalogSubAgent.cs`
- Modify: `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SendMessage.cs`

**Interfaces:**
- Produces: `ToolResultCollector.Add(string tool, object? result, object? filters = null)`; `Results` tuple `(string Tool, object? Result, object? Filters)`. `SendMessage.ReplyItem(string Tool, object Result, object? Filters = null)`.
- Consumes: CatalogSubAgent `search_books` wrapper'ı `collector.Add("search_books", typed, args)` çağırır (args = ParseSearchFilters çıktısı, page dahil).

- [ ] **Step 1: Collector'ı genişlet** — `ToolResultCapture.cs`: tuple'a `object? Filters` ekle, `Add` opsiyonel `filters` parametresi alsın (default null).

- [ ] **Step 2: ReplyItem'a Filters ekle** — `SendMessage.cs`: `record ReplyItem(string Tool, object Result, object? Filters = null)`. `BuildItems` collector tuple'ından `Filters`'ı geçirsin. (camelCase serileştirme zaten var → React `filters` görür.)

- [ ] **Step 3: CatalogSubAgent echo** — `CatalogSubAgent.cs` `SearchBooks`: `collector.Add("search_books", typed, args)` (args = parse edilen Dictionary). `similar_books` dokunulmaz (filters null).

- [ ] **Step 4: Build** — `dotnet build src/agents/ScoutAgent.Api/ScoutAgent.Api.csproj` → 0 hata. (Birim test yok — contract/handler yolu; Task 3 sonu canlı doğrulanır.)

- [ ] **Step 5: Commit**

```bash
git add src/agents/ScoutAgent.Api/Agents/ToolResultCapture.cs src/agents/ScoutAgent.Api/SubAgents/CatalogSubAgent.cs src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SendMessage.cs
git commit -m "feat(scout): search_books cevabı kullanılan filtreleri echo'lar"
```

---

### Task 3: Backend continuation/bypass (SendMessageHandler)

**Files:**
- Modify: `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SendMessage.cs`
- Modify: `src/agents/ScoutAgent.Api/Domains/Conversations/ConversationEndpointExtension.cs` (gövde `lastSearch`'ü bind edip Command'a geçir)
- Test: `tests/ScoutAgent.Api.Tests/PaginationIntentTests.cs` (sınır: IsMore kuralı — handler MCP yolu Aspire'da canlı)

**Interfaces:**
- Consumes: `PaginationIntent.IsMore` (Task 1); `McpToolSource.GetClientAsync`/`DeserializeResult<SearchBooksResult>`; `ToolResultCollector.Add(...,filters)` ve `ReplyItem(...,Filters)` (Task 2).
- Produces: `SendMessage.Command(Guid? ConversationId, string Message, Guid? UserId, IReadOnlyList<HistoryTurn>? History, LastSearch? LastSearch)`; `record LastSearch(Dictionary<string, object?> Filters, int Page)`.

- [ ] **Step 1: Command + LastSearch tipi** — `SendMessage.cs`: `Command`'a `LastSearch? LastSearch = null` ekle; `record LastSearch(Dictionary<string, object?> Filters, int Page)` tanımla. Handler ctor'a `McpToolSource mcp` enjekte et.

- [ ] **Step 2: Bypass yolu** — `Handle` içinde, conversation yükle + `AddUserMessage` SONRASI, agent.RunAsync ÖNCESİ:
  ```
  if (command.LastSearch is not null && PaginationIntent.IsMore(command.Message))
      reply = await PaginateAsync(command.LastSearch, collector, ct);
  else { /* mevcut agent akışı */ }
  ```
  `PaginateAsync`: args = `new Dictionary<string,object?>(LastSearch.Filters)`, `args["page"] = LastSearch.Page + 1`; `search_books` MCP çağır; `DeserializeResult<SearchBooksResult>`; `collector.Add("search_books", typed, args)`; metin = `typed.Items.Count == 0 ? "Başka sonuç kalmadı." : $"Sonraki sayfa — {(typed.Page-1)*pageSize+1}–{(typed.Page-1)*pageSize+typed.Items.Count}, toplam {typed.Total}."` (pageSize = args'tan, yoksa 10). `isNewConversation=false` → başlık üretme atlanır (zaten false).

- [ ] **Step 3: Endpoint bind** — `ConversationEndpointExtension.cs`: `/messages` gövde DTO'suna `LastSearch? lastSearch` ekle (opsiyonel), Command'a geçir. Anon + login aynı.

- [ ] **Step 4: Build + unit** — `dotnet build` (0 hata) + `dotnet test --filter PaginationIntent` (Task 1 yeşil kalır).

- [ ] **Step 5: Canlı doğrula (Aspire)** — AppHost ayakta, React'ten: (a) "Can Yayınları roman" ara → kart gelir; (b) "devamını getir" → AYNI publisher, page 2, farklı kitaplar (önceki total korunur); (c) tekrar "devamını getir" → page 3; (d) son sayfadan sonra → "Başka sonuç kalmadı."; (e) "devamını getir" İLK mesaj olarak (lastSearch yok) → normal LLM akışı, çökme yok. DIAG gerekmez (page+1 deterministik); gerekirse geçici log.

- [ ] **Step 6: Commit**

```bash
git add src/agents/ScoutAgent.Api/Domains/Conversations/
git commit -m "feat(scout): devamını getir → son aramanın page+1'i (LLM-atla bypass)"
```

---

### Task 4: React lastSearch state + taşıma

**Files:**
- Modify: `~/dev/ECommerceWeb/src/api/chat.ts`
- Modify: `~/dev/ECommerceWeb/src/pages/ChatWorkspace.tsx`

**Interfaces:**
- Consumes: backend `ReplyItem.filters` (Task 2) + `/messages` gövde `lastSearch` (Task 3).
- Produces: `sendMessage` gövdesi opsiyonel `lastSearch?: { filters: Record<string, unknown>; page: number }`.

- [ ] **Step 1: API tipi** — `chat.ts`: `ReplyItem`'a `filters?: Record<string, unknown>`; `sendMessage` body tipine `lastSearch?: { filters: Record<string, unknown>; page: number }`.

- [ ] **Step 2: State + yakala** — `ChatWorkspace.tsx`: `lastSearchRef = useRef<{filters, page} | undefined>()`. Her cevap sonrası `res.reply.items`'te `tool==='search_books'` + `filters` olan item bul; varsa `lastSearchRef.current = { filters: item.filters, page: (result.page ?? result.Page ?? 1) }`.

- [ ] **Step 3: Taşı** — `send()`: body'ye `lastSearch: lastSearchRef.current` ekle (varsa). Yeni-arama cevabı geldiğinde Step 2 page'i tazeler (pagination cevabı da search_books döndüğü için page otomatik güncellenir).

- [ ] **Step 4: Canlı doğrula** — Task 3 Step 5 senaryoları React UI'da: kartlar sayfa sayfa eklenir, publisher sabit kalır, "N sonuç · 1–10/11–20…" ilerler.

- [ ] **Step 5: Commit (ECommerceWeb repo)**

```bash
cd ~/dev/ECommerceWeb && git add src/api/chat.ts src/pages/ChatWorkspace.tsx
git commit -m "feat(chat): devamını getir için lastSearch filtre+page taşı"
```

---

## Notlar

- DIAG logları (CatalogSubAgent) zaten kaynaktan söküldü; bu plan onlara dokunmaz.
- Bug 2 (maxPageCount "sayfa az" uydurma) + Bug 3 (çıplak-string düşüşü) bu planın HEDEF-DIŞI'nda; ayrı iş.
- Çalışan binary her backend task'tan sonra rebuild + scout restart (AppHost restart ya da DCP) ister; canlı adımlar bunu varsayar.
