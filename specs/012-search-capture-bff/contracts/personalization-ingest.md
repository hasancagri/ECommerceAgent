# Contract: Personalization Ingest (tüketici + saklama)

## BC

- Yeni `Personalization.Api` (`src/services/personalization`), kendi `personalizationDb` + şeması, izole.
- YALNIZ tüketici — dış HTTP yüzeyi YOK (serving sonraki dilim). Wolverine in-proc + RabbitMQ listen.

## Tüketim

- `UserSearched` kuyruğunu dinler → `UserSearchRecord.Create(userId, query, mode, resultCount, searchedAt)`:
  - geçerli → `session.Store(record)` + `SaveChanges` (append, insert).
  - geçersiz (Create Error) → iş düşer/loglanır (bozuk event işi durdurmaz).
- İdempotent değil (her event ayrı kayıt — tekrar da sinyal). Hata → retry→error-queue (DLQ).

## Saklama

- `UserSearchRecord` belgesi (data-model); `UserId` index. Upsert YOK, sil/güncelle YOK.

## Değişmezler

- Veri izole (personalizationDb) — başka BC doğrudan erişmez (FR-007).
- Okuma/sunum yüzeyi YOK (FR-009).
