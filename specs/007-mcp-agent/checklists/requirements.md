# Specification Quality Checklist: MCP Query-Only Dönüşü + ScoutAgent

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-06
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details — MAF/Ollama yalnız Assumptions'ta (kullanıcı kararları), gövde teknoloji-nötr
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain (kararlar kullanıcıdan geldi: MCP-önce, basit niyetler, MAF, ScoutAgent adı)
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (LLM kapalı, yanlış tool, token süresi, çift niyet, uzun liste)
- [x] Scope is clearly bounded (query-only, 4 BC, yazma/kompleks/dış-agent YOK; ekran REST'i ayrı dilim)
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (anonim arama, 3 kişisel niyet, kartlar, sohbet geçmişi)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Kademe: Tam → sıradaki adım `/speckit-plan` (MAF/MCP kablosu, ScoutAgent yerleşimi, tool şemaları, hafıza katmanları, Order A2A kalıntısı orada).
- CLAUDE.md "MCP'yi parça parça geri getirme" maddesi: bu spec o maddenin istediği "bilinçli mimari karar + spec" — plan'da CLAUDE.md revizyonu task'ı olmalı.
- Plan girdileri: fasat YOK (doğrudan BC MCP uçları, FR-005a) · hafıza = kendi DB + pencere/özet · cevap = metin+yapılı öğe · v1 stream yok · model seçimi (tool-calling destekli Ollama modeli).
