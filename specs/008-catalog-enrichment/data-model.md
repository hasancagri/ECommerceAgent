# Data Model: Katalog Verisi Zenginleştirme

Offline araç — DB yok. "Model" = cache kaydı + Excel kolon şeması + rapor.

## Varlık: Zenginleştirme Cache Kaydı

`cache_enrich.json`: `{ "<isbn>": { ... } }`

| Alan | Tip | Kural |
|---|---|---|
| `isbn` | string (anahtar) | Kitabın tekil ISBN'i; cache anahtarı |
| `aciklama` | string | OpenAI arama-dostu açıklama (Türkçe). `kaynak=yetersiz` ise boş/kısa olabilir |
| `etiketler` | string[] | Serbest Türkçe etiketler (tema/alt-tür/ruh hali). Tür/kategori İÇERMEZ |
| `kaynak` | enum | `model` \| `kaynak_aciklama` \| `yetersiz` — açıklamanın dayanağı (model kendi bildirir) |
| `status` | enum | `ok` \| `error` — yalnız `ok` kalıcılaşır; `error` cache'e yazılmaz (yeniden denenir) |

**Geçişler**: işlenmemiş → (OpenAI başarılı) → `ok` cache'te kalıcı → sonraki koşu atlar. İşlenmemiş → (hata/retry tükendi) → cache'e YAZILMAZ → sonraki koşu yeniden dener.

**Validasyon**:
- `kaynak ∈ {model, kaynak_aciklama}` ise `etiketler` boş olmamalı (≥5 hedef, SC-002).
- `kaynak=yetersiz` ise uydurma yapılmamış sayılır; açıklama boş/kaynak kadar; rapora "zenginleştirilemedi".
- `aciklama`, `kaynak=kaynak_aciklama` iken kaynakta olmayan olgu içermemeli (SC-003, elle denetim).

## Varlık: Zenginleştirilmiş Excel Satırı (çıktı)

Kaynak 27 sütun korunur. Değişen/eklenen:

| Sütun | Durum | İçerik |
|---|---|---|
| `Açıklama` | **değişir** | OpenAI metni (gösterim + arama tek metin) |
| `Kaynak Açıklama` | **yeni** | Orijinal Kitapyurdu açıklaması (iz/denetim) |
| `Etiketler` | **yeni** | Cache `etiketler` dizisi, virgülle ayrık tek hücre metni |
| diğer 26 sütun | değişmez | ISBN, Kitap Adı, Yazar, Kategori, Fiyat... (Kategori otoriter, dokunulmaz) |

Çıktı = kaynak Excel kopyası ⊕ cache. Cache'te `ok` olmayan satırlarda `Açıklama` orijinal kalır, yeni kolonlar boş/kaynak.

## Varlık: Kapsam Raporu

`--report` çıktısı (konsol):

| Metrik | Tanım |
|---|---|
| toplam | kaynak kitap sayısı |
| zenginleşen | `kaynak ∈ {model, kaynak_aciklama}` |
| model-temelli | `kaynak=model` |
| kaynak-temelli | `kaynak=kaynak_aciklama` |
| zenginleştirilemedi | `kaynak=yetersiz` |
| etiketli | `etiketler` ≥5 olan |
| bekleyen | cache'te `ok` olmayan (işlenmemiş) |
