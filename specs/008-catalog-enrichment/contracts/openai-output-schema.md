# Contract: OpenAI Dönüş Şeması + Davranış Kuralları

## Girdi (kitap başına, prompt'a verilir)

- `kitap_adi` (string)
- `yazar` (string)
- `kategori` (string) — Kitapyurdu kategorisi, bağlam için; ETİKETLENMEZ
- `kaynak_aciklama` (string) — orijinal Kitapyurdu açıklaması (boş olabilir)

## Çıktı şeması (structured output, zorunlu JSON)

```json
{
  "type": "object",
  "properties": {
    "aciklama":  { "type": "string" },
    "etiketler": { "type": "array", "items": { "type": "string" } },
    "kaynak":    { "type": "string", "enum": ["model", "kaynak_aciklama", "yetersiz"] }
  },
  "required": ["aciklama", "etiketler", "kaynak"],
  "additionalProperties": false
}
```

## Davranış kuralları (prompt'ta kilitlenir)

1. **Biliyorsan model-temelli**: Kitabı gerçekten biliyorsan doğru, Türkçe, arama-dostu zengin açıklama yaz → `kaynak="model"`.
2. **Bilmiyorsan kaynağa sadık**: Bilmiyorsan yalnız `kaynak_aciklama`daki bilgiyi yeniden ifade et, yeni olgu (olay/karakter/tema) **EKLEME** → `kaynak="kaynak_aciklama"`.
3. **İkisi de yoksa uydurma**: `kaynak_aciklama` boş + kitabı bilmiyorsan açıklama ÜRETME, kısa/boş bırak → `kaynak="yetersiz"`.
4. **Etiketler serbest Türkçe**: tema/alt-tür/ruh hali; **tür/kategori etiketleme** (kategori otoriter, ayrı). `yetersiz` değilse ≥5 etiket hedefle.
5. **Dil**: tüm çıktı Türkçe.
6. **Uydurma yasağı mutlak**: emin olmadığın hiçbir olguyu yazma (spec FR-002, İlke VIII ruhu).

## Tüketici

- `enrich_books.py` → `kaynak` alanıyla cache `status`/rapor doldurulur; `aciklama`/`etiketler` Excel'e yazılır.
- `kaynak` alanı SC-003 denetiminin (uydurma yok) ölçüm kancasıdır.
