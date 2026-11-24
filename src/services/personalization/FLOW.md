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

## Taste-vektör kişiselleştirme (028 Dilim-4 — as-built, B: ScoutAgent orkestrasyon)

> Kuzey yıldızı: kullanıcıyı kitap embedding uzayına NOKTA (taste) koy → kNN = kişisel öneri. Cold-start:
> 0 satın-alma/yorum, ~41 arama (sorgu METNİ). MVP: taste = arama-sorgusu embed merkezi (on-demand); sinyal<3
> → küratörlü. gRPC+m2m İPTAL → ScoutAgent orkestrasyon (get_my_taste MCP → recommend_books MCP; mevcut JWT/forward).

T1. 🟦 **Zevk-vektörünü hesapla** (on-demand) — kullanıcının `UserSearchRecord` distinct sorguları →          `(ComputeTaste,`
   OpenAI embed → `TasteVector.Average` merkez + signalCount. Arama-only (satın-alma/yorum ağırlık kablosu      ` TasteVector, EmbeddingClientFactory)`
   `Average(weights)`'te hazır, 0 veri). Boş/boş-userId → boş vektör+0.
T2. 🟦 **get_my_taste** (PERSONAL MCP) ⇨ ScoutAgent — yalnız çağıranın taste'i (user JWT; 013 deseni).            `(GetMyTaste.Tool)`
   Vektör OPAK (İlke VIII): yalnız S2S makinaya, insan/LLM/log'a çıkmaz.
T3. 🟩 **Sana özel feed** ⇦ ScoutAgent → ⇨ Catalog — ScoutAgent get_my_taste → signalCount<3/boş ise boş         `(ScoutAgent Feed →`
   taste (küratörlü) değilse vektörle Catalog `recommend_books` (product_embedding kNN + satılabilir +           ` Catalog recommend_books)`
   WorkKey tekil + çeşitlilik) → kartlar. Spesifik sorgu → mevcut lexical/semantik (taste karışmaz; YouTube-modeli).
T4. 🟪 **Taste güçlenir (GELECEK)** ⇦ `OrderCompleted`/Review — satın-alınan/yorumlanan kitap embed'i yüksek
   ağırlıkla taste'e; 0 veri, kablo = `Average(weights)` imzası. Tüketici sonraki dilim.

🟥 Hotspot (closed: 028): taste nerede = **Personalization (hesap+MCP sun)**, kNN = Catalog; cross-BC =
   **ScoutAgent orkestrasyon** (get_my_taste MCP → recommend_books MCP; gRPC+m2m İPTAL — Catalog m2m client'ı
   yoktu, IdP seed ağır). Hesap = **on-demand** (feed-load). Cold eşik = **<3 → küratörlü** (popülerlik).
   **Tek centroid** (çamurlu-merkez K-Means faz-2 Python; canlı 18-sorgu merkezi Dostoyevski-ağırlıklı tutarlı
   ama 0.40+ mesafe = bulanıklık gözlendi). Decay yok. Sinyal = arama-only (tıklama-log ayrı iş). Embedding
   client Personalization'a eklendi (Catalog 025 ile AYNI model). Feed yüzeyi = ScoutAgent `/feed` (React ekranı ayrı dilim).

## Sınır (bu BC'nin dokunmadığı)

ONNX/Python/K-Means zevk-kümeleri + tıklama-log + tazelik-decay (sonraki dilim; taste-vektör 028'de geldi —
tek centroid, on-demand). Arama-dışı sinyaller (order/library) kablosu hazır, 0 veri. Event YAYMAZ — yalnız
tüketir (`UserSearched`) + geçmişini HTTP'den sunar/siler + taste'i get_my_taste MCP'den ScoutAgent'a verir.
Kitap-kNN burada DEĞİL (Catalog recommend_books). "Tekrar ara" Catalog'da.
