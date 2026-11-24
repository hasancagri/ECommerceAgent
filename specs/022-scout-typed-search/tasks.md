# Tasks: Kâşif Tipli Niyet-Çıkarma (iki-faz kitap arama)

**Input**: `/specs/022-scout-typed-search/` (plan, spec, research, data-model, contracts, quickstart)

**Tests**: İlke VI — `FilterInterpreter` saf domain mantığı → test-first ZORUNLU. Endpoint/LLM/React
test-sonra/canlı doğrulama (İlke VI dışı).

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup

- [ ] T001 Branch `feat/021-content-discovery-v11` + `.slnx` zaten var; ek setup YOK (doğrula: `dotnet build`).

## Phase 2: Foundational (tüm story'ler buna bağlı)

- [ ] T002 [P] `src/agents/ScoutAgent.Api/Search/BookSearchFilters.cs` — NESTED kontrat kaydı:
  `BookSearchFilters(TextSlot? Text, PublisherSlot? Publisher, string? PriceHint, string? SortHint,
  string? BindingHint, string? PageHint, TaxonomySlot? Taxonomy, FlagsSlot? Flags, int Page=1, int PageSize=10)`
  + alt record'lar (TextSlot{Query,Author}, PublisherSlot{Include,Exclude}, TaxonomySlot{Genres,Moods,
  Themes,Audience}, FlagsSlot{OnlyDiscounted,OnlyInStock,OnlySalable}); `[Description]` öznitelikleri (schema).
- [ ] T003 [US3] `tests/ScoutAgent.Api.Tests/FilterInterpreterTests.cs` — TEST-FIRST: PriceHint parse
  ("300 TL altı"→max=300; "100-300"→min=100,max=300; "ucuz"→null-sessiz-düş; boş→null), PageHint parse,
  grup-null güvenli düzleme, slot→arg pass-through (author/publisher/sort/binding/flags/page). Kırmızı.
- [ ] T004 [US3] `src/agents/ScoutAgent.Api/Search/FilterInterpreter.cs` — saf: BookSearchFilters →
  `Dictionary<string,object?>` (search_books argümanları). T003 yeşile döner.
