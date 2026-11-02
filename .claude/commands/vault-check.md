---
description: Obsidian vault'taki notları kod + CLAUDE.md + memory ile karşılaştırıp bayat olanları raporlar
argument-hint: (opsiyonel) tek bir not adı ya da konu — yoksa tüm vault taranır
---

# Vault Staleness Check

Obsidian vault, elle senkron edilen bir DDD gerekçe/açıklama katmanı. Kod veya karar
değişince notlar kendiliğinden güncellenmez → risk **bayatlama**. Bu komut bayat notları
**tespit edip raporlar**; düzeltme yalnızca kullanıcı onayıyla uygulanır.

## Vault yapısı

Notlar tür klasörlerine ayrılmıştır: `reference/` (yaşayan gerekçe: ADR, context-map,
ubiquitous-language, integration-events, agent-auth-model), `todos/`, `questions/`,
`sessions/`. Her notun frontmatter'ında bir `status` alanı vardır:
`living` (reference) | `open` | `done` | `dropped` (todos/questions) | `superseded` (sessions).

## Gerçek-kaynak hiyerarşisi

Çelişkide sıra: **kod + `CLAUDE.md`** > Claude memory > vault. Vault bağlayıcı değildir;
bir not kod/CLAUDE.md ile çelişiyorsa **not yanlıştır**, kod değil.

## Adımlar

1. **Envanter.** Vault kökünden (`~/dev/DevNotes/`) tüm `.md` notlarını
   **özyinelemeli** bul ve oku. DDD gerekçe katmanı `ECommerceAgent/` altında
   (reference/todos/questions/sessions); teknoloji öğrenme klasörleri (Wolverine/MCP/ES/Marten/Aspire…)
   kök seviyesinde — ikisi de taranır. Notlar alt klasöre taşınmış olabilir — sabit yola güvenme,
   kökten `find` ile tara. `.obsidian/` yapılandırma klasörünü atla.
   - Vault yoksa/erişilemezse net hata ver ("vault bulunamadı: <yol>"), sessizce boş rapor dönme.
   - Argüman verildiyse yalnızca eşleşen notu/konuyu değerlendir.

2. **Gerçek-kaynağı topla.** `git log --oneline -30` + bu oturumda değişen dosyalar;
   `CLAUDE.md`; ve Claude memory (`MEMORY.md` + ilgili memory notları — ikincil tazelik sinyali).

3. **Karşılaştır (muhakeme).** Her not için sor: "Bu notun iddia ettiği yapı/karar/durum
   kod + CLAUDE.md + memory ile hâlâ tutuyor mu?" Bir notu şu durumlarda işaretle:
   - **Çelişki:** anlattığı yapı/karar artık kodda öyle değil.
   - **Çözülmüş açık-iş:** "TODO/açık" diyor ama iş bitmiş/merge olmuş.
   - **Kırık referans:** `[[wikilink]]` var olmayan nota işaret ediyor; bahsettiği dosya/tip
     silinmiş ya da yeniden adlandırılmış.
   - **Memory çelişkisi:** bir memory notu aynı konuda güncel/farklı şey söylüyorsa,
     vault notu bayat adayıdır.
   - **status alanı ↔ gövde çelişkisi:** frontmatter `status` gövdedeki `**Durum:**` ile
     ya da gerçek kod durumuyla tutmuyorsa bayat. (Ör. `status: open` ama iş bitmiş/merge olmuş →
     `done`; iş terk edilmiş → `dropped`.) Düzeltmede hem `status` alanını hem gövdeyi hizala.
   - Emin olmadığın durumu **🔴 bayat** deme; **🟡 gözden geçir** (düşük güven) kategorisine koy.
     Yanlış pozitifle gürültü üretmektense emin olduklarını öne al.

4. **Raporla (sohbette).** Güven sırasına göre, her giriş şu formatta:

   ```
   🔴 Bayat — <dosya adı>
      Neden: <hangi değişiklik notu çürüttü>
      Öneri: <önerilen düzeltme>

   🟡 Gözden geçir — <dosya adı>
      Neden: <kısmen geçerli / şüpheli nokta>
   ```

   Temiz notları tek tek listeleme; tek satır özetle ("N not tarandı, hepsi güncel").
   Hiç bayat yoksa düzeltme sorma.

5. **Onay kapısı.** Bayat/gözden-geçir varsa sonda sor: "Düzeltmeleri uygulayayım mı?"
   - Kullanıcı onaylarsa düzeltmeleri vault dosyalarına Edit ile uygula (Obsidian
     konvansiyonlarını koru: frontmatter, `[[wikilinks]]`, tag'ler).
   - Onaylamazsa hiçbir yazma yapma.