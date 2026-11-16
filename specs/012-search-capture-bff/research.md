# Research: Kullanıcı Arama Kaydı + App-BFF

## 1. BFF = yeni app/edge orkestratör (BC değil)

- **Decision**: `src/others/Bff` (Gateway yanı) — durumsuz Minimal API servisi. JWT bearer (OIDC mevcut), `HttpClient` + Aspire service discovery ile Catalog'u çağırır, Wolverine+RabbitMQ ile `UserSearched` yayınlar. DB YOK, domain aggregate YOK.
- **Rationale**: Web+mobile ortak giriş; kullanıcı-niyet sinyalinin doğal yayıncısı; ileride ChatAgent/feed evi. BC değil (domain verisi yok) → İlke I BC-izolasyonu dışında, "yeni yüzey". Catalog'un **public REST aramasını** çağırır (tarayıcının çağırdığı yüzey) — BC→BC backchannel değil, Catalog'a dokunulmaz.
- **Alternatives**: Catalog `UserSearched` yayınlasın — aktivite sorumluluğunu saf ürün BC'sine sızdırır, mobile ortak giriş yok; reddedildi. Gateway'i BFF'e terfi — YARP bilinçli ince, mantık karışır; reddedildi.

## 2. BFF→Catalog çağrısı: REST + service discovery

- **Decision**: BFF, Catalog'un `POST`/`GET /api/catalog/products/search` (011) yüzeyini `HttpClient` ile çağırır; adres Aspire service discovery'den (`http://catalog-api`). Sonucu istemciye aynen döner.
- **Rationale**: 011 arama zaten REST; BFF onun istemcisi (tarayıcı gibi). gRPC S2S kurmak tek-tüketici + var olan REST için gereksiz seremoni. Catalog değişmez.
- **Alternatives**: BFF→Catalog gRPC — yeni proto + sunucu sarmalayıcı, 011 REST'i kopyalar; reddedildi. BFF Catalog DB'sine erişsin — İlke I ihlali; reddedildi.

## 3. UserSearched integration event (Wolverine→RabbitMQ fanout)

- **Decision**: `Shared.IntegrationEvents.UserSearched` + `RabbitMqConstants.UserSearched.Exchange` (fanout) + `Queues.Personalization`. **Yayıncı = BFF** (exchange deklare + `PublishMessage<UserSearched>().ToRabbitExchange`). **Tüketici = Personalization** (exchange deklare + `BindQueue(Personalization)` + `ListenToRabbitQueue`). Binding'i tüketici kurar (soğuk-açılış dersi).
- **Rationale**: Repo integration-event deseni birebir (ProductAdded/StockChanged gibi). Fanout → ileride başka tüketici (örn. order/library sinyal birleştirici) bedava bind eder. Publish `[Transactional]`/outbox — ama BFF durumsuz, DB yok → outbox olmadan doğrudan publish; teslim RabbitMQ durability + tüketici retry/DLQ'ya dayanır.
- **FLAG (BFF outbox)**: BFF'in DB'si olmadığından transactional-outbox yok; publish fire-ve-RabbitMQ-durable. Nadir publish-kaybı kabul (arama kaydı düşük-önem, ödeme değil). Dayanıklılık asıl tüketici tarafında (retry→error-queue).

## 4. Capture yalnız login (kimlik BFF'te çözülür)

- **Decision**: BFF JWT'yi doğrular (007 ScoutAgent deseni: `ValidateAudience=false`, issuer/signature/lifetime), `CurrentUser.Load`. userId varsa `UserSearched` yayınlanır; anonim (token yok/geçersiz) → yayınlanmaz. React login'de `Authorization: Bearer` gönderir, Gateway iletir.
- **Rationale**: "login'de kaydet" kuralı tek yerde (BFF). Anonim arama tam çalışır, sinyal üretmez — gizlilik sınırı.

## 5. UserSearchRecord saklama (append-only, Marten)

- **Decision**: Personalization `personalizationDb`'de `UserSearchRecord` Marten **belgesi** (AggregateRoot değil — append-only log; `Create` fabrikası + validasyon). Her event bir satır (upsert değil, insert). `UserId` index (sonra kullanıcı-bazlı okuma/ML için).
- **Rationale**: Ham log en esnek; zevk profili/ONNX sonra bundan türetilir — erken şema kilidine girmez (data-model §). Document yeterli (mutasyon yok); İlke II "read-model/log aggregate değil" istisnası.
- **Alternatives**: Toplanmış profil (etiket ağırlıkları) — erken yorum kilidi, v1'de gereksiz; reddedildi.

## Çözülen NEEDS CLARIFICATION

- Yok — brainstorm'da tüm kararlar netleşti (BFF şekli/yeri, publisher=BFF, login-only, ham log, REST BFF→Catalog).
