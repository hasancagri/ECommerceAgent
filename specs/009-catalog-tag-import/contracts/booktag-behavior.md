# Contract: BookTag + Product.SetTags + Mapper Parse

## ExcelRowMapper — "Etiketler" parse

**Girdi**: satır hücreleri (`"Etiketler"` → ham metin, ör. `"distopya, gözetim, karanlık gelecek"`).
**Çıktı**: `ImportRow.Tags: List<string>` — ham parçalar.

Kurallar:
- Virgülle böl; her parçayı `Trim`; boş/whitespace ele; `Distinct` (ham string).
- Sütun yok / hücre boş → boş liste (geriye-uyumlu, FR-008).
- NORMALİZE ETME — sadece böl (biçim katmanı; kural aggregate'te).

Örnek:
| Hücre | ImportRow.Tags |
|---|---|
| `distopya, gözetim` | `["distopya", "gözetim"]` |
| `a,, b,` | `["a", "b"]` |
| (boş) | `[]` |

## BookTag.Create(raw) → ResultDomain<BookTag>

| raw | Sonuç | Value |
|---|---|---|
| `"Distopya"` | Ok | `"distopya"` (trim + lower) |
| `"  Gözetim  "` | Ok | `"gözetim"` |
| `"karanlık   gelecek"` | Ok | `"karanlık gelecek"` (whitespace-collapse) |
| `""` / `"   "` / `null` | Error (VALUE_EMPTY) | — |

- Normalize = trim + whitespace-collapse + `ToLowerInvariant` (lowercase; Türkçe karakter korunur).
- Eşitlik: `Create("Distopya").Value == Create("DISTOPYA").Value` → ikisi `"distopya"` → EŞİT.

## BookTag.AsciiVariant() → BookTag?

Türkçe karakter (ı ş ğ ü ö ç â î û) içeriyorsa ASCII-katlanmış ikiz; yoksa `null`.

| Value | AsciiVariant |
|---|---|
| `"karanlık"` | `"karanlik"` |
| `"gözetim"` | `"gozetim"` |
| `"distopya"` | `null` (Türkçe karakter yok) |

## Product.SetTags(IEnumerable<string> rawTags) → ResultDomain

- Her ham parça `BookTag.Create`; `IsSuccess` olmayan ATLANIR (import durmaz).
- Her başarılı tag + (varsa) `AsciiVariant` ikizi eklenir (arama köprüsü).
- `Value`'ya göre tekilleştirilir; `_tags` tam-değiştirme (`SetAuthors` deseni).
- Boş/tümü-geçersiz liste → `_tags` boş, `Ok` (etiketsiz ürün geçerli).

| rawTags | Product.Tags (Value'lar) |
|---|---|
| `["distopya","Distopya","DISTOPYA"]` | `["distopya"]` (dedupe) |
| `["karanlık"]` | `["karanlık","karanlik"]` (ASCII ikiz) |
| `["", "  "]` | `[]` |
| `[]` | `[]` |

## Tüketici / sonraki dilim

- Import worker `product.SetTags(importRow.Tags)` çağırır (yalnız YENİ ürün — INSERT-ONLY).
- Gruplama ("distopya içeren kitaplar") `BookTag.Value` eşleşmesiyle SONRAKİ arama diliminde; bu dilim yalnız etiketi oturtur + eşleşmeye hazırlar.
