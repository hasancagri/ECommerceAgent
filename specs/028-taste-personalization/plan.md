# Implementation Plan: Taste-vektör kişiselleştirme (Dilim-4)

**Branch**: `feat/028-taste-personalization` | **Date**: 2026-11-24 | **Spec**: [spec.md](./spec.md)

**Input**: `/specs/028-taste-personalization/spec.md`

> **MİMARİ REVİZYON (B, implement'te karar):** gRPC+m2m İPTAL (Catalog'un m2m client'ı yok → IdP seed/scope
> ağır). Yerine **ScoutAgent orkestrasyon:** Personalization `get_my_taste` PERSONAL MCP tool (user JWT,
> 013 deseni) → ScoutAgent → Catalog `recommend_books(vector,count)` anonim MCP tool. m2m/proto/gRPC YOK;
> mevcut token-forward + JWT. Taste vektör sunucu-içi (ScoutAgent) taşınır, insan/LLM/log'a çıkmaz (İlke VIII).
> Aşağıdaki gRPC/proto bölümleri bu revizyonla GEÇERSİZ — güncel akış tasks.md + contracts'ta.

## Summary

Kuzey yıldızı: login kullanıcıyı kitap embedding uzayına bir NOKTA (taste-vektör) koy → kNN = kişisel feed.
**Personalization** taste'i sahiplenir (user data): kullanıcının `UserSearchRecord` sorgu metinlerini OpenAI'la
embed edip ağırlıklı merkez üretir (on-demand), sanksiyonlu **gRPC** (`TasteQuery.GetTaste`) ile sunar.
**Catalog** `recommend_books` query-only MCP tool'u taste'i gRPC'yle çeker → `product_embedding` kNN (026
`SemanticSearchQueries` deseni) + hard filtre + WorkKey tekil + çeşitlilik; sinyal<3 → küratörlü (çok-satan).
**ScoutAgent** `/feed` ucu (login) `recommend_books`'u çağırır → kartlar. YouTube-modeli: feed=kişisel,
spesifik sorgu mevcut lexical/semantik yollarda (taste karışmaz). Cold-start: ~41 arama, 0 satın-alma/yorum.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: gRPC (`Grpc.AspNetCore` server=Personalization, client=Catalog; proto `Shared/Protos`) ·
Microsoft.Extensions.AI.OpenAI (`EmbeddingClientFactory` — Personalization'a eklenir, 025 deseni) · Npgsql
pgvector kNN (Catalog, 026 deseni) · Marten (UserSearchRecord okuma, künye) · Wolverine (mevcut) · ModelContextProtocol (recommend_books MCP)

**Storage**: personalizationDb (`UserSearchRecord` — OKU) · catalogDb `product_embedding` (kNN — OKU). Yeni kalıcı tablo YOK (taste on-demand; cache opsiyonel).

**Testing**: xUnit + Shouldly saf birimler — taste merkez-hesabı (ortalama/ağırlık), cold-start eşiği, çeşitlilik seçimi; gRPC/kNN/MCP canlı (İlke VI dışı).

**Target Platform**: Linux konteyner (Aspire)

**Project Type**: çok-BC (Personalization + Catalog + ScoutAgent)

**Performance Goals**: feed-load'da N sorgu embed (N≤~41, distinct daha az) + 1 kNN → ms-saniye; cache sonra. gRPC tek çağrı.

**Constraints**: İlke VIII taste-vektör insan-yüzeye/LLM'e render EDİLMEZ (opak) · kişisellik yalnız sorgu-YOK feed · query-only · model Catalog'la AYNI (aynı uzay)

**Scale/Scope**: cold-start (tek-haneli sinyal); MVP tek centroid, decay yok, satın-alma/yorum kablosu hazır (0 veri)

## Constitution Check

*GATE: Phase 0'dan önce. Phase 1 sonrası yeniden.*

- **İlke I (BC izolasyon + S2S):** PASS — taste Personalization'da (user data), kitap-kNN Catalog'da; cross-BC
  **sanksiyonlu gRPC** (Order→Basket `GetBasketItems` deseni; çağıran karşının API'sine erişir, DB'sine değil).
  recommend_books agent-içi query-only MCP (adr-mcp-inward-agent). UserSearched zaten event (012).
- **İlke III (VSA + CQRS):** PASS — Catalog `Features/Agents/Queries/RecommendBooks.cs` (+[McpServerToolType]);
  Personalization taste slice; ham-SQL kNN `Search/`'te; gRPC server ince sarmalayıcı (mantık servis class'ında).
- **İlke IV (Result):** PASS — handler'lar `FeatureObjectResultModel`.
- **İlke VI (domain-TDD):** PASS — saf birimler test-first: taste merkez-hesabı (`TasteVector.Average`/ağırlık),
  cold-start eşiği (`IsColdStart`), çeşitlilik/dedup seçimi. gRPC/kNN/MCP canlı.
- **İlke VII (FLOW.md):** PASS — Personalization FLOW Dilim-4 as-built + Catalog (recommend_books) + ScoutAgent (feed) dikişleri.
- **İlke VIII (sır/opak değer render yok):** PASS — taste-vektör gRPC'de taşınır, insan-yüzeye/LLM'e/loga ÇIKMAZ;
  kullanıcı yalnız kitap kartları görür (vektör sunucuda kalır).

İhlal YOK → Complexity Tracking boş. (gRPC bir ihlal değil, sanksiyonlu S2S istisnası.)

## Project Structure

```text
src/others/Shared/Protos/
└── taste_vector.proto                 # TasteQuery.GetTaste(userId) → {repeated float vector, int32 signal_count}

src/services/personalization/Personalization.Api/
├── Domains/Taste/
│   ├── TasteVector.cs                 # SAF: Average(embeddings)/ağırlık + IsColdStart(signalCount) (test-first)
│   └── Features/.../ComputeTaste.cs   # kullanıcının UserSearchRecord sorgularını embed → merkez (on-demand)
├── Grpc/TasteQueryGrpcService.cs      # gRPC server (ince sarmalayıcı; iş ComputeTaste'te) — scope-korumalı
├── Search/EmbeddingClientFactory.cs   # OpenAI embed (025 Catalog deseni, AYNI model)
└── Options/PersonalizationEmbeddingOptions.cs

src/services/catalog/Catalog.Api/
├── Domains/Products/Features/Agents/Queries/RecommendBooks.cs  # [McpServerToolType] recommend_books (personal; token→userId)
├── Search/RecommendQueries.cs         # ham-SQL: kNN(taste) + hard filtre + WorkKey tekil + çeşitlilik; cold→küratörlü
└── (gRPC client: AddGrpcClient<TasteQuery.TasteQueryClient> + S2S token handler)

src/agents/ScoutAgent.Api/
├── Domains/Conversations/.../Feed.cs  # GET /api/chat/v1/feed (login) → recommend_books MCP → kartlar
└── Agents/ToolResults.cs              # + RecommendBooksResult ayna-tipi
```

**Structure Decision**: Üç BC. Personalization = taste sahibi (embed+merkez+gRPC sun). Catalog = kitap-kNN
(gRPC client + recommend_books). ScoutAgent = feed yüzeyi. Her biri mevcut deseni izler (Basket gRPC server /
Order gRPC client / 026 kNN / 022 SearchTyped tool-çağrısı).

## Complexity Tracking

İhlal yok — boş.
