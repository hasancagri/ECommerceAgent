# Personalization — Domain Süreci (EventStorming)

**BC ne yapar:** Login kullanıcının arama sinyalini (012: Catalog arama ucu `UserSearched`) tüketip ham arama
logunu izole saklar — zevk/kişiselleştirmenin veri temeli. Sunum/öneri/ML YOK (sonraki dilimler).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟩 read-model ·
> ⇦/⇨ BC-arası dikiş (kaynak/hedef BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir.

## Süreç

1. 🟧 **Kullanıcı aradı** ⇦ Catalog (012) — login aramada `UserSearched(userId, query, mode,     `(CatalogSearchConsumers,`
   resultCount, searchedAt)` tüketilir (fanout; binding tüketicide — 007 dersi). Anonim            ` UserSearchRecord.Create)`
   arama Catalog'da yayınlanmaz → buraya hiç gelmez.
2. 🟦 **Arama kaydını oluştur** → 🟨 **UserSearchRecord append** — ham log satırı (append-only;      `(UserSearchRecord)`
   tekilleştirme YOK, tekrar da sinyal). Create reddederse (bozuk event) iş düşmez, loglanır.
   Hata → retry merdiveni → error-queue (DLQ, 088 deseni).

## Domain kuralları (süreci yöneten değişmezler)

- **Append-only (012).** `UserSearchRecord` değiştirilmez/silinmez; her arama ayrı satır. Mutasyon metodu YOK.
- **Login şart.** `UserId` boş olamaz (`Create` guard'ı) — anonim sinyal gelmez (BFF login-only yayınlar).
- **Ham log, yorum yok.** Zevk profili/etiket ağırlıkları/ONNX bu dilimde YOK — ileride bu satırlardan türetilir.
- **İzole.** Kayıtlar `personalizationDb`'de; başka BC doğrudan erişmez (İlke I).

## Sınır (bu BC'nin dokunmadığı)

Okuma/sunum/öneri yüzeyi (serving sonraki dilim). Zevk-profili toplama, ML/ONNX/Python. Arama-dışı
sinyaller (order/library). Yayın YOK — yalnız tüketir.
