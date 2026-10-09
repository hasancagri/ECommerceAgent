# Tasks: ADR'leri Repo İçine Taşı (dev-anında gerekçe kaynağı)

**Branch**: `089-adr-repo-home` | **Feature dir**: `specs/089-adr-repo-home/`

**Girdi**: plan.md, spec.md, research.md (D1–D11), data-model.md, contracts/ (adr-file.md, adr-index.md), quickstart.md

**Tür**: Pure-docs taşıması — kod/build/test harness YOK. Doğrulama = quickstart.md grep (V1–V7).

**Kaynak vault** (repo-dışı, local-only): `~/dev/EcommerceNotes/ECommerceAgent/reference/`
**Hedef**: `docs/adr/`

**Transform tarifi** her ADR için (contracts/adr-file.md): frontmatter yalnız `status:` bırak
(Obsidian `aliases`/`tags` sil) · status D5 vokabülerine map (`Kabul`/`Öneri`/`Superseded`/`Tarihsel`) ·
banner'ı repo-truth + kural evi link'ine yaz · `[[adr-x]]`→`[adr-x](adr-x.md)` · `[[vault-notu]]`→düz
metin · hassas değer render etme · Bağlam/Karar/Sonuçlar içeriği korunur.

---

## Phase 1: Setup

- [x] T001 `docs/adr/` dizinini oluştur (repo kökünde, `docs/conventions.md` komşusu)

## Phase 2: Foundational (taşıma öncesi önkoşul)

- [x] T002 Kaynak envanterini doğrula: `~/dev/EcommerceNotes/ECommerceAgent/reference/`'de 10 karar-ADR'si + `adr-nedir.md` + `karar-dokuman-katmanlari.md` mevcut mu (research.md envanter tablosuyla eşle)

---

## Phase 3: User Story 1 — Gerekçeyi repodan oku (P1) 🎯 MVP

**Hedef**: 10 karar-ADR'si `docs/adr/` içinde, dönüştürülmüş + grep'lenebilir; gerekçe repo içinden bulunur.

**Independent Test**: `ls docs/adr/adr-*.md` = 10; herhangi bir kararın gerekçesi `grep -r docs/adr/` ile bulunur, vault'a gidilmeden (quickstart V1).

Her task = bir ADR'yi vault'tan `docs/adr/`'ye transform tarifiyle taşı. Farklı dosyalar → [P].

- [x] T003 [P] [US1] `adr-aop-caching-mechanism.md` taşı (status `Kabul`); `docs/adr/`
- [x] T004 [P] [US1] `adr-cache-vs-readmodel.md` taşı (status `Kabul`); `docs/adr/`
- [x] T005 [P] [US1] `adr-external-agent-oauth-061.md` taşı (status `Kabul`); `docs/adr/`
- [x] T006 [P] [US1] `adr-hybrid-search-slice.md` taşı (status `Superseded`); `docs/adr/`
- [x] T007 [P] [US1] `adr-ingestion-llm-writers.md` taşı (status `Superseded`); `docs/adr/`
- [x] T008 [P] [US1] `adr-credential-link-forward-protection.md` taşı (status `Öneri`); banner `[adr-mcp-control-plane-no-secret-return](adr-mcp-control-plane-no-secret-return.md)` repo-link'i taşır; `docs/adr/`
- [x] T009 [P] [US1] `adr-checkout-saga-orchestration.md` taşı (status `Kabul`); `docs/adr/`
- [x] T010 [P] [US1] `adr-moderation-agent-extraction.md` taşı — **edge-case: status `Tarihsel`** (088 moderasyon söküldü), banner'a "pratik 088'de kaldırıldı, kayıt tarihsel" notu; `docs/adr/`
- [x] T011 [P] [US1] `adr-mcp-control-plane-no-secret-return.md` taşı (status `Kabul`); **hassas değer render etme kuralını kendi gövdesi taşır — koru**; inter-ADR link'leri repo biçimine çevir; `docs/adr/`
- [x] T012 [P] [US1] `adr-bounded-context-per-service.md` taşı (status `Kabul`); **FR-006 dup-guard: normatif kuralı kopyalama** — banner'da kural evi `../../.specify/memory/constitution.md` (İLKE I) + `../conventions.md` link'i, gövde karar+neden+alternatif anlatır; `docs/adr/`
- [x] T013 [US1] Taşınan 10 ADR'yi doğrula: (a) her dosya adı vault slug'ıyla birebir aynı, rename/numara yok (FR-002); (b) `grep -rq "\[\[" docs/adr/` boş — 0 wikilink kaldı (FR-004 intra-ADR + ADR→vault çevrimi tamam, quickstart V4)

**Checkpoint**: US1 bağımsız teslim edilebilir — ADR katmanı repo içinde okunur (SC-002).

---

## Phase 4: User Story 2 — Mevcut atıflar gerçek dosyaya çözülür (P2)

**Hedef**: `CLAUDE.md`, `conventions.md`, `specs/*` ADR atıfları repo-içi `docs/adr/` dosyasına çözülür; 0 ölü işaretçi.

**Independent Test**: repo'daki her `adr-` slug atıfı `docs/adr/<slug>.md`'ye çözülür; eski ölü slug kalmaz (quickstart V3).