- [ ] T005 `src/agents/ScoutAgent.Api/Search/ExtractPrompt.cs` — extract system prompt holder (spike v2:
  uydurma-yasağı + by/about + yayınevi-tanıma; Program.cs'e gömme — conventions).
- [ ] T006 `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Queries/ExtractIntent.cs` — slice:
  query → `IChatClient.GetResponseAsync<BookSearchFilters>` (catalog modeli, ExtractPrompt); hata→çıplak
  query fallback (bloklamaz). Result pattern.
- [ ] T007 `src/agents/ScoutAgent.Api/Domains/Conversations/Features/Commands/SearchTyped.cs` — slice:
  BookSearchFilters → FilterInterpreter → catalog `search_books` (mevcut MCP yolu reuse) → kartlar +
  şablon intro ("'X' için N kitap"); Conversation'a yazar (017); page+1 "devamını getir".
- [ ] T008 `src/agents/ScoutAgent.Api/Domains/Conversations/ConversationEndpointExtension.cs` —
  `POST /api/chat/v1/extract` (T006) + `POST /api/chat/v1/search` (T007) map; opsiyonel bearer +
  rate-limit "chat" (mevcut `/messages` deseni). Wolverine discovery gerekirse Program.cs kaydı.

**Not:** Catalog yazar-filtresi (SearchRequest.Author + jsonb `?|` ResolveAuthorIdsAsync + search_books
`author` param) bu oturumda EKLENDİ, build+26 test yeşil → foundational hazır, tekrar yapılmaz.

## Phase 3: US1 — Yazar eserlerini isteme (P1, MVP)

**Hedef:** "Mo Yan'ın kitapları" → yalnız Mo Yan. **Bağımsız test:** quickstart §3.

- [ ] T009 [US1] ExtractPrompt (T005) yazarlık kuralı: "X'in kitapları/eserleri/yazar X" → Text.Author;
  büyük/küçük-harf duyarsız (Catalog ResolveAuthorIds zaten normalize).
- [ ] T010 [US1] Canlı doğrula (quickstart §3, SC-001): 10 yazar × tekrar; dönen kartların %100'ü doğru
  yazar; eşleşme yoksa dürüst boş (uydurma kart yok).

## Phase 4: US2 — Yazarlık vs Hakkında (P2)

**Hedef:** "X hakkında" → konu (karışık); "X'in kitapları" → yazar. **Bağımsız test:** quickstart §4.

- [ ] T011 [US2] ExtractPrompt by/about kuralı + örnek: "X hakkında/üzerine/ile ilgili" → Text.Query
  (Author BOŞ); "X'in kitapları" → Text.Author.
- [ ] T012 [US2] Canlı doğrula (SC-002): "Dostoyevski hakkında" (karışık) vs "Dostoyevski'nin kitapları"
  (yalnız Dostoyevski); her biri 3/3 tutarlı.

## Phase 5: US3 — Çok-kriter + uydurma yok (P2)

**Hedef:** çok-kriterli sorgu doğru ayrışır; olmayan filtre eklenmez. **Bağımsız test:** quickstart §5.

- [ ] T013 [US3] ExtractPrompt uydurma-yasağı + yayınevi-tanıma kuralı (sorguda açıkça yoksa null/false;
  yayınevi kalıpları → Publisher).
- [ ] T014 [US3] Canlı doğrula (SC-003): "İş Bankası hariç 300 TL altı ciltli" → exclude+price+binding
  doğru, sort/stok/indirim UYDURULMAZ; çok-kriter setinde uydurma %0.

## Phase 6: US4 — Anladığının şeffaflığı (P3, React)

**Hedef:** sistemin çıkardığı filtreler ekranda görünür. **Bağımsız test:** quickstart §6.

- [ ] T015 [US4] ECommerceWeb (ayrı repo): iki-tur oto-relay (query→/extract→typed→/search); typed'ı
  "anladım" chip/rozet olarak göster (onay YOK, oto-relay). Pagination: son typed'ı taşı, page+1.

## Phase 7: Polish & Cross-Cutting

- [ ] T016 [P] `src/agents/ScoutAgent.Api/SpikeExtract.cs` SİL + Program.cs spike `MapSpikeExtract()` satırı SİL.
- [ ] T017 [P] `src/agents/ScoutAgent.Api/FLOW.md` — iki-faz kitap-arama süreci (extract→interpret→search;
  İlke VII, aynı PR).
- [ ] T018 Flaky-apology fallback gözden geçir (D7): yeni `/search` yolunda gereksiz; `/messages`
  (router+sub-agent) yolunda diğer niyetler için KALIR. Çift yol notu + gereksizse catalog yolundan ayıkla.
- [ ] T019 `dotnet build && dotnet test` tam yeşil; CLAUDE.md commit-öncesi kontrolü.
- [ ] T020 BC haritası/CLAUDE.md gerekirse not (yeni uç); memory güncelle.

## Dependencies

- Phase 2 (foundational) tüm story'leri bloklar; T003→T004 (test-first), T002→T006/T007, T005→T009/T011/T013.
- US1 (P1) = MVP; US2/US3 aynı extract/search mekanizmasını paylaşır (prompt kuralı + canlı doğrulama farkı).
- US4 (React) US1-3 backend'i hazır olunca.
- Polish en son.

## Parallel Opportunities

- T002 (kontrat) ‖ T003 (test) başlangıçta paralel yazılabilir (farklı dosya).
- T016 ‖ T017 (spike sil ‖ FLOW.md) paralel.
- US2/US3 prompt kuralları (T011/T013) aynı ExtractPrompt dosyasında → SIRALI (paralel değil).

## MVP Scope

**US1 (P1)** tek başına MVP: yazar araması güvenilir (en görünür hata kapanır). Foundational (Phase 2) +
US1 (Phase 3) = çalışır dilim. US2/US3 aynı mekanizmada prompt+doğrulama; US4 React cilası.
