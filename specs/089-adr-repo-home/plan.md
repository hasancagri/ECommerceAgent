# Implementation Plan: ADR'leri Repo İçine Taşı (dev-anında gerekçe kaynağı)

**Branch**: `089-adr-repo-home` | **Date**: 2026-10-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/089-adr-repo-home/spec.md`

## Summary

10 mimari karar kaydı (ADR) bugün dış Obsidian vault'ta (`~/dev/EcommerceNotes/ECommerceAgent/reference/`)
yaşıyor ve bağlayıcı değil; dev/ajan kod yazarken gerekçeyi kaynak-of-truth katmanında bulamıyor. Bu
feature ADR'leri repo içine `docs/adr/` altına taşır: slug'lar korunur, vault wikilink'leri repo-içi
biçime çevrilir, bir indeks (`docs/adr/README.md`) eklenir, `CLAUDE.md` + `conventions.md` ADR katmanını
işaret eder, ölü slug atıfı (`specs/049`) düzeltilir ve vault'tan karar-kayıtları kaldırılır (öğrenme
notları kalır). **Pure-docs feature — kod/şema/event/test yok.**

## Technical Context

**Language/Version**: N/A (Markdown dokümantasyon taşıması)

**Primary Dependencies**: N/A — düz Markdown; git mv/edit; grep doğrulaması

**Storage**: Repo dosya sistemi (`docs/adr/`); kaynak = dış vault (`~/dev/EcommerceNotes/ECommerceAgent/reference/`)

**Testing**: Doğrulama = grep/okuma script'i (quickstart.md); otomatik test harness'ı YOK (docs)

**Target Platform**: Repo (GitHub + local); okuyucu = geliştirici + kod yazan AI ajan

**Project Type**: Dokümantasyon katmanı (kod değil)

**Performance Goals**: N/A

**Constraints**: 0 ölü işaretçi (SC-001); 0 çiftlenmiş kural (SC-004); hassas değer ADR metninde render edilmez

**Scale/Scope**: 10 ADR dosyası + 1 indeks; 2 repo doküman (CLAUDE.md, conventions.md) güncellemesi;
1 ölü-slug düzeltmesi (specs/049); 2 vault öğrenme-notu güncellemesi + 10 vault silme

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Anayasa (v1.11.1) ilkeleri **kod/mimari** içindir; bu feature dokümantasyon taşımasıdır, kod üretmez.

- **İLKE I–VI (BC izolasyon / aggregate / VSA / Result / scope / TDD):** Uygulanmaz — kod dokunuşu yok. ✅
- **İLKE VII (Domain Süreci Legibility):** FLOW.md'lere dokunulmaz; ADR katmanı FLOW.md'den ayrı
  (süreç değil karar-gerekçe belgesi). ✅
- **Artefakt Ölçekleme:** Normalde docs taşıması "Trivial"dir. Ancak kullanıcı `/speckit-plan`'ı açıkça
  istedi + çapraz-dosya tutarlılık (slug çözümü, dup-guard, vault senkronu) kayda değer → tam akış
  yürütülür. Boş-doğru artefakt üretilmez; her artefakt bu taşımanın gerçek kararını taşır. ✅
- **Gerçek-kaynak sırası (CLAUDE.md):** kod + CLAUDE.md > memory > vault. Bu feature ADR'yi vault'tan
  alıp repo'ya (kaynak-of-truth katmanı) koyarak bu sırayı GÜÇLENDİRİR; ihlal etmez. ✅
- **Dup-guard (FR-006, conventions "Bilinçli tekrar"):** Taşınabilir kural (ör. BC=servis = İLKE I)
  ADR'de KOPYALANMAZ; ADR link verir, kural evi constitution/conventions kalır. ✅

**Sonuç: Gate PASS — ihlal yok, Complexity Tracking boş.**

## Project Structure

### Documentation (this feature)

```text
specs/089-adr-repo-home/
├── plan.md              # This file
├── research.md          # Phase 0: taşıma kararları (konum, slug, link, status, dup-guard, ölü-slug, vault)
├── data-model.md        # Phase 1: ADR + ADR İndeksi varlıkları (doküman şeması)
├── quickstart.md        # Phase 1: grep tabanlı kabul doğrulaması (SC-001..004)
├── contracts/
│   ├── adr-file.md      # Repo ADR dosya şablonu (frontmatter + bölümler)
│   └── adr-index.md     # İndeks satır formatı (slug · başlık · durum)
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

Kod yok. Etkilenen repo yolları (dokümanlar):

```text
docs/
├── adr/                 # YENİ — taşınan 10 ADR + indeks
│   ├── README.md        # ADR İndeksi (slug · başlık · durum)
│   ├── adr-aop-caching-mechanism.md
│   ├── adr-bounded-context-per-service.md
│   ├── adr-cache-vs-readmodel.md
│   ├── adr-checkout-saga-orchestration.md
│   ├── adr-credential-link-forward-protection.md
│   ├── adr-external-agent-oauth-061.md
│   ├── adr-hybrid-search-slice.md
│   ├── adr-ingestion-llm-writers.md
│   ├── adr-mcp-control-plane-no-secret-return.md
│   └── adr-moderation-agent-extraction.md
└── conventions.md       # GÜNCELLE — ADR katman işaretçisi (FR-007) + satır 171 yol atıfı (FR-005)

CLAUDE.md                # GÜNCELLE — gerçek-kaynak satırı + satır 139 yol atıfı (FR-005/FR-007)
specs/049-checkout-orchestrator/checklists/requirements.md  # GÜNCELLE — ölü slug düzelt (SC-001)
```

Repo-DIŞI (dış vault, local-only, push önerilmez):

```text
~/dev/EcommerceNotes/ECommerceAgent/reference/
├── adr-*.md (10 karar kaydı)         # SİL (FR-008) — repo artık ev
├── adr-nedir.md                      # KALIR — "ADR nerede yaşar" repo yoluna güncelle
└── karar-dokuman-katmanlari.md       # KALIR — aynı güncelleme
```

**Structure Decision**: ADR'ler `docs/adr/` altına, `docs/conventions.md` ile aynı kökte (spec
varsayımı — doküman katmanı komşuluğu). İndeks `docs/adr/README.md` (GitHub klasör-landing + grep).
Slug'lar birebir korunur (FR-002). Bu bir dokümantasyon ağacıdır; `src/` değişmez.

## Complexity Tracking

> Constitution Check PASS — ihlal yok. Tablo boş.
