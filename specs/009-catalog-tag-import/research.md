# Research: Katalog Etiket Import (BookTag)

## 1. BookTag şekli — tek alan normalized (orijinal saklanmaz)

- **Decision**: `BookTag` = record, TEK alan `Value` (normalize edilmiş metin). Orijinal casing SAKLANMAZ. `Create(raw)` → trim + `NameNormalization.Normalize` → `ResultDomain<BookTag>`. Eşitlik `Value`'dan (record value-equality) → kitaplar-arası gruplama bedava.
- **Rationale**: Kullanıcı kararı (2026-11-16) — "orijinal diye bir şey olmasın, normalized DB'ye aktarılır". Author/Category'nin iki-alanlı (Name + NormalizedName) deseninden bilinçli ayrılır: etiket makine-üretimi (008), gösterim için orijinal casing'e ihtiyaç yok.
- **Alternatives**: İki alan (Display + NormalizedValue, Author deseni) — orijinal casing korur ama kullanıcı reddetti. Tek alan lowercase + tag'e-özel fold — `NameNormalization` reuse etmez, iki normalize mantığı doğar (reddedildi, sapma).

## 2. Normalizasyon — lowercase + ASCII ikiz (NameNormalization reuse EDİLMEDİ)

- **Decision (REVİZE, kullanıcı kararı 2026-11-16)**: `BookTag.Create` = `Trim` + whitespace-collapse + **`ToLowerInvariant`** (lowercase). AYRICA Türkçe karakterli etiketin **ASCII ikizi** ayrı BookTag olarak saklanır (`AsciiVariant`): ı→i ş→s ğ→g ü→u ö→o ç→c â→a î→i û→u. "karanlık" → iki kayıt: `"karanlık"` + `"karanlik"`.
- **Rationale**: Test kanıtladı — `NameNormalization` (ToUpperInvariant) Türkçe noktasız ı'yı KATLAMIYOR ("karanlık" → "KARANLıK", ı U+0131 kalır). Yani uppercase reuse, "distopya/DİSTOPYA aynı" İ/ı sözünü vermiyor. Lowercase + ASCII ikiz: kullanıcı "karanlik" de yazsa "karanlık" da yazsa eşleşir (arama köprüsü). İkiz AYRI kayıt — gösterim dilimi (sonraki) ikizi gizler/dedupe eder.
- **Trade-off**: Türkçe karakterli etiket iki kayıt → kitapta görünürde iki benzer çip (gösterim UI'ı sonraki dilimde dedupe eder; şu an depolama+arama için doğru).
- **Alternatives**: NameNormalization uppercase reuse — İ/ı katlamaz, US2 sözünü tutmaz (test kanıtladı, reddedildi). Türkçe-özel tek-değer fold (ikiz yok) — "karanlik" yazımı "karanlık"ı bulamaz, reddedildi.

## 3. Marten deserialize tuzağı — parametresiz ctor + private set

- **Decision**: `BookTag` kayıtta get-only + parametreli-ctor OLMAZ. Şekil: `public string Value { get; private set; }` + `private BookTag() { }` (parametresiz) + `private BookTag(string value)`. Marten (Newtonsoft) `Product` belgesini okurken nested `BookTag`'i parametresiz ctor + setter ile deserialize eder.
- **Rationale**: Kanıtlı tuzak (memory `marten-record-vo-deserialize-ctor-gotcha`): get-only + parametreli-ctor VO, DB'den OKUMA anında 500 verir; transient kurulum sorunsuz olduğu için UNIT test kaçırır, yalnız Aspire/E2E yakalar. Author/Product zaten non-public setter deseninde (CLAUDE.md "non-public setter+ctor").
- **Doğrulama**: Aspire'da import koşusu sonrası ürünü OKU (etiketli) — 500 yoksa tuzak kapalı (unit test yetmez, bilinçli canlı adım).
- **Alternatives**: Marten custom serializer/JsonConverter — aşırı; parametresiz ctor yeter.

## 4. ExcelRowMapper etiket parse — mevcut SplitNames deseni

- **Decision**: Mapper `"Etiketler"` sütununu okur, virgülle böler (008 çıktısı "virgül+boşluk"). Mevcut `SplitNames` deseni: `Split(',')` + trim + boş-ele + `Distinct()`. `ImportRow`'a `List<string> Tags` alanı eklenir (ham parçalar — normalize AGGREGATE tarafında `BookTag.Create`'te, mapper saf metin bölme yapar).
- **Rationale**: Mapper zaten saf + test-first (`ExcelRowMapperTests`); sütun yoksa `Text` null döner → boş liste (geriye-uyumlu, FR-008). Normalize'i aggregate'e bırakmak katman ayrımını korur (mapper = biçim, VO = kural).
- **Alternatives**: Mapper'da normalize — domain kuralı mapper'a sızar, reddedildi.

## 5. ImportJobWorker entegrasyonu — SetTags çağrısı

- **Decision**: `ImportJobWorker` ürün kurulum bloğunda (satır ~249, `SetAuthors`/`SetCategory` yanında) `product.SetTags(importRow.Tags)` eklenir. `SetTags` içeride her ham parçayı `BookTag.Create` eder, başarısızları atlar, `Value`'ya göre tekilleştirir, listeyi tam-değiştirir. INSERT-ONLY korunur — var olan ISBN zaten `Create` aşamasında atlanıyor, worker'a hiç gelmez.
- **Rationale**: Mevcut INSERT-ONLY akışı (ISBN-varsa-atla) worker'ın üstünde; etiket yalnız YENİ ürün kurulurken yazılır → var olan ürün etiketi ezilmez (FR-007). `SetTags` `SetAuthors`/`SetRelatedCategories` ile aynı tam-değiştirme + dedup imzası.
- **Alternatives**: Yeni event/consumer — gereksiz; import senkron kurulum yolu yeterli.

## Çözülen NEEDS CLARIFICATION

- BookTag şekli (tek-alan vs iki-alan) + casing → **tek-alan normalized, uppercase NameNormalization** (kullanıcı kararı §1/§2).
- Marten deserialize → **parametresiz ctor + private set** (§3).
- Var-olan-ürün backfill → spec Assumptions'ta kapsam dışı (taze import; INSERT-ONLY).
