# Quickstart: Arama Kaydı + App-BFF Doğrulama

## Ön koşullar

- AppHost: Gateway + **Bff** + Catalog + **Personalization.Api** + `personalizationDb` + RabbitMQ ayakta.
- Catalog dolu (012 öncesi import — 22.5k kitap) ki arama sonuç dönsün.
- Gateway rotası `/api/discovery/**` → bff; React arama `/api/discovery/search`'e gider, login'de bearer.

## Birim test (test-first — İlke VI)

```bash
dotnet test tests/Personalization.Api.Tests/Personalization.Api.Tests.csproj
```
Kapsam: `UserSearchRecord.Create` (userId boş / query boş / resultCount<0 → Error; geçerli → Ok).

## Canlı doğrulama (acceptance ↔ test)

| Senaryo | Adım | Beklenen |
|---|---|---|
| **US1** login kayıt | login token'la `POST /api/discovery/search {"query":"orwell"}` | sonuç döner VE `personalizationDb.user_search_record`'a o userId ile 1 satır (query/mode/resultCount/searchedAt) |
| **US1** çok arama | aynı kullanıcı 3 arama | 3 ayrı satır (append, üzerine yazmaz) |
| **US2** anonim | token'sız `POST /api/discovery/search` | sonuç döner, **hiç** kayıt oluşmaz |
| **US3** şeffaf | BFF sonucu vs Catalog `/search` | aynı kümeler/sıra; etkileşimli hız |
| **US1** dayanıklılık | Personalization'ı durdurup login arama, sonra başlat | arama başarılı döner; Personalization gelince event tüketilir, kayıt tutar (retry) |

## DB spot-check

```sql
-- kayıt oluştu mu (login sonrası)
SELECT count(*), max(data->>'SearchedAt') FROM personalization.mt_doc_user_search_record
  WHERE data->>'UserId' = '<userId>';
-- anonim sonrası artmamalı
```

## Doğrulama notları

- BFF publish → RabbitMQ → Personalization bind/listen; binding tüketicide. Wolverine handler `*Consumer` değilse `IncludeType` kontrol.
- Anonim/geçersiz token → userId yok → event yok (login-only kuralı).
- Kayıt aramayı bloklamaz: publish hatası simüle (RabbitMQ kapalı) → arama yine 200 döner.

## Kapsam dışı

Öneri/serving, zevk-profili, ML/ONNX, ChatAgent, order/library sinyalleri — sonraki dilimler.
