# Implementation Plan: Kâşif Tipli Niyet-Çıkarma (iki-faz kitap arama)

**Branch**: `feat/021-content-discovery-v11` | **Date**: 2026-11-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/022-scout-typed-search/spec.md`

## Summary

Kitap aramada kırılgan "iç LLM JSON param doldurur" adımını **tipli niyet-çıkarmayla** değiştir.
React ham sorgu yollar → ScoutAgent `/extract` MEAI strict structured output ile NESTED tipli slot
objesi üretir (LLM yalnız kullanıcının kelimelerini doğru slota koyar) → React tipi alır, beklemeden
`/search`'e relay eder → sunucu slotları DETERMİNİSTİK yorumlar (mevcut Catalog yorumlayıcıları reuse:
taksonomi/sort-metni/binding-metni/ad→id) → ParadeDB arar → kartlar. Spike (2026-11-24) de-riskledi:
structured output çalışıyor, NESTED güvenilir (FLAT ~%50 parse-fail), uydurma-yasağı+by/about+yayınevi
prompt'uyla gpt-4o-mini zor vakalarda 3/3 tutarlı.

## Technical Context

**Language/Version**: .NET 10 (C#), React/TypeScript (ECommerceWeb ayrı repo)

**Primary Dependencies**: Microsoft.Agents.AI 1.13 + Microsoft.Extensions.AI 10.7 (OpenAI gpt-4o-mini,
`GetResponseAsync<T>` structured output); ParadeDB pg_search (mevcut `ProductSearchQueries.SearchAsync`);
Marten; YARP gateway (`/api/chat/**` → scout-agent).

**Storage**: Marten (ScoutAgent conversation; Catalog catalogDb read-only). Yeni tablo YOK.

**Testing**: xUnit + Shouldly, saf unit (İlke VI). Yeni saf birim = slot→SearchRequest eşleme +
hint-metin parse. LLM çıkarma + arama canlı doğrulanır (Aspire).

**Target Platform**: Aspire AppHost (Linux/macOS dev); servisler container.

**Project Type**: Web (backend .NET servisleri + React frontend).

**Performance Goals**: Kitap arama turu ≤1 LLM çağrısı (bugünkü router+sub-agent 2-zar yerine tek
çıkarma). 2. tur deterministik (LLM yok).

**Constraints**: NESTED şema (FLAT kullanılamaz — spike ~%50 parse-fail). Extract prompt uydurma-yasağı
içermeli. Konuşulmayan grup = null. Sır insan-yüzeyde görünmez (İlke VIII) — tipli obje sır taşımaz.

**Scale/Scope**: catalog arama tek sub-agent (kanıt zemini); 22.5k kitap katalog. basket/customer
genellemesi KAPSAM DIŞI (sonraki dilim).

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bak.*

- **İlke I (BC izolasyonu):** ✅ ScoutAgent Catalog'a yalnız MCP/query ucuyla erişir (DB'sine değil);
  tipli çıkarma agent-içi; React↔ScoutAgent gateway üzerinden. Cross-BC DB yok.
- **İlke II (zengin aggregate):** ✅ Yeni aggregate yok; Conversation aggregate davranışı değişmez.
- **İlke III (VSA+CQRS, repo yok):** ✅ Yeni slice'lar ScoutAgent `Domains/Conversations/Features`
  altında (extract = query-benzeri okuma; search = mevcut arama yolu). Minimal API endpoint.
- **İlke IV (Result):** ✅ Handler'lar `FeatureObjectResultModel<T>` döner.
- **İlke V (scope yetki):** ✅ Arama anonim/opsiyonel-bearer (mevcut `/messages` deseni); yeni scope yok.
- **İlke VI (Domain-TDD):** ✅ Saf birim = NestedFilters→SearchRequest eşleme + hint-metin parse
  (fiyat/sayfa) → **test-first**. LLM/endpoint/React test-sonra/canlı (İlke VI kapsamı dışı).
- **İlke VII (FLOW.md):** ⚠️ Kitap-arama süreci değişiyor (iki-faz) → ScoutAgent FLOW.md **aynı PR'da**
  güncellenir (extract→interpret→search adımları).
- **İlke VIII (sır görünmez):** ✅ Tipli obje yalnız arama niyeti taşır (sır/PII yok); OpenAI anahtarı
  sunucuda kalır (mevcut ChatClientFactory).

**Sonuç:** Gate PASS. Tek aksiyon: FLOW.md güncellemesi (İlke VII), implementation'la aynı PR'da.

## Project Structure

### Documentation (this feature)

```text
specs/022-scout-typed-search/
├── plan.md              # bu dosya
├── research.md          # Faz 0 — karar kayıtları
├── data-model.md        # Faz 1 — tipli kontrat + eşleme
├── quickstart.md        # Faz 1 — canlı doğrulama rehberi
├── contracts/           # Faz 1 — /extract + /search uç sözleşmeleri
└── tasks.md             # /speckit-tasks çıktısı (bu komutta DEĞİL)
```

### Source Code (repository root)

```text
src/agents/ScoutAgent.Api/
├── Domains/Conversations/
│   ├── Features/Queries/ExtractIntent.cs      # YENİ: query → NestedFilters (structured output)
│   ├── Features/Commands/SearchTyped.cs        # YENİ: NestedFilters → kartlar (deterministik)
│   └── ConversationEndpointExtension.cs        # /extract + /search map
├── Search/BookSearchFilters.cs                 # YENİ: NESTED tipli kontrat (slot objesi)
├── Search/FilterInterpreter.cs                 # YENİ: NestedFilters → search_books args (saf, test-first)
└── SubAgents/CatalogSubAgent.cs                # catalog search_books yolu (reuse; param eşleme)

src/services/catalog/Catalog.Api/
└── (mevcut SearchAsync/SearchRequest/Author filtresi — bu oturumda eklendi; değişiklik minimal:
    hint-metin parse gerekiyorsa search_books'a priceHint/pageHint eklenebilir)

tests/ScoutAgent.Api.Tests/
└── FilterInterpreterTests.cs                   # YENİ: slot→args eşleme + hint parse (test-first)

ECommerceWeb (ayrı repo):
└── iki-tur akış: query → /extract → typed → /search; "anladım" filtre gösterimi
```

**Structure Decision**: Web (backend+frontend). Değişim ağırlığı ScoutAgent'ta (çıkarma + deterministik
eşleme). Catalog arama motoru + yazar filtresi hazır, reuse. React iki-tur oto-relay. Yeni BC/tablo yok.

## Complexity Tracking

> Anayasa ihlali yok — tablo boş.
