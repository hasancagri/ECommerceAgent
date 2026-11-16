# Personalization — Domain Süreci (EventStorming)

**BC ne yapar:** Login kullanıcının arama sinyalini (012: Catalog arama ucu `UserSearched`) tüketip ham arama
logunu izole saklar — zevk/kişiselleştirmenin veri temeli; kullanıcıya kendi geçmişini (tekilleştirilmiş)
sunar + silme (013). Öneri/ML YOK (sonraki dilimler).

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
3. 👤 **Geçmişimi göster** → 🟩 tekilleştirilmiş sayfalı liste (013) — login kullanıcı KENDİ         `(GetSearchHistory,`
   geçmişini görür; ham kayıtlar `userId`'ye süzülür, saf projection normalize-grupla-sırala.           ` SearchHistoryProjection)`
   Dış HTTP ucu (auth-zorunlu, named-scope yok). Anonim → 401.
4. 👤 **Geçmişi sil / temizle** → 🟦 hard delete (013) — kullanıcı tek sorgunun TÜM kayıtlarını         `(DeleteSearchHistoryItem,`
   ya da tüm geçmişini KALICI siler (yalnız kendi). Silinen geri gelmez (yeniden aranırsa yeni kayıt).   ` ClearSearchHistory)`

## Domain kuralları (süreci yöneten değişmezler)

- **Ekleme + kullanıcı-silme (012→013 revizyonu).** Yazım append-only (her arama ayrı satır, mutasyon YOK); 013 kullanıcıya KENDİ kayıtlarını hard silme/temizleme verir. İçerik mutasyonu hâlâ YOK (yalnız ekle/sil). 🟥 Hotspot (closed: "append-only silme-yok" → kullanıcı kendi kaydını hard siler, gizlilik/kontrol; ML sinyali korunmaz, kullanıcı niyeti öncelikli).
- **Login şart.** `UserId` boş olamaz (`Create` guard'ı) — anonim sinyal gelmez (Catalog login-only yayınlar). Okuma/silme uçları da auth-zorunlu, kullanıcı yalnız kendi `userId`'si (named-scope yok, İlke V notu).
- **Ham log, yorum yok.** Zevk profili/etiket ağırlıkları/ONNX bu dilimde YOK — ileride bu satırlardan türetilir.
- **Tekilleştirme sunumda, depoda değil.** `UserSearchRecord` ham kalır; benzersiz-sorgu görünümü okuma anında türetilir (`SearchHistoryProjection`, Türkçe-duyarlı normalize).
- **İzole.** Kayıtlar `personalizationDb`'de; başka BC doğrudan erişmez (İlke I).

## Sınır (bu BC'nin dokunmadığı)

Öneri/zevk-profili/ML/ONNX/Python (sonraki dilim). Arama-dışı sinyaller (order/library). Event YAYMAZ —
yalnız tüketir (`UserSearched`) + kendi geçmişini HTTP'den sunar/siler. "Tekrar ara" Catalog'da çalışır (burada değil).
