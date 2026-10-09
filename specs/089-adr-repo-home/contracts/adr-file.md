# Contract: Repo ADR Dosya Şablonu

Her `docs/adr/<slug>.md` bu iskelete uyar. Mevcut vault içeriği korunur (FR-001); yalnız frontmatter
sadeleşir ve linkler repo-içi biçime döner (D3/D4).

```markdown
---
status: Kabul            # Kabul | Öneri | Superseded | Tarihsel
---

# ADR: <İnsan-okur başlık>

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `CLAUDE.md` + `.specify/memory/constitution.md`.
> Kural evi: <ilgili İLKE / conventions bölümü linki, varsa>.
> İlgili: [adr-x](adr-x.md), [adr-y](adr-y.md).   ← yalnız repo-içi ADR'ler link; vault notları düz metin

## Bağlam
<kararın problemi/niyeti — vault'tan korunur>

## Karar
<tek cümle karar + detay — vault'tan korunur>

## Sonuçlar
<etkiler + durum gerekçesi — vault'tan korunur; invariant/nöbetçi-kural alt-bölümleri varsa korunur>
```

## Kurallar (zorunlu)

1. **Frontmatter yalnız `status:`** — Obsidian `aliases`/`tags` kaldırılır (D3).
2. **Status vokabüleri kapalı:** `Kabul` | `Öneri` | `Superseded` | `Tarihsel` (D5). Frontmatter değeri
   indeks satırıyla aynı olur.
3. **Wikilink yok:** `[[adr-x]]` → `[adr-x](adr-x.md)`; `[[vault-notu]]` → düz metin (parantez sökülür).
4. **Kural kopyalama yok (FR-006):** taşınabilir normatif kural ADR'de tekrarlanmaz; banner'da kural
   evine link verilir. ADR = karar+neden, conventions/constitution = kural.
5. **Hassas değer yok:** MerchantId/Key, PII, finansal değer metinde render edilmez (kontrol-düzlemi).
6. **Başlık biçimi:** ilk satır içerik `# ADR: <başlık>` (indeks başlığı buradan okunur).
7. **Bölüm başlıkları esnek:** mevcut ADR `## Dert`/`## Durum`/`## Nöbetçi kurallar` gibi varyant
   bölümler taşıyabilir — Bağlam/Karar/Sonuçlar semantiği korunduğu sürece aynen taşınır (yeniden
   yazım zorunlu değil, içerik korunur).
