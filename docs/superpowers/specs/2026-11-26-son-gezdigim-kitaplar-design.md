# Son Gezdiğim Kitaplar — Tasarım (küçük değişiklik)

**Tarih:** 2026-11-26 · **Durum:** onay bekliyor

## Niyet / neden
Login kullanıcı gezdiği kitapları ana sayfada tanıdık bir şeritte görsün (Kitapyurdu "Son
Gezdiğiniz Ürünler" gibi). Kişisel, sahip-olunan sinyal — her senaryoda (1000kitap yönü de dükkan
yönü de) doğru, pişmanlık-riski yok. Zevk profili/öneri işinin uzun-vade girdisi, ama **bu dilimde
yalnız liste** (embedding/taste YOK).

## Hedefler
- Login kullanıcı kitap detayını açınca kayıt düşer.
- Ana sayfada (`DiscoverWelcome`) **alt şerit** "Son Gezdiğim Kitaplar" — en yeni önce, kapaklı kartlar.
- Aynı kitap tekilleşir (tekrar bakış tarihi günceller, satır çoğaltmaz), **cap 20**.

## Hedef-dışı (non-goals)
- Embedding / zevk-vektörü / öneri hesabı — park ([[book-view-taste-signal-parked]]), bu dilimde yok.
- Anonim takip / merge — login şart, anonim 401, hiç kaydetmez.
- BC-arası zenginleştirme (Catalog'a S2S sorgu) — yok (bkz. yaklaşım A).
- Kişisel Kitaplığım rafları (okuma-durumu) — ayrı/sonraki dilim.

## Yaklaşımlar (künye nereden gelir?)
`UserBookView` yalnız `productId` tutsa, şeritte kart basmak için ad/yazar/kapak lazım; Library'de
kitap metadata yok (BC izolasyonu).
- **A — Anlık snapshot (SEÇİLDİ):** `POST /viewed` künyeyi de taşır; detay sayfası (`BookDetail`) zaten
  render etmiş, bedava gönderir. GET direkt döndürür → **sıfır BC-arası çağrı.** Bayatlık (kapak)
  zararsız — "son gezdiğin" tarihsel görüntü.
- B — productId-only + frontend Catalog batch-resolve: +1 uç, +1 çağrı, +orkestrasyon. Red.
- C — Library S2S gRPC Catalog'a sorar: proto + client, en çok iş. Red.
- **Neden A:** en az iş, sıfır bağ; embedding park edildiği için productId-only'nin tek faydası (S2S
  taste) zaten yok.

## Karar verilen "nasıl"
**Backend (ECommerceAgent, `library` BC — ilk REST yüzeyi):**
- `Domains/UserBookView/UserBookView.cs` — `AggregateRoot`; alanlar: `UserId`, `ProductId`, snapshot
  (`Title`, `Author`, `CoverUrl`, `Isbn`), `ViewedAt`. İdempotent upsert (userId+productId tek kayıt;
  varsa `ViewedAt`+snapshot güncelle) — `UserPurchase` idempotent-upsert desenini izle.
- `Features/Commands/RecordView.cs` — `POST /library/v1/viewed` (auth). Gövde: productId+snapshot.
- `Features/Queries/GetRecentlyViewed.cs` — `GET /library/v1/viewed` (auth). En yeni önce, cap 20.
- `UserBookViewEndpointExtension` + `Program.cs`'e `Map…` (+ Wolverine `IncludeType` gerekirse).
- **Auth:** SPA access-token'ı audience taşımaz → personalization 013 deseni (inline JWT,
  `ValidateAudience=false`, `RequireAuthorization`, kimlik `sub`'tan). Library'nin mevcut scope-temelli
  `/mcp` auth'uyla uzlaştır (builder'a bırakılan nokta — iki şema mı, tek SPA-uyumlu şema mı).
- **Gateway:** `/api/library/{**catch-all}` → cluster `library` → `http://library-api`.

**Frontend (ECommerceWeb):**
- `src/api/library.ts` (yeni): `recordView(token, book)` + `getRecentlyViewed(token)`.
- `BookDetail.tsx`: kitap yüklenince + authenticated ise `recordView` (ateşle-unut, hata yutar —
  sayfayı bozmaz; anonimse hiç çağırma).
- `DiscoverWelcome.tsx`: **alt şerit** — authenticated ise `getRecentlyViewed`, kapaklı yatay kartlar;
  anonim/boş/hata → render yok (sessiz, sayfa bozulmaz — `PersonalShelf` deseni). Kart linki
  `/kitap/{slug}-{isbn}`.

## Builder'a bırakılan
- Library auth şema uzlaşması (SPA JWT vs mevcut scope auth).
- `UserBookView` Marten belge adı/şema + index (`UserId`, `ProductId`); InvariantGlobalization global
  kural ı'yı hallediyor.
- Şerit kart bileşeni yeniden mi kullanılır (mevcut `BookCard`/`BookThumb`) yoksa sade yatay kart mı.
- Snapshot alan seti kesinleşmesi (link için isbn+slug yeter; yazar tek string).

## Boyut
Küçük-orta: tek yeni aggregate + tablo + 2 uç + 1 gateway rotası + 2 React dokunuşu. Proje konvansiyonu
(yeni aggregate/tablo) tam speckit önerir; kullanıcı sade tercih ediyor → onay sonrası speckit-specify
mı yoksa doğrudan plan mı, kullanıcı seçer.
