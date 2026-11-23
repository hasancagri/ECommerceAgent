# Research: İçerik-Temelli Keşif Omurgası (021)

## R1 — Facet filtreleri: BM25 @@@ içinde mi, jsonb operatörüyle mi?

**Decision**: Facet exact-match filtreleri BM25 @@@ sorgusunun DIŞINDA, Postgres jsonb
operatörleriyle uygulanır: tekil alan `data->>'Genre' = ANY(@values)`, liste alan
`data->'Themes' ?| @values` (OR semantiği operatörde hazır). BM25 @@@ yalnız serbest metin +
skor için kullanılır; `paradedb.score` gereken sorgularda @@@ ve jsonb predicate aynı WHERE'de
birleşir (ParadeDB bunu destekler — index taraması + heap filter).

**Rationale**: pg_search `json_fields` konfigürasyonu alan-seviyesidir (`data` bütünü için
fast/expand_dots); **alt-yol (per-path) bazında tokenizer seçimi desteklenmez** (0.26.x). Default
tokenizer çok-kelimeli kapalı-sözlük değerlerini token'lara böler → `data.Themes:"tarih"`
araması "sanat tarihi" elemanına çarpar (false positive). Keyword/raw tokenizer tüm `data`ya
uygulanamaz (metin araması ölür). jsonb operatörü exact-match'i tokenizasyonsuz, index'siz bile
22K satırda ucuz verir; BM25 zaten daraltıyor.

**Alternatives considered**: (a) Facet'leri ayrı top-level kolonlara çıkarıp ayrı BM25 keyword
field tanımlamak — Marten belge modelini bozar (computed duplicate kolon + trigger), reddedildi.
(b) Tek-kelimelik sözlük değerleri garanti edip @@@ phrase kullanmak — taksonomi değerleri
çok-kelimeli ("sanat/yaratıcılık", "mizahi/absürt"), sözlüğü bozmak okunabilirliği öldürür.
(c) İleride pg_search per-path tokenizer gelirse facet'ler @@@'e taşınabilir — kapı açık,
SearchPredicateBuilder sözleşmesi iki yolu da taşıyabilir.

## R2 — SearchPredicateBuilder çıktı sözleşmesi

**Decision**: Builder çıktısı genişler: `(string Bm25Query, IReadOnlyList<SqlFragment> Where)` —
@@@ clause (metin + bool/range term'ler, 018 mevcut) + parametreli SQL fragment listesi (facet
jsonb predicate'leri + ExcludePublisher mevcut deseni). `ProductSearchQueries.SearchAsync` iki
parçayı tek WHERE'de birleştirir.

**Rationale**: ExcludePublisher zaten SQL-predicate istisnasıydı (018) — desen vardı, genelleşiyor.
Tek SQL korunur (018 ilkesi), sorgu başına round-trip artmaz.

## R3 — similar_books sorgu deseni

**Decision**: Tek sorgu, ham SQL (NpgsqlDataSource):
1. Hedef kitap okunur (Themes/Moods/WorkKey/Language/AudienceLevel/AuthorIds/Translators/Genre).
2. BM25 OR sorgusu: hedefin konu+ton değerleri `data.Themes:"..." OR data.Moods:"..."` —
   `paradedb.score(id)` IDF-ağırlıklı örtüşmeyi verir (nadir ortak değer güçlü sinyal, FR-018).
3. Boost CASE'leri skora eklenir: aynı yazar (+2.0) > aynı tür/Genre (+1.0) > aynı çevirmen (+0.5) —
   katsayılar `SimilarBooksOptions` (Options pattern).
4. Hard filtre: `Language` eşit; `AudienceLevel` uyum grubu içinde (`= ANY(@grup)`); `WorkKey <> @hedef`.
5. `DISTINCT ON (data->>'WorkKey') ... ORDER BY WorkKey, skor DESC` — eser başına en iyi baskı.
6. Dış sıralama: `StockCount>0 DESC, skor DESC, PurchaseCount DESC LIMIT @n`.
Kısıt: `paradedb.score(id)` @@@ içeren SELECT seviyesinde çağrılmalı (CTE sınırı) — skor iç
SELECT'te hesaplanır, dış katman yalnız sıralar.

**Rationale**: PDF bölüm 16 "V1 reçetesi" birebir; mevcut ProductSearchQueries ham-SQL deseniyle
tutarlı; ek altyapı (embedding/pgvector) gerektirmez (yapma-listesi).

**Alternatives considered**: Uygulama-içi skorlama (iki sorgu + bellek birleştirme) — 22K'da
çalışır ama SQL tek geçişte aynı işi yapar, reddedildi.

## R4 — WorkKey üretimi

**Decision**: `WorkKeyNormalizer.Derive(authorNames, title)`: yazar adları normalize edilip
SIRALANIR (ordinal) + `|` ile birleşir; başlıktan parantezli ek(ler) atılır (`\(.*?\)`), Türkçe
karakterler ASCII'ye düzleştirilir (mevcut `NameNormalization` deseni yeniden kullanılır/genişler),
lowercase invariant, ardışık boşluk tek. Örn: `dostoyevski|suc ve ceza`.

**Rationale**: Deterministik, kültür-bağımsız (InvariantGlobalization zaten açık), yazar-sırası
farkına dayanıklı. Pipeline'daki eser-havuzlama anahtarıyla aynı mantık (PDF bölüm 18).

**Alternatives considered**: Excel'e WorkKey kolonu ekletmek — dosya nihai (v11), import'ta
türetmek tek kaynaklı ve yeniden üretilebilir; reddedildi.

## R5 — Taksonomi sözlüğünün kaynağı

**Decision**: PDF bölüm 19 listeleri `BookTaxonomy.cs`'e FrozenSet olarak gömülür: tur (~25),
ton (~20), seviye (9: çocuk 0-3/4-6/7-10/10-12, genç yetişkin, yetişkin-giriş/orta/ileri,
belirsiz), konu (~45). Seviye uyum grupları: çocuk* kümesi ↔ kendisi; genç yetişkin köprü (iki
grupla da uyumlu); yetişkin* kümesi ↔ kendisi + belirsiz her yerde.

**Rationale**: Kapalı sözlük KOD-sahipli (KnownScopes emsali — serbest string üretimi imkansız);
"Program.cs'te inline veri yasak" konvansiyonu gereği static holder.

## R6 — ImportJob.Tagged → Enriched

**Decision**: `Tagged` sayacı/alanı `Enriched` olur: tur ≠ "belirsiz" VEYA ≥1 konu atanmış satır.
DB sıfır — eski belge yok, rename serbest.

## R7 — Stok sinyali tespiti

**Decision**: `ExcelRowMapper` "Stok Durumu" hücresini trim + `StartsWith("SATIŞ YOK",
OrdinalIgnoreCase)` ile ikiye ayırır → `ImportRow.InStock`. Tüm "SATIŞ YOK (…)" varyantları
(TÜKENMİŞ, Satışta Değil) kapsanır; diğer ~6 kargo/temin metni satışta sayılır. Boş hücre =
satışta (iyimser default — event default'uyla tutarlı).