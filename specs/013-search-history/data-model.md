# Data Model: Arama Geçmişi Görüntüleme (013)

## UserSearchRecord (mevcut — 012, dokunulmaz)

Marten belgesi, `personalization.mt_doc_user_search_record`. AggregateRoot DEĞİL.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | belge kimliği |
| UserId | Guid | login kullanıcı (sub); index (012) |
| Query | string | ham sorgu metni (trim'li) |
| Mode | string | keyword/semantic/hybrid |
| ResultCount | int | ≥0 |
| SearchedAt | DateTimeOffset | arama anı |

**013 etkisi**: OKUMA (userId'ye filtreli) + KALICI SİLME (userId'ye filtreli). İçerik mutasyonu YOK.
**İnvariant revizyonu (012→013)**: "append-only, silme YOK" → kullanıcı kendi kayıtlarını hard silebilir.

## SearchHistoryEntry (türetilmiş görünüm — kalıcı değil)

Okuma anında `SearchHistoryProjection.Deduplicate` ile ham kayıtlardan hesaplanır. DB'de tablo YOK.

| Alan | Tip | Türetim |
|---|---|---|
| Query | string | grubun en son kaydının ham `Query`'si (gösterim için orijinal yazım) |
| NormalizedQuery | string | dedup anahtarı (Türkçe-duyarlı büyük/küçük fold) — API dışına sızmaz, silme hedefi |
| Mode | string | grubun en son kaydının modu (tekrar-ara bununla çalışır) |
| LastSearchedAt | DateTimeOffset | gruptaki en büyük `SearchedAt` (sıralama anahtarı) |
| Count | int | gruptaki ham kayıt sayısı (opsiyonel gösterim) |

**Gruplama kuralı**: `NormalizedQuery` eşitliği. **Sıralama**: `LastSearchedAt` azalan. **Sayfalama**: dedup+sırala SONRASI.

## Normalizasyon (saf, test-first)

`Normalize(query)` → Türkçe-duyarlı: büyük/küçük fold (İ→i, I→ı mantığı InvariantGlobalization ile tutarlı — bkz. CLAUDE.md), baş/son trim, iç boşluk sadeleştirme. Aynı sorgunun yazım varyantları tek anahtara iner. Yalnız dedup+silme-eşleşmesi için; kullanıcıya orijinal `Query` gösterilir.

## Silme hedefleme

- **Delete-one**: `UserId == currentUser.Id AND Normalize(Query) == Normalize(hedefSorgu)` eşleşen TÜM kayıtlar.
- **Clear-all**: `UserId == currentUser.Id` tüm kayıtlar.
- Marten filtreli silme in-memory normalize gerektirdiğinden (Türkçe-fold SQL'de yok): handler kullanıcının kayıtlarını çeker, normalize eşleşenlerin Id'lerini toplar, `session.Delete` + `SaveChanges`. (Clear-all doğrudan `DeleteWhere(UserId==x)`.)
