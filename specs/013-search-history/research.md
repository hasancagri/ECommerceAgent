# Research: Arama Geçmişi Görüntüleme (013)

Spec'te açık NEEDS CLARIFICATION yok (clarify tamamlandı). Kararlar + gerekçeler:

## JWT auth — inline (ValidateAudience=false), zorunlu

- **Decision**: Personalization Program.cs'e inline JWT bearer eklenir (`Authority=IdentityOption.Address`, `MapInboundClaims=false`, `ValidateAudience=false`, issuer/imza/süre doğrulanır) + `ICurrentUser`/`HttpContextAccessor`; geçmiş uçları `.RequireAuthorization()` (anonim 401).
- **Rationale**: SPA access-token audience taşımaz (yalnız scope). Common `AddAuthenticationAndAuthorizationExtension` `ValidateAudience=true` + `Audience` set eder → audience'sız token REDDEDİLİR. Catalog 012'de aynı tuzağa inline desenle çözüldü — birebir tekrarlanır (bilinçli tekrar, BC izolasyonu).
- **Alternatives**: Common extension reuse (audience uyumsuzluğu → red); opsiyonel JWT (geçmiş kişisel → zorunlu olmalı, reddedildi).

## Named scope YOK — authenticated + sub filtresi

- **Decision**: Yeni `KnownScope` eklenmez; uçlar yalnız authenticated ister, veri `currentUser.Id` (sub) ile süzülür.
- **Rationale**: Kişisel-veri erişimi kimliğe göredir, rol-otoritesine değil (İlke V scope'ları rol-kapılı kaynaklar içindir). Scope eklemek bu veri için aşırı.
- **Alternatives**: `personalization.read` scope (gereksiz RBAC kurulumu, red).

## Tekilleştirme — in-memory saf fonksiyon

- **Decision**: Query handler kullanıcının ham kayıtlarını `IQuerySession` ile çeker; saf `SearchHistoryProjection.Deduplicate` normalize-grupla-sırala yapar; sayfalama dedup SONRASI uygulanır.
- **Rationale**: Kullanıcı başına kayıt az → in-memory ucuz; saf fonksiyon İlke VI test-first (SQL `distinct on` test edilemezdi). Normalize mantığı (Türkçe İ/ı + büyük/küçük fold) tek yerde, testli.
- **Alternatives**: SQL `distinct on`/jsonb group (test edilemez, Marten-bağlı, Türkçe-fold SQL'de kırılgan — red).

## Normalize — Personalization'a özel kopya

- **Decision**: Türkçe-duyarlı fold (büyük/küçük + İ/ı) Personalization içinde küçük saf helper olarak yazılır (Catalog `NameNormalization` paylaşılmaz).
- **Rationale**: BC izolasyonu + bilinçli tekrar (conventions). Sözleşme davranıştır, kod değil.
- **Alternatives**: Catalog helper'ı paylaş (BC coupling, red).

## Silme — hard, command handler + IDocumentSession

- **Decision**: Delete-one = o normalize sorguya eşleşen kullanıcının TÜM kayıtlarını `session.DeleteWhere`-benzeri filtreyle fiziksel sil; clear-all = kullanıcının tüm kayıtları. `[Transactional]` command handler.
- **Rationale**: Clarify kararı hard delete (kullanıcı niyeti). Toplu silme aggregate davranışı değil (UserSearchRecord log) → handler'da doğrudan session.
- **Alternatives**: Soft-delete flag (clarify'da reddedildi); aggregate metodu (UserSearchRecord aggregate değil).

## Tekrar-ara — frontend yönlendirme

- **Decision**: Geçmiş öğesine tıklama React'te arama/keşif ekranına gider ve sorgu+modu orada çalıştırır (mevcut `searchProducts` + Catalog ucu). Yeni `UserSearched` üretir; dedup recency'yi günceller.
- **Rationale**: Tek sonuç UX'i, DRY; 012 yolu zaten var. Backend'e ek uç gerekmez.
- **Alternatives**: Geçmiş sayfasında satır-içi sonuç (ikinci sonuç görünümü, red).

## Gateway + AppHost

- **Decision**: Gateway'e `/api/personalization/**` → `personalization` cluster (`http://personalization-api`); AppHost'ta `gateway.WithReference(personalizationApi).WaitFor(...)`.
- **Rationale**: React tek-origin gateway'den geçer (004 deseni). Personalization şimdiye dek gateway'e bağlı değildi (dış yüzeyi yoktu).
- **Alternatives**: Doğrudan servise (service discovery tarayıcıda yok, red).
