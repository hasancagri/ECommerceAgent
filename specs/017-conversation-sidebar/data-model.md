# Data Model — Sohbet Kayıt Sadakati + Kalıcı Sidebar

BC: ScoutAgent · Store: Marten `scoutDb` / şema `scout` / belge `mt_doc_conversation`.
Yeni tablo/şema YOK — alan eklemesi Marten'de otomatik.

## Conversation (aggregate root — mevcut, genişletilir)

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | mevcut |
| UserId | Guid | doğumda sabit; sahiplik |
| Title | string | `Rename`/`SetTitle` değiştirir |
| _messages | List<ChatMessage> | private; `Messages` IReadOnlyList |
| Summary / SummarizedUpTo | string? / int | mevcut (özetleme kapalı) |
| CreatedTime / UpdatedTime | DateTime | `UpdatedTime` sidebar sırası |

### Davranış metotları (değişen/eklenen)

- **`AddAssistantMessage(string text, string? itemsJson = null)`** — kartları mesaja yazar.
  `UpdatedTime` güncellenir. Boş metin → `ResultDomain.Error`.
- **`Rename(string title)`** — YENİ. Boş/boşluk → Error; trim; üst-sınır aşımı → Error.
  **`UpdatedTime`'a DOKUNMAZ** (sıra korunur). `ResultDomain`.
- **`SetTitle`** — mevcut, LLM-lenient (boş→dokunmaz), `UpdatedTime` günceller. Rename'den AYRI.
- Silme aggregate metodu DEĞİL — handler `session.Delete<Conversation>(id)` (sahiplik guard sonrası).

### Invariant'lar

- Başlık (Rename) boş olamaz; `MaxTitleLength` (≈100) aşılamaz.
- Sahiplik: değişiklik/silme/okuma yalnız `UserId == çağıran`.
- `_messages` yalnız davranış metotlarından büyür.

## ChatMessage (VO — mevcut, genişletilir)

| Alan | Tip | Not |
|---|---|---|
| Role | ChatRole | User/Assistant |
| Text | string | mevcut |
| CreatedAt | DateTimeOffset | mevcut |
| **ItemsJson** | string? | YENİ — assistant turunda yapılı kart JSON'u (`[{tool,result}]`); user turunda null |

- `record` + private parametresiz ctor + private setter (Marten record-VO tuzağı).
- `Create(ChatRole role, string text, DateTimeOffset createdAt, string? itemsJson = null)`.
- **ItemsJson = opak string** — stored VO'da `JsonElement` yok (deserialize 500 riskinden kaçınış);
  React `JSON.parse` eder.

## Durum geçişleri

Yok (durum makinesi değil). Mesaj listesi append-only; sohbet HARD silinebilir (son durum: yok).
