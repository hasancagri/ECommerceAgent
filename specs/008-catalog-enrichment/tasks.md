# Tasks: Katalog Verisi Zenginleştirme (OpenAI, offline)

**Feature**: `specs/008-catalog-enrichment` | **Input**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

Araç **ECommerceAgent solution dışında**: `~/dev/book-crawl/enrich_books.py` (+ `cache_enrich.json`). Girdi `~/Desktop/ECommerce Notları/Kitapyurdu_Katalog_v2.xlsx`, çıktı `..._v3_enriched.xlsx`. Testler saf-helper için opsiyonel (Anayasa İlke VI .NET-dışı araçta domain-TDD dayatmaz).

**Dosya yolu kısaltması**: `enrich_books.py` = `~/dev/book-crawl/enrich_books.py`.

---

## Phase 1: Setup

- [X] T001 Ortamı hazırla: `pip install openai`, `openpyxl` (3.1.5 kurulu) doğrula, `OPENAI_API_KEY` env'de mi kontrol et (yoksa net hata talimatı)
- [X] T002 `enrich_books.py` iskeletini oluştur: sabitler (kaynak/çıktı/cache yolları, model `gpt-4o-mini`, WORKERS, CHECKPOINT_N), CLI arg ayrıştırma (`--limit N`, `--write`, `--report`), `main()` dispatcher

---

## Phase 2: Foundational (tüm story'ler için bloklayıcı)

- [X] T003 [P] Excel okuma helper'ı `enrich_books.py`: 5 yayınevi sayfasını gez, satır başına `{isbn, kitap_adi, yazar, kategori, kaynak_aciklama}` çıkar; ISBN boş satırı atla; aynı ISBN tek kez
- [X] T004 [P] Cache katmanı `enrich_books.py`: `cache_enrich.json` load/save, atomik `tmp`+`os.replace`, ISBN-anahtarlı (desen `fetch_descriptions.py`)
- [X] T005 OpenAI çağrı sarmalayıcısı `enrich_books.py`: resmi SDK, structured output (contracts/openai-output-schema.md şeması), anahtar env'den (log'a/çıktıya YAZMA), 429/5xx üstel backoff + retry

---

## Phase 3: User Story 1 — Katalogu zenginleştir (P1) 🎯 MVP

**Hedef**: Kaynak Excel → her kitaba arama-dostu açıklama + dolu etiketler; orijinali bozmadan zenginleştirilmiş kopya.

**Bağımsız test**: `--limit 20` → `--write`; çıktı xlsx'te 20 satırda `Açıklama` OpenAI metni, `Etiketler` dolu, `Kaynak Açıklama` orijinali tutuyor, 27 orijinal sütun yerinde; kaynak `_v2` değişmemiş.

- [X] T006 [US1] Prompt + zenginleştirme fonksiyonu `enrich_books.py`: tek kitap girdisinden (contracts/openai-output-schema.md davranış kuralları: grounding+kaçış) `{aciklama, etiketler, kaynak}` üret
- [X] T007 [US1] Zenginleştirme döngüsü `enrich_books.py`: `ThreadPoolExecutor` (WORKERS) ile kitapları işle, her CHECKPOINT_N'de cache kaydet; yalnız başarılı (`status=ok`) cache'e yazılır
- [X] T008 [US1] `--write` üretimi `enrich_books.py`: kaynak Excel'i kopyala, cache'ten `Açıklama` üstüne yaz + `Kaynak Açıklama`/`Etiketler` sütunlarını SONA ekle (contracts/enriched-xlsx-columns.md), 5 sayfa/27 sütun koru; `Etiketler` virgül+boşluk serileştirme; `_v3_enriched.xlsx` yaz
- [X] T009 [US1] Doğrula: `--limit 20` → `--write`; çıktıyı openpyxl ile aç, acceptance senaryolarını (quickstart US1) gözle teyit; `_v2` md5 değişmediğini kontrol et

**Checkpoint**: US1 tek başına çalışır = MVP (küçük örnekte zengin açıklama + tags + korunmuş orijinal).

---

## Phase 4: User Story 2 — Güvenli yeniden-çalıştırma (P2)

