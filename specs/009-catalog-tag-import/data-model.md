# Data Model: Katalog Etiket Import (BookTag)

Yeni DB tablosu YOK. Etiketler `Product` belgesi içinde nested yaşar (Marten document).

## Value Object: BookTag

`Product/ValueObjects/ProductValueObjects.cs` içinde (konvansiyon: aggregate'in TÜM VO'ları tek dosyada).

| Alan | Tip | Kural |
|---|---|---|
| `Value` | string | Normalize edilmiş etiket metni: trim + whitespace-collapse + **lowercase** (`ToLowerInvariant`). Boş olamaz. Orijinal casing SAKLANMAZ. Türkçe karakterli etiketin ASCII ikizi (`AsciiVariant`) AYRI BookTag olarak saklanır ("karanlık"+"karanlik") |

**Şekil (Marten tuzağı — araştırma §3):**
```csharp
public record BookTag
{
    public string Value { get; private set; } = default!;
    private BookTag() { }                       // Marten deserialize (parametresiz)
    private BookTag(string value) => Value = value;
    public static ResultDomain<BookTag> Create(string raw);  // normalize + boş-guard
}
```

**Kurallar:**
- `Create(raw)`: `NameNormalization.Normalize(raw)` sonucu boş/whitespace ise → `Error` (VALUE_EMPTY); değilse `Ok(new BookTag(norm))`.
- Eşitlik `Value`'dan (record value-equality) — iki BookTag aynı normalize değere sahipse EŞİTtir. Gruplamanın temeli.
- Kimlik/yaşam döngüsü yok (entity değil).

## Aggregate genişlemesi: Product.Tags

| Üye | Tip | Not |
|---|---|---|
| `_tags` | `List<BookTag>` (private) | Kitaba-ait etiket listesi; ortak kayıt yok |
| `Tags` | `IReadOnlyList<BookTag>` | Dışarıya salt-okunur (İlke II) |
| `SetTags(IEnumerable<string> rawTags)` | `ResultDomain` | Tam-değiştirme; her ham parça `BookTag.Create`, başarısız atlanır, `Value`'ya göre dedupe, `_tags` temizlenip yeniden dolar. Boş liste geçerli |

**Geçiş**: ürün kurulurken (import, yalnız YENİ ISBN) `SetTags` bir kez çağrılır. Var olan ISBN worker'a gelmez (INSERT-ONLY) → etiketi ezilmez.

**Validasyon**:
- `SetTags` içinde `BookTag.Create` başarısız (boş) olan parça sessizce atlanır (import satırını durdurmaz — FR-008 ruhu).
- Dedupe `Value` eşitliğinden (SetAuthors/SetRelatedCategories deseni).

## Mapper genişlemesi: ImportRow.Tags

| Alan | Tip | Not |
|---|---|---|
| `Tags` | `List<string>` | `ExcelRowMapper` ham etiket parçaları (virgülle bölünmüş, trim, boş-ele, Distinct). NORMALİZE DEĞİL — normalize aggregate'te (`BookTag.Create`). Sütun yoksa boş liste |

## Rapor (dry-run — mevcut sayaçlara ek)

| Metrik | Tanım |
|---|---|
| etiketli satır | `Tags` boş olmayan satır sayısı (dry-run raporuna eklenir; hiçbir şey yazılmaz) |
