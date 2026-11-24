# Feature Specification: /messages (router+sub-agent) emekliliği

**Feature Branch**: `feat/023-messages-teardown` · **Created**: 2026-11-24 · **Status**: Draft

## Niyet / neden
022'de kitap araması iki-faz tipli yola (`/extract`+`/search`) geçti; React artık `/messages`'ı HİÇ
çağırmıyor. `/messages` + router (`ScoutAgentFactory`) + 7 sub-agent + `CatalogSubAgent` (JSON hack
`ParseSearchFilters` + flaky fallback) + `PaginationIntent` **frontend-ölü**. Catalog-dışı 6 sub-agent
014'ten kalma iskelet (~47-56 satır, hiç yüzeylenmemiş). Karar: sohbet YALNIZ kitap keşfi
("keşif=sohbet, aksiyon=tık"); ölü yol + JSON hack sökülür.

## User Story 1 — Ölü /messages yolu kalkar, /extract+/search çalışmaya devam (P1)

**Bağımsız test:** silim sonrası `dotnet build` 0 hata + testler yeşil + canlı `/extract`→`/search`
(Mo Yan 6/6) + `/conversations`/`/transcribe` çalışır; `/messages` artık 404.

**Acceptance:**
1. **Given** teardown uygulandı, **When** `POST /api/chat/v1/messages`, **Then** 404 (uç yok).
2. **Given** teardown, **When** `/extract`→`/search` (yazar sorgusu), **Then** kartlar döner (bozulmadı).
3. **Given** teardown, **When** React sohbet (DiscoverWelcome'dan arama), **Then** kart + chip + sidebar/geçmiş (017) çalışır.

### Edge
- `SendMessage.Response/Reply/ReplyItem` SearchTyped tarafından kullanılıyor → SİLİNMEZ, ortak dosyaya TAŞINIR.
- Conversation kaydı (017 sidebar) `/search`'ten besleniyor → korunur.

## Requirements
- **FR-001**: `/api/chat/v1/messages` endpoint + `SendMessageRequest` kaldırılır.
- **FR-002**: Yalnızca `/messages` yolunun kullandığı tipler kaldırılır: `SendMessage` (handler/command/
  HistoryTurn/LastSearch/BuildContext/PaginateAsync), `ScoutAgentFactory` (router+RouterPrompt+GenerateTitle),
  7 sub-agent + `ISubAgent`, `PaginationIntent`, `SimilarBooksResult`/`SimilarBookItem`, `ParseSearchFilters`.
- **FR-003**: `Response`/`Reply`/`ReplyItem` ortak dosyaya taşınır; `SearchTyped` ona bağlanır.
- **FR-004**: KORUNUR: `McpToolSource`, `ToolResultCollector`, `SearchBooksResult`, `ChatClientFactory`,
  `ScoutModelResolver`, `ExtractIntent`, `SearchTyped`, `FilterInterpreter`, `BookSearchFilters`,
  `ExtractPrompt`, Conversation aggregate + Get/List/Rename/Delete/Transcribe + /extract /search /conversations /transcribe.
- **FR-005**: React ölüleri kaldırılır: `sendMessage`/`HistoryTurn`/`LastSearch` (chat.ts) + ChatWorkspace
  `lastSearchRef`/`lastSearchFromItems` (kullanılmıyorsa).
- **FR-006**: Her silim öncesi canlı-referans YOK teyidi (grep); build+test her repo'da yeşil.
- **FR-007**: FLOW.md'den /messages + planned-019 bölümleri çıkar; 022 iki-faz tek yol kalır.

## Success Criteria
- **SC-001**: `dotnet build` 0 hata, `dotnet test` tüm yeşil (silinen PaginationIntentTests hariç).
- **SC-002**: `/messages` 404; `/extract`+`/search` canlı çalışır (Mo Yan 6/6).
- **SC-003**: React build (tsc) temiz; ekran sohbet (arama+chip+pagination+sidebar) çalışır.
- **SC-004**: `grep -r ParseSearchFilters|ScoutAgentFactory|ISubAgent|PaginationIntent` → 0 sonuç.

## Assumptions
- React `/messages`'ı çağırmıyor (recon doğrulandı) → sökmek güvenli.
- Catalog-dışı sohbet niyetleri (sepet/yorum...) KAPSAM DIŞI, bilinçli (keşif=sohbet, aksiyon=tık).
- Title üretimi (`GenerateTitleAsync`) gider → yeni sohbet başlığı ilk mesaja düşer (SearchTyped zaten title üretmiyor).
