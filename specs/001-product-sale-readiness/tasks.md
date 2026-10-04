---
description: "Task list for Product Sale Readiness (Completeness Gating)"
---

# Tasks: Product Sale Readiness (Completeness Gating)
**Input**: `spec.md` — anayasadaki "Küçük" kademe (Artefakt Ölçekleme).
**Tests**: DAHİL — saf domain birim testleri (xUnit + Shouldly), TDD.

## Görevler

- [x] T001 `tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj` oluştur (Basket.Api.Tests pattern'i; ProjectReference → Catalog.Api).
- [x] T002 Test projesini `ECommerceAgent.slnx` `/tests/` klasörüne kaydet.
- [x] T003 TDD (önce başarısız): `ProductCompletenessTests.cs` — boş açıklama/görselle `Product.Create` → `IsComplete/IsOnSale == false`; ikisi dolu + aktif → `true`; yalnız-whitespace açıklama → eksik.
- [x] T004 `Domains/Products/Product.cs`: kalıcı `bool IsComplete { get; private set; }`, computed `IsOnSale => IsActive && IsComplete`, `private RecalculateCompleteness()` (`!IsNullOrWhiteSpace(Description) && !IsNullOrWhiteSpace(ImageUrl)`); `Create`/`Update`/`UpdateImageUrl` sonunda çağır.
- [x] T005 `Features/Agent/SearchProducts.cs`: WHERE'e `&& x.IsComplete`.
- [x] T006 `Features/Agent/GetProduct.cs`: WHERE'e `&& x.IsComplete` (add_to_cart öncesi).
- [x] T007 `Features/Queries/GetProductByName.cs`: WHERE → `!IsDeleted && x.IsActive && x.IsComplete && Name.Contains(...)`.
- [x] T008 `ProductCompletenessTests.cs` geçiş testleri: `Update` ile tamamlanınca `true`; yalnız açıklama → hâlâ `false`; açıklama sonradan boşalınca satıştan düşer; `UpdateImageUrl` ile tamamlanır.
- [x] T009 `Features/Queries/GetAllProducts.cs`: `ProductResponse`'a `IsComplete` + `IsOnSale` ekle (filtre değişmez — admin hepsini görür).
- [x] T010 `dotnet build` + `dotnet test tests/Catalog.Api.Tests/...` geçer.
- [x] T011 E2E (Aspire): (1)✓ 200 seed ürün aramada görünmez; (2)✓ `Update` ile tamamlanan aktif ürün aramada çıkar (`IsOnSale=true`); (3)~ `Deactivate` HTTP endpoint'i yok (T008 birim testi kapsar); yerine canlı "tamlık kaybı → satıştan düşme" doğrulandı; (4)✓ `GetAllProducts` 200 ürünü `IsComplete/IsOnSale=false` listeler.

## Korunan tasarım kararları (retrospektif — kaldırılan research.md'den)

- **Tamlık kalıcı `bool IsComplete`, uçuşta hesaplanmaz** — Marten→Postgres WHERE'i (`IsActive && IsComplete`) ancak kalıcı alanla SQL'e/indekse çevrilir; computed getter whitespace-trim'i SQL'e çeviremez ve kuralı her sorguda tekrar ettirir (anayasa II).
- **`IsOnSale` saklanmaz, türetilir** — ayrı alan üçüncü senkron durum + drift riski demek.
- **Filtre yalnızca keşif/satın-almada** (`SearchProducts`, `GetProduct`, `GetProductByName`); **`GetProductById` filtrelenmez** — arama değil id-lookup, admin/UI detayınca kullanılır.
- **Migration yok** — eski dokümanlarda alan yoksa `false` deserialize edilir (satış-dışı = doğru varsayılan).
- Enrichment agent (AI açıklama + görsel) ayrı feature — spec Out of Scope.