- [x] T014 [P] [US2] `CLAUDE.md` güncelle: satır ~139 `adr-mcp-control-plane-no-secret-return` atıfını `docs/adr/adr-mcp-control-plane-no-secret-return.md` yoluna bağla (FR-005)
- [x] T015 [P] [US2] `docs/conventions.md` güncelle: satır ~171 aynı ADR atıfını `docs/adr/...` yoluna bağla (FR-005)
- [x] T016 [US2] `specs/049-checkout-orchestrator/checklists/requirements.md` satır ~35: ölü slug `adr-checkout-orchestrator-standalone-049` → gerçek `docs/adr/adr-checkout-saga-orchestration.md` (D6, SC-001 istisnası)
- [x] T017 [US2] Repo-geneli ölü-işaretçi taraması: `grep -rhoE "adr-[a-z0-9-]+" CLAUDE.md docs/ specs/` çıktısındaki her slug `docs/adr/<slug>.md`'ye çözülüyor mu (istisna `adr-nedir`, `adr-repo-home`); çözülmeyen varsa düzelt (quickstart V3)

**Checkpoint**: US2 bağımsız — atıflar çözülür (SC-001). US1 gerektirir (dosyalar var olmalı).

---

## Phase 5: User Story 3 — Kararları tek indeksten tara (P3)

**Hedef**: Tüm kararlar + durumları tek `docs/adr/README.md` indeksinde.

**Independent Test**: `docs/adr/README.md` 10 ADR'yi slug+başlık+durum ile listeler; her satır gerçek dosyaya çözülür (quickstart V2).

- [x] T018 [US3] `docs/adr/README.md` oluştur — contracts/adr-index.md formatında tablo (slug-link · başlık · durum); başlıkları her ADR'nin gerçek `# ADR:` satırından al, durumları dosya frontmatter'ıyla eşle (FR-003, SC-003)
- [x] T019 [US3] İndeks bütünlüğü: her `docs/adr/adr-*.md` için bir satır (10 satır), her satır link'i gerçek dosyaya gidiyor (0 ölü/eksik — quickstart V2)

**Checkpoint**: US3 bağımsız — tek indeks taranır (SC-003). US1 gerektirir.

---

## Phase 6: Polish & Cross-Cutting

- [x] T020 [US2] FR-007 katman görünürlüğü: `CLAUDE.md` gerçek-kaynak satırını + `docs/conventions.md` üstünü, "mimari karar gerekçesi → `docs/adr/` ADR katmanı; dev-anında oradan grep'le" ile güncelle (quickstart V6)
- [x] T021 Dup-guard süpürmesi (SC-004/FR-006): taşınan 10 ADR'yi tara — taşınabilir normatif kuralı otoriteymiş gibi kopyalayan var mı; varsa trim + kural evine link (özellikle bounded-context, mcp-control, external-agent-oauth); quickstart V5
- [x] T022 **Vault senkronu (FR-008, repo-dışı local-only — push YOK)**: `~/dev/EcommerceNotes/ECommerceAgent/reference/`'den 10 karar-ADR'sini sil; `adr-nedir.md` + `karar-dokuman-katmanlari.md` KALIR (quickstart V7)
- [x] T023 [P] (FR-008) `adr-nedir.md` + `karar-dokuman-katmanlari.md`'nin "ADR nerede yaşar" bölümünü `docs/adr/` repo yoluna güncelle ("artık repo içinde, vault'ta değil")
- [x] T024 Tam doğrulama: quickstart.md V1–V7'yi çalıştır, hepsi temiz (SC-001..004 + US1/US2/US3 geçer)

---

## Dependencies

- **Setup (T001) → Foundational (T002) → US1 (T003–T013)** sıralı başlar.
- **US1 bloklar US2 + US3** (dosyalar var olmadan atıf çözülmez / indeks yazılamaz).
- **US2 (T014–T017, T020) ∥ US3 (T018–T019)** — US1 bitince paralel.
- **Polish (T021–T024)** en son; T022 (vault silme) yalnız repo doğrulandıktan sonra (T024 öncesi ama T013/T017/T019 sonrası).
- T022 → T023 sıralı (ikisi de vault; silme sonrası öğrenme-notu güncelle).

## Parallel örnekleri

- **US1 toplu taşıma**: T003–T012 hepsi [P] (10 ayrı dosya, aynı tarif) — paralel ajanla veya tek geçişte.
- **US1 sonrası**: T014+T015 [P] (CLAUDE.md ∥ conventions.md) + T018 (indeks) aynı anda başlar.

## Implementation strategy

- **MVP = US1** (T001–T013): ADR'ler repo içinde okunur — feature'ın tek değer çekirdeği (spec P1 gerekçesi). Burada durulsa bile dev gerekçeyi repodan grep'ler.
- **Artımlı**: US2 ölü-atıfları kapatır (kozmetik-ama-önemli), US3 taranabilirlik ekler.
- **Polish**: vault senkronu + dup-guard + tam quickstart. T022 vault silme **geri-dönüşü zor** — T024 tam doğrulama geçene dek erteleme; repo tek-ev olduğu kanıtlanınca sil.

## Notlar

- Vault ayrı git repo (`~/dev/EcommerceNotes`); cross-repo `git mv` yok → repo'ya kopyala+dönüştür, vault'ta ayrı sil (D11).
- Vault manuel-sync, LOCAL-ONLY — push önerme (memory: obsidian-vault-auto-sync).
- Kod/`src/` değişmez; constitution İLKE I–VII uygulanmaz (plan Constitution Check PASS).
