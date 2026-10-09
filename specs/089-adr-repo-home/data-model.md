# Data Model: ADR Repo Katmanı

Bu feature kod/DB üretmez; "veri modeli" = taşınan **doküman varlıklarının** şeması.

## Varlık: ADR (Mimari Karar Kaydı)

Tek karar = tek dosya `docs/adr/<slug>.md`.

| Alan | Konum | Kural |
|---|---|---|
| `slug` | dosya adı (`<slug>.md`) | Kimlik; vault slug'ı birebir korunur (FR-002); `adr-` önekli kebab-case |
| `status` | frontmatter `status:` | Vokabüler kapalı: `Kabul` \| `Öneri` \| `Superseded` \| `Tarihsel` (D5) |
| `title` | ilk `# ADR: ...` başlığı | İnsan-okur başlık; indekste gösterilir |
| banner | başlıktan sonra `>` blok | Repo-truth ipucu + kural evi link(ler)i (FR-006/D9); hassas değer İÇERMEZ |
| Bağlam | `## Bağlam` / `## Dert` | Kararın niyeti/problemi (korunur, FR-001) |
| Karar | `## Karar` | Tek cümle + detay (korunur) |
| Sonuçlar | `## Sonuçlar` / `## Durum` / invariant bölümleri | Etkiler + durum gerekçesi (korunur) |

**Doğrulama kuralları:**
- Frontmatter'da Obsidian `aliases`/`tags` BULUNMAZ (D3).
- Gövdede `[[...]]` wikilink BULUNMAZ: ADR→ADR link'i `[x](x.md)`, ADR→vault-notu düz metin (D4/FR-004).
- Normatif taşınabilir kural metni KOPYALANMAZ; kural evine link (FR-006/SC-004).
- Hassas değer (MerchantId/Key, PII, finansal) metinde render EDİLMEZ (ADR mcp-control-plane kendi kuralı).

**Durum geçişi:** ADR statik kayıt; "geçiş" = status alanı güncellemesi (ör. moderation `living`→`Tarihsel`,
088 pratiği söküldüğü için — D5). Yeni ADR eklenince indekse bir satır eklenir.

## Varlık: ADR İndeksi

Tek dosya `docs/adr/README.md` — tüm ADR'lerin katalog görünümü.

| Alan | Kaynak | Kural |
|---|---|---|
| satır/ADR | her `docs/adr/adr-*.md` | 10 karar-ADR'si = 10 satır (SC-003) |
| `slug`+link | dosya adı | `[adr-x](adr-x.md)` — tıklanır repo-içi |
| `title` | ADR `#` başlığı | — |
| `status` | ADR frontmatter | D5 vokabüleri |

**Doğrulama:** İndeksteki her satır gerçek bir `docs/adr/` dosyasına çözülür (0 ölü satır); her ADR
dosyasının indekste bir satırı var (0 eksik). Format: `contracts/adr-index.md`.

## İlişkiler

```
docs/adr/README.md  ──listeler──▶  docs/adr/adr-*.md (10)
docs/adr/adr-*.md   ──link──▶      docs/adr/adr-*.md        (ADR→ADR, repo-içi)
docs/adr/adr-*.md   ──link──▶      ../conventions.md / ../../.specify/memory/constitution.md  (kural evi)
docs/adr/adr-*.md   ──düz metin──▶ vault notları            (bağlayıcı değil, link yok)
CLAUDE.md / conventions.md  ──işaret──▶  docs/adr/          (katman görünürlüğü, FR-007)
specs/*             ──slug prose──▶  docs/adr/<slug>.md      (korunan slug ile karşılık; 049 düzeltilir)
```