**Hedef**: Kesinti/çökme sonrası kaldığı yerden; biteni yeniden işlemez; artımlı yeni kitap.

**Bağımsız test**: `--limit 50` koşusunu ortada durdur → tekrar `--limit 50`; ikinci koşu tamamlananları atlar, yalnız bekleyeni işler.

- [X] T010 [US2] Artımlı atlama `enrich_books.py`: döngü başında cache'te `status=ok` olan ISBN'leri işlenecek listeden çıkar
- [X] T011 [US2] Hata izolasyonu `enrich_books.py`: retry tükenirse kayıt cache'e YAZILMAZ (kısmi çöp yok), sayaç/log'a düşer; sonraki koşu yeniden dener
- [X] T012 [US2] Doğrula: yarım koşuyu durdur + tekrar başlat; atlanan/işlenen sayısını teyit (quickstart US2)

**Checkpoint**: US1 + US2 = dayanıklı tam-katalog koşusu.

---

## Phase 5: User Story 3 — Uydurma yok (grounding + kaçış) (P2)

**Hedef**: Hiçbir kitapta uydurma; bilinmeyen+kaynaklı kitap yalnız kaynağa sadık; boş-kaynak+bilinmeyen `yetersiz`.

**Bağımsız test**: ünlü (Karamazov) + bilinmeyen-kaynaklı + boş-kaynak kitap seç; `kaynak` alanı sırasıyla `model`/`kaynak_aciklama`/`yetersiz`; boş-kaynakta uydurma yok.

- [X] T013 [US3] `yetersiz` ele alma `enrich_books.py`: `kaynak=yetersiz` dönüşte `Açıklama` orijinal/boş bırak, "zenginleştirilemedi" işaretle; etiket zorlanmaz
- [X] T014 [US3] Doğrula (SC-003): `kaynak=kaynak_aciklama` 30 örnek + 3 sınıf örneği elle incele; kaynakta olmayan olgu eklenmediğini teyit (quickstart US3)

**Checkpoint**: Zenginleştirme güvenli — uzun-kuyruk + gelecekteki yeni kitaplar kirlenmez.

---

## Phase 6: Polish & Cross-Cutting

- [X] T015 [P] `--report` kapsam raporu `enrich_books.py`: toplam/zenginleşen/model-temelli/kaynak-temelli/zenginleştirilemedi/etiketli/bekleyen (data-model.md rapor tablosu)
- [X] T016 [P] Saf-helper birim testi (opsiyonel) `~/dev/book-crawl/test_enrich.py`: etiket serileştirme + cache-merge/atlama saf fonksiyonları
- [X] T017 Tam koşu + maliyet teyidi: önce `--limit` ile gerçek token/maliyet ölç, sonra tam `enrich_books.py` → `--write`; SC-001/002/006 raporla
- [X] T018 [P] Kısa kullanım notu `~/dev/book-crawl/enrich_books.py` başına docstring (komutlar + env + yollar; `fetch_descriptions.py` deseni)

---

## Dependencies & Execution Order

- **Setup (T001-T002)** → her şeyden önce
- **Foundational (T003-T005)** → tüm story'leri bloklar; T003/T004 paralel [P], T005 bağımsız
- **US1 (T006-T009)** → MVP; Foundational'a dayanır
- **US2 (T010-T012)** → US1 döngüsü/cache'i üstüne kurar
- **US3 (T013-T014)** → US1 zenginleştirme fonksiyonu/`kaynak` alanı üstüne kurar
- **Polish (T015-T018)** → US1-US3 sonrası; T015/T016/T018 paralel [P]

Tek dosyalık araç olduğundan story'ler US1 çekirdeğine lineer dayanır (yapay bağımsızlık zorlanmadı).

## Parallel Opportunities

- T003 ∥ T004 (ayrı fonksiyonlar, aynı dosya ama bağımsız bloklar)
- T015 ∥ T016 ∥ T018 (rapor / test / docstring — bağımsız)

## MVP Scope

**US1 (T001-T009)** = teslim edilebilir MVP: küçük örnekte zengin açıklama + tags + korunmuş orijinal. US2/US3 dayanıklılık + güvenlik katar; tam-katalog koşusu için ikisi de önerilir.
