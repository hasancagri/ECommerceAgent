# Research: ADR'leri Repo İçine Taşı

Phase 0 — spec'in NEEDS CLARIFICATION'ları ve edge-case kararları çözülür. Kaynak envanteri canlı
tarama ile çıkarıldı (vault: `~/dev/EcommerceNotes/ECommerceAgent/reference/`).

## Envanter (gerçek durum)

**10 karar-ADR'si (taşınır):**

| Slug | Vault status | Kaynak-of-truth ipucu |
|---|---|---|
| adr-aop-caching-mechanism | living | 002-aop-query-caching |
| adr-bounded-context-per-service | living | İLKE I (kural evi constitution) |
| adr-cache-vs-readmodel | living | — |
| adr-checkout-saga-orchestration | living | 028/049; **specs/049 ölü-slug buna işaret eder** |
| adr-credential-link-forward-protection | proposed | 078/087 |
| adr-external-agent-oauth-061 | living | 061 |
| adr-hybrid-search-slice | superseded | 086 ES'e geçti |
| adr-ingestion-llm-writers | superseded | 050 first-party pivot söktü |
| adr-mcp-control-plane-no-secret-return | accepted | CLAUDE.md:139, conventions.md:171, 087 |
| adr-moderation-agent-extraction | living (ama pratik SÖKÜLDÜ) | 088 moderasyon kaldırıldı |

**Öğrenme notları (vault'ta KALIR):** `adr-nedir.md`, `karar-dokuman-katmanlari.md` (FR-008).

## Kararlar

### D1 — Konum + indeks dosyası
- **Decision**: ADR'ler `docs/adr/<slug>.md`; indeks `docs/adr/README.md`.
- **Rationale**: `docs/conventions.md` komşuluğu (spec varsayımı). README = GitHub klasör-landing +
  tek grep hedefi; ayrı `INDEX.md` GitHub'da otomatik render olmaz.
- **Alternatives**: repo kökü `adr/` (conventions'tan uzak, reddedildi); `docs/decisions/` (slug'lar
  zaten `adr-` önekli, çift önek gereksiz).

### D2 — Slug / rename
- **Decision**: Slug'lar birebir korunur, yeniden numaralama YOK (FR-002).
- **Rationale**: `specs/*` zaten bu slug'lara atıf veriyor; rename = ölü-atıf üretir.
- **Alternatives**: `NNNN-` numara öneki (MADR stili) — reddedildi, mevcut atıfları kırar.

### D3 — Frontmatter sadeleştirme
- **Decision**: Obsidian-özel frontmatter (`aliases`, `tags`) KALDIRILIR; yalnız `status:` tek satır
  frontmatter kalır. Gövde başındaki "> Obsidian" banner'ı repo-truth banner'ına yeniden yazılır
  ("Açıklama/gerekçe katmanı — bağlayıcı gerçek: kod + CLAUDE.md + constitution").
- **Rationale**: `aliases`/`tags` Obsidian arama/graf özelliği; repo'da değer yok, gürültü. `status`
  indeks için gerekli (FR-003).
- **Alternatives**: tüm frontmatter'ı at (status gövdeye) — reddedildi, indeks grep'i zorlaşır;
  frontmatter'ı aynen taşı — reddedildi, ölü Obsidian alanları.

### D4 — Link çevrimi (FR-004)
İki tür wikilink var:
- **ADR→ADR** (repo-içi olacak): `adr-bounded-context-per-service`, `adr-mcp-control-plane-no-secret-return`,
  `adr-external-agent-oauth-061`, `adr-cache-vs-readmodel`, `adr-credential-link-forward-protection`,
  `adr-hybrid-search-slice`, `adr-checkout-saga-orchestration`, `adr-aop-caching-mechanism`.
  → **Decision**: `[[adr-x]]` → `[adr-x](adr-x.md)` (aynı klasör, repo-relative).
- **ADR→vault-notu** (vault'ta kalan öğrenme/session/open-question/todo notları: `ddd-context-map`,
  `mimari-genel-bakis`, `integration-events`, `agent-auth-model`, `userkey-resolve-flow`,
  `ubiquitous-language`, `session-*`, `todo-*`, `*-open-question`, `event-sourcing-marten-es-projection`,
  `query-storefront-flow`, `prefers-direct-code-over-abstraction`).
  → **Decision**: `[[note]]` → **düz metin** (köşeli parantez sökülür, ad prose olarak kalır). Repo'da
  0 ölü işaretçi (SC-001); vault dışı + bağlayıcı-olmayan nota link verilmez. Kaybolan tek şey tıklanırlık,
  referans-niyeti metinde kalır.
- **Rationale**: Repo kaynak-of-truth katmanı dış not-defterine işaret edemez (spec US2 çekirdeği).
- **Alternatives**: vault notlarını da taşı — reddedildi, kapsam patlar + spec FR-008 "öğrenme notları
  vault'ta kalır" der; mutlak dosya-URL'i göm — reddedildi, local-only path kırılgan.

### D5 — Status normalizasyonu (indeks durumu)
- **Decision**: İndeks + ADR frontmatter durum vokabülerini 4 değere indir:
  `Kabul` (vault living/accepted), `Öneri` (proposed), `Superseded` (superseded),
  `Tarihsel` (pratik söküldü ama kayıt saklanır).
- **Eşleme**: aop/bounded-context/cache-vs-readmodel/checkout-saga/external-agent-oauth → **Kabul**;
  mcp-control-plane → **Kabul** (accepted); credential-link-forward-protection → **Öneri**;
  hybrid-search-slice, ingestion-llm-writers → **Superseded**;
  **moderation-agent-extraction → Tarihsel** (088 moderasyon söküldü — edge-case: silinmez, damgalanır).
- **Rationale**: Tek bakışta taranabilirlik (SC-003). Edge-case "terk edilmiş pratik tarihsel damgayla
  kalır" karşılanır.
- **Alternatives**: vault `living` terimini aynen tut — reddedildi, moderation "living" yanıltıcı
  (pratik yok). İngilizce MADR terimleri zorla — reddedildi, repo Türkçe.

### D6 — Ölü slug (specs/049) — SC-001 istisnası
- **Durum**: `specs/049-checkout-orchestrator/checklists/requirements.md:35` →
  `adr-checkout-orchestrator-standalone-049` slug'una atıf veriyor; böyle bir dosya YOK. Gerçek dosya
  `adr-checkout-saga-orchestration.md`.
- **Decision**: 049 checklist atıfı gerçek slug'a **düzeltilir** (`adr-checkout-saga-orchestration`,
  `docs/adr/` yolu). Ölü-atıf olarak bırakılmaz.
