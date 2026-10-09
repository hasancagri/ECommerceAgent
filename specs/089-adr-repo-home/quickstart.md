# Quickstart: ADR Repo Taşıması Doğrulaması

Taşıma bitince bu grep tabanlı kontroller kabul kriterlerini kanıtlar. Hepsi repo kökünden,
dış vault'a gitmeden çalışır (US1/US2 çekirdeği). Kod/build yok.

## Önkoşul
- Branch `089-adr-repo-home`, `/speckit-implement` tamamlandı.
- `docs/adr/` içinde 10 ADR + `README.md` var.

## Doğrulama

### V1 — ADR katmanı repo içinde (SC-002, US1)
```bash
ls docs/adr/adr-*.md | wc -l          # beklenen: 10
test -f docs/adr/README.md && echo OK  # indeks var
```
10 kararın herhangi birinin gerekçesi repo içinden bulunur:
```bash
grep -rl "no-secret-return\|bounded context\|hibrit" docs/adr/   # dosya(lar) döner, vault'a gidilmez
```

### V2 — İndeks tam + çözülür (SC-003, US3)
```bash
# İndekste her satır link'i gerçek dosyaya gider mi?
grep -oE "\(adr-[a-z0-9-]+\.md\)" docs/adr/README.md | tr -d '()' | while read f; do
  test -f "docs/adr/$f" || echo "ÖLÜ SATIR: $f"
done                                   # çıktı boş = 0 ölü satır
grep -cE "^\| \[adr-" docs/adr/README.md   # beklenen: 10 satır
```

### V3 — 0 ölü işaretçi (SC-001)
```bash
# Repo'da adr- slug atıfları gerçek docs/adr dosyasına çözülür mü?
grep -rhoE "adr-[a-z0-9-]+" CLAUDE.md docs/ specs/ \
  | sort -u | grep -v "adr-nedir\|adr-repo-home" | while read s; do
  test -f "docs/adr/$s.md" || echo "ÇÖZÜLMEYEN: $s"
done
# Ölü slug düzeldi mi? (D6) — eski slug repo'da KALMAMALI
! grep -rq "adr-checkout-orchestrator-standalone-049" specs/ && echo "049 OK"
```
Not: `adr-nedir` + `adr-repo-home` istisna (biri vault öğrenme-notu, biri bu spec slug'ı).

### V4 — Wikilink kalmadı (FR-004)
```bash
! grep -rq "\[\[" docs/adr/ && echo "0 wikilink"   # repo-içi biçime döndü
```

### V5 — 0 çiftlenmiş kural (SC-004, FR-006)
```bash
# bounded-context ADR normatif kuralı kopyalamıyor, kural evine link veriyor mu?
grep -q "İLKE I\|constitution\|CLAUDE.md" docs/adr/adr-bounded-context-per-service.md && echo "kural evi linkli"
```
Manuel göz: her taşınabilir-kurallı ADR banner'ı kural evini işaret eder; ADR gövdesi kuralı
otoriteymiş gibi yeniden tanımlamaz (karar+neden anlatır).

### V6 — Katman görünürlüğü (FR-007)
```bash
grep -q "docs/adr" CLAUDE.md && echo "CLAUDE.md işaret ediyor"
grep -q "docs/adr" docs/conventions.md && echo "conventions işaret ediyor"
```

### V7 — Vault senkronu (FR-008, local-only)
```bash
V=~/dev/EcommerceNotes/ECommerceAgent/reference
ls $V/adr-nedir.md $V/karar-dokuman-katmanlari.md   # KALIR
ls $V/adr-bounded-context-per-service.md 2>/dev/null && echo "HATA: karar-ADR hala vault'ta" || echo "karar-ADR'leri silinmiş OK"
grep -q "docs/adr" $V/adr-nedir.md && echo "adr-nedir repo yoluna güncellendi"
```

## Beklenen sonuç
V1–V7 temiz → SC-001..004 karşılandı; US1 (repodan oku), US2 (atıflar çözülür), US3 (tek indeks) geçer.
