# Data Model: Kullanıcı Arama Kaydı

## Belge: UserSearchRecord (Personalization — YENİ)

Marten belgesi (`personalizationDb`, şema "personalization"); append-only, mutasyon yok.

| Alan | Tip | Kural |
|---|---|---|
| `Id` | Guid (PK) | Her kayıt tekil (insert; upsert YOK) |
| `UserId` | Guid | Boş olamaz (login şart); **index** (sonra kullanıcı-bazlı okuma/ML) |
| `Query` | string | Boş/whitespace olamaz |
| `Mode` | string | `keyword`/`semantic`/`hybrid` (serbest string; BFF'ten gelir) |
| `ResultCount` | int | ≥ 0 (0 = sonuçsuz arama da kaydedilir) |
| `SearchedAt` | DateTimeOffset | Olay zamanı (BFF damgalar) |

- **`Create(userId, query, mode, resultCount, searchedAt)`** → `ResultDomain<UserSearchRecord>`: userId boş / query boş / resultCount<0 → Error; aksi Ok. (Test-first, İlke VI.)
- Mutasyon metodu YOK (append-only; güncellenmez/silinmez).
- Marten `DocumentAlias` "user_search_record" (tr-TR ı güvencesi — InvariantGlobalization zaten hallediyor, zararsız).

## Integration event: UserSearched (Shared — YENİ)

`Shared.IntegrationEvents.UserSearched` — BFF yayınlar, Personalization tüketir (fanout).

| Alan | Tip |
|---|---|
| `UserId` | Guid |
| `Query` | string |
| `Mode` | string |
| `ResultCount` | int |
| `SearchedAt` | DateTimeOffset |

- Additive alan default'lu eklenebilir (eski tüketici kırılmaz) — tıklama/ISBN sonra.
- `RabbitMqConstants.UserSearched` : `Exchange` (fanout) + `Queues.Personalization`.

## Akış (event→belge)

```
BFF (login arama) → UserSearched yayınla ⇨ Personalization
Personalization tüketir → UserSearchRecord.Create → session.Store → SaveChanges (append)
```

- Tüketici idempotent olması GEREKMEZ (her event ayrı kayıt; tekrar = ayrı satır, bilinçli — tekrar da sinyal). Hata → retry merdiveni → error-queue (DLQ, 088 deseni).

## Saf birim: UserSearchRecord.Create

Validasyon (userId boş, query boş/whitespace, resultCount<0 → Error; geçerli → Ok). Test-first, host'suz.

## Sınır

Okuma/sorgu yüzeyi YOK (serving sonraki dilim). Zevk-profili toplama YOK (ham satır). BFF durumsuz — kendi verisi yok.