- **Rationale**: SC-001 = 0 ölü işaretçi. İçerik aynı kararı anlatıyor (checkout orchestration).
- **Alternatives**: ölü-atıf diye not düş — reddedildi, SC-001 ihlali; yeni dosya oluştur — reddedildi,
  mevcut ADR aynı kararı kapsıyor (çiftleme).

### D7 — Repo atıf güncellemeleri (FR-005)
- **Decision**: `CLAUDE.md:139` ve `docs/conventions.md:171` içindeki
  `adr-mcp-control-plane-no-secret-return` çıplak slug atıfları repo-içi yola bağlanır
  (`docs/adr/adr-mcp-control-plane-no-secret-return.md`). `specs/*` içindeki slug prose atıfları
  (002/006/087) FR-002 ile korunan slug sayesinde `docs/adr/`'ye karşılık gelir — prose aynen kalır,
  yeniden yazılmaz (yalnız 049 ölü-slug düzeltilir, D6).
- **Rationale**: FR-005 açıkça CLAUDE.md + conventions.md der; specs tarihsel artefakt, slug korunduğu
  için çözülür (US2 Independent Test = "karşılık gelir", yeniden-link şartı yok).

### D8 — ADR katmanı görünürlüğü (FR-007)
- **Decision**: `CLAUDE.md` üstüne ve `docs/conventions.md` üstüne bir satır: mimari karar *gerekçesi*
  artık `docs/adr/` (repo ADR katmanı); kod yazarken karar sınırına dayanınca oradan grep'le. CLAUDE.md
  gerçek-kaynak satırı güncellenir: "kod + CLAUDE.md > memory > vault" → ADR'nin repo katmanı eklenir.
- **Rationale**: Ajan/dev dev-anında tanısın (US1). Katman işaret edilmezse keşfedilmez.

### D9 — Dup-guard (FR-006 / SC-004)
- **Decision**: Taşınan her ADR, kuralı constitution/conventions'ta yaşayan bir karar için **kuralı
  kopyalamaz, link verir**. Özellikle `adr-bounded-context-per-service`: normatif kural listesi İLKE I'de;
  ADR banner'ı "kural evi: constitution İLKE I + conventions" der, ADR gövdesi *neden/alternatif/bağlam*
  anlatır. Taşıma sırasında her ADR'nin kural evi link satırı doğrulanır; ADR'yi normatif kaynak gibi
  sunan cümle trim edilir.
- **Rationale**: SC-004 = 0 çiftlenmiş kural; her taşınabilir kuralın tek evi. ADR = karar+neden,
  conventions/constitution = kural (conventions "Bilinçli tekrar" ayrımı).
- **Alternatives**: kuralı ADR'ye de yaz — reddedildi, çiftleme + drift riski.

### D10 — Vault senkronu (FR-008)
- **Decision**: Taşıma repo'da doğrulandıktan SONRA vault'taki 10 karar-ADR'si silinir.
  `adr-nedir.md` + `karar-dokuman-katmanlari.md` KALIR; her ikisinin "ADR nerede yaşar" bölümü
  `docs/adr/` repo yoluna güncellenir ("artık repo içinde, vault'ta değil").
- **Rationale**: Tek ev (çift-kaynak driftini önler); öğrenme notları serbest-biçim, vault'ta meşru.
- **Not**: Vault ayrı local-only repo (`~/dev/EcommerceNotes`), manuel-sync — push önerilmez
  (memory: obsidian-vault-auto-sync). Silme/güncelleme yalnız local.

### D11 — Git stratejisi
- **Decision**: Dosyalar vault'tan repo'ya **kopyalanır + dönüştürülür** (vault ayrı repo, `git mv`
  çalışmaz). Repo tarafı yeni dosya ekleme; vault tarafı ayrı silme. Branch zaten `089-adr-repo-home`.
- **Rationale**: İki ayrı git repo; cross-repo mv yok. İçerik zaten dönüşüyor (frontmatter/link), saf
  taşıma değil.

## Çözülmemiş
Yok — tüm NEEDS CLARIFICATION ve edge-case'ler yukarıda karara bağlandı.
