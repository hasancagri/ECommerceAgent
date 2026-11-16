# Quickstart: Katalog Zenginleştirme Doğrulama

Araç `~/dev/book-crawl/enrich_books.py`. Kaynak/çıktı `~/Desktop/ECommerce Notları/`.

## Ön koşullar

```bash
pip install openai            # SDK kurulu değil
python3 -c "import openpyxl"  # kurulu (3.1.5)
export OPENAI_API_KEY="sk-..."   # ScoutAgent user-secret'taki anahtar; env'e ver (log'lanmaz)
```

## Komutlar

```bash
cd ~/dev/book-crawl

# 1) Örnek koşu (maliyet/çıktı ölç) — ilk 20 kitabı zenginleştir, cache'e yaz
python3 enrich_books.py --limit 20

# 2) Tam koşu — cache'te olmayan tüm kitapları işle (resumable)
python3 enrich_books.py

# 3) Çıktı Excel üret — cache'ten zenginleştirilmiş kopyayı yaz
python3 enrich_books.py --write

# 4) Kapsam raporu — kaç zenginleşti / etiketlendi / bekliyor
python3 enrich_books.py --report
```

## Doğrulama senaryoları (acceptance ↔ test)

| Senaryo | Adım | Beklenen |
|---|---|---|
| **US1** katalogu zenginleştir | `--limit 20` → `--write` | Çıktı xlsx'te 20 satırda `Açıklama` OpenAI metni, `Etiketler` dolu, `Kaynak Açıklama` orijinali tutuyor; orijinal 27 sütun yerinde |
| **US1** orijinal korunur | `--write` sonrası `_v2.xlsx` md5 | Kaynak değişmemiş; çıktı ayrı `_v3_enriched.xlsx` |
| **US2** resume | `--limit 50` koşusunu ortada durdur → tekrar `--limit 50` | İkinci koşu tamamlananları atlar; rapor "bekleyen"i yeniden işler, biteni değil |
| **US2** geçici hata | API 429 simüle/retry | Kayıt cache'e `ok` yazılmaz; sonraki koşu yeniden dener (kısmi çöp yok) |
| **US3** grounding | Ünlü kitap (Karamazov) + bilinmeyen-kaynaklı + boş-kaynak seç | Sırasıyla `kaynak=model`, `kaynak_aciklama`, `yetersiz`; boş-kaynak kitapta uydurma YOK |
| **SC-003** uydurma denetimi | `kaynak=kaynak_aciklama` 30 örnek | Hiçbiri kaynakta olmayan olgu içermiyor (elle) |

## Kapsam dışı (bu dilimde çalıştırılmaz)

Catalog import / Aspire / FTS / semantik — çıktı Excel yalnız sonraki dilim için hazırlanır.
