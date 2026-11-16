# Contract: Zenginleştirilmiş Excel Kolon Şeması

Çıktı dosyası: `Kitapyurdu_Katalog_v3_enriched.xlsx` (kaynak `_v2`'nin kopyası). 5 yayınevi sayfası korunur; sayfa = yayınevi kuralı değişmez.

## Kolonlar

Kaynak 27 sütunun tamamı korunur. Fark:

| # | Sütun | Durum | Not |
|---|---|---|---|
| 25 | `Açıklama` | **İÇERİK DEĞİŞİR** | OpenAI metni. Mevcut import mapper'ı bu sütunu zaten okuyor → değişiklik gerektirmeden zengin metni alır |
| — | `Kaynak Açıklama` | **YENİ SÜTUN** | Orijinal Kitapyurdu açıklaması; denetim/yeniden-çalıştırma. Import bu dilimde yok sayar |
| — | `Etiketler` | **YENİ SÜTUN** | Serbest etiketler, **virgül+boşluk ile ayrık tek hücre** (ör. `distopya, gözetim, karanlık gelecek`). Import bu dilimde yok sayar |
| 0–24, 26 | diğer 26 sütun | **DEĞİŞMEZ** | ISBN/Kitap Adı/Yazar/**Kategori**/Fiyat/Puan... birebir kopya |

## Değişmezler

- Orijinal `_v2.xlsx` **bit-aynı** kalır (SC-005).
- Çıktının 27 orijinal sütunu eksiksiz; başlık adları birebir (mevcut `ExcelRowMapper` kırılmaz, FR-013).
- Yeni iki sütun **sona** eklenir; mevcut sütun sırası/indeksleri kaymaz.
- `Kategori` sütununa dokunulmaz (otoriter; OpenAI üretmez — FR-005).

## İleri dilim notu (kapsam dışı)

Import mapper'ın `Etiketler` sütununu Product'a bağlaması + `Kaynak Açıklama` politikası **sonraki dilim**. Bu sözleşme yalnız kolonların varlığını/biçimini garanti eder.
