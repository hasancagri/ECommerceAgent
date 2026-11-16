# Catalog Arama-Kaydı (BFF Söküm) Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** App-BFF orkestratör katmanını sök; arama kaydını (`UserSearched`) Catalog.Api kendi arama ucundan yayınlasın, Personalization aynı şekilde tüketsin.

**Architecture:** Catalog arama ucuna opsiyonel JWT (007 deseni) eklenir; endpoint query'yi çağırdıktan sonra login ise `UserSearched` yayar (query saf okuma kalır — publish endpoint'te). Event kaynağı Bff→Catalog olur (exchange/queue/consumer adları değişir). BFF projesi + gateway `discovery` route + AppHost kaydı tamamen silinir. React aramayı doğrudan Catalog'a (gateway `/api/catalog/**`) yollar, token'ı header'da taşır.

**Tech Stack:** .NET 10, Wolverine + RabbitMQ (fanout), Marten, YARP gateway, React (ECommerceWeb repo), OpenIddict JWT (Identity.Server, issuer `https://localhost:5001/`).

**Spec:** Bu plan brainstorming'de onaylanan kısa tasarımı uygular (konuşma kaydı). Yeni event/tablo/aggregate YOK — `IntegrationEvents.UserSearched` + `UserSearchRecord` zaten var (012).

## Global Constraints

- Test disiplini (CLAUDE.md): endpoint/auth/messaging = **Aspire'da canlı doğrulama**, xUnit DEĞİL. Saf domain (`UserSearchRecord.Create`) zaten test'li, değişmiyor — yeni unit test yok.
- Sistem HEP Aspire AppHost'tan başlatılır; tek servis bağımsız açılmaz.
- Paket sürümü değişikliği yok → `Directory.Packages.props`'a dokunma.
- JWT opsiyonel: `ValidateAudience=false`, arama ucunda `RequireAuthorization` YOK (anonim serbest). Issuer/imza/süre doğrulanır. Token audience taşımaz (yalnız scope) → audience doğrulanmaz.
- Wolverine `*Consumers` çoğul adı keşfedilmez → `IncludeType` ZORUNLU (rename edilince kayıt da güncellenir).
- Integration event additive; exchange yayıncıda deklare, binding tüketicide (007 soğuk-açılış dersi).
- Commit öncesi: `dotnet build && dotnet test` yeşil.
- FLOW.md domain süreci değiştiği için AYNI değişiklikte güncellenir (İlke VII).

## Review Focus

- **Anonim arama (token yok):** arama sonuç döner, `UserSearched` YAYILMAZ, kayıt oluşmaz. → Task 4 canlı doğrulama (anonim curl).
- **Geçersiz/süresi dolmuş token:** 401 DEĞİL — anonim sayılır, arama yine çalışır. → Task 4 (bozuk token curl).
- **Boş sorgu + login:** publish guard'ı `q` boşsa yaymaz. → Task 2 endpoint guard + Task 4.
- **Publish hatası (RabbitMQ erişilemez):** arama sonucu yine döner (fire-and-forget, catch+log). → Task 2 try/catch, BFF deseninin aynısı.
- **JWKS/cert — KRİTİK:** Catalog token'ı gerçekten doğrulayıp `sub`'ı çözmeli (BFF'te başarısız olan tam nokta). Identity `https://localhost:5001/` ayakta + dev-cert güvenli olmalı. → Task 4 acceptance = login aramada kayıt yazılması; yazılmazsa dashboard catalog-api log `IDX*` ile JWKS/cert debug.

---

### Task 1: Event kaynağını Bff→Catalog yeniden adlandır

**Files:**
- Modify: `src/others/Shared/RabbitMqConstants.cs:147-156` (`UserSearched.Exchange` `bff.user-searched` → `catalog.user-searched`; yorum Bff→Catalog)
- Rename: `src/services/personalization/Personalization.Api/BffConsumers.cs` → `CatalogSearchConsumers.cs` (sınıf adı dahil)
- Modify: `src/services/personalization/Personalization.Api/Extensions/MessagingExtensions.cs:33` (`IncludeType(typeof(...BffConsumers))` → `CatalogSearchConsumers`)
- Modify: `src/services/personalization/FLOW.md:12` (kaynak `⇦ Bff` → `⇦ Catalog`, anchor `BffConsumers` → `CatalogSearchConsumers`)

**Interfaces:**
- Produces: `RabbitMqConstants.UserSearched.Exchange == "catalog.user-searched"`, kuyruk `Personalization` değişmez (`personalization.user-searched`); `Personalization.Api.CatalogSearchConsumers.Handle(IntegrationEvents.UserSearched, IDocumentSession, CancellationToken)`.

- [ ] **Step 1: Exchange sabitini + yorumu güncelle** — `RabbitMqConstants.UserSearched`: `Exchange = "catalog.user-searched"`; blok yorumu "app-BFF yayınlar" → "Catalog arama ucu yayınlar".
- [ ] **Step 2: Consumer dosyasını+sınıfını yeniden adlandır** — `BffConsumers` → `CatalogSearchConsumers`; dosya başı yorumda kaynak Bff→Catalog; `Handle` imzası ve gövdesi AYNEN kalır.
- [ ] **Step 3: IncludeType kaydını güncelle** — `MessagingExtensions.cs:33` yeni tip adına.
- [ ] **Step 4: FLOW.md satırını güncelle** — kaynak + anchor.
- [ ] **Step 5: Build** — Run: `dotnet build`; Expected: 0 hata (BFF hâlâ aynı sabiti kullanır, değer değişimi derlemeyi bozmaz).
- [ ] **Step 6: Commit** — `refactor(012): UserSearched kaynağını Bff→Catalog yeniden adlandır`

---

### Task 2: Catalog arama ucu login aramada UserSearched yayınlar

**Files:**
- Modify: `src/services/catalog/Catalog.Api/Program.cs` (opsiyonel JWT auth + `ICurrentUser` DI + `UseAuthentication/UseAuthorization`)
- Modify: `src/services/catalog/Catalog.Api/Extensions/MessagingExtensions.cs` (`UserSearched` exchange deklare + publish map)
- Modify: `src/services/catalog/Catalog.Api/Domains/Products/ProductEndpointExtension.cs:19-37` (`/search` ucunda query sonrası login publish)
- Modify: `src/services/catalog/Catalog.Api/appsettings.Development.json` (`IdentityOption` section — zaten var, doğrula)
- Modify: `src/services/catalog/FLOW.md` (arama → `UserSearched` yay adımı/dikişi `⇨ Personalization`)

**Interfaces:**
- Consumes: Task 1 `RabbitMqConstants.UserSearched.Exchange`; `Common.Auths.ICurrentUser` + `CurrentUser` (`Load(ClaimsPrincipal)` → `Id` `sub`'tan); `IntegrationEvents.UserSearched(Guid,string,string,int,DateTimeOffset)`.
- Produces: `/api/catalog/products/search` ucu opsiyonel bearer kabul eder; login + dolu sorguda `UserSearched` fanout.

- [ ] **Step 1: Catalog'a opsiyonel JWT auth ekle** — `Program.cs` `builder.Build()` ÖNCESİ, BFF Program.cs:10-25 desenini uygula: `IdentityOption` oku → `AddAuthentication(JwtBearerDefaults...).AddJwtBearer` (`Authority=identity.Address`, `RequireHttpsMetadata=false`, `MapInboundClaims=false`, `TokenValidationParameters{ValidateAudience=false, ValidateIssuer=true, ValidateIssuerSigningKey=true, ValidateLifetime=true}`) + `AddAuthorization()`. `ICurrentUser`→`CurrentUser` transient kaydı. ApiKey şeması KORUNUR (iki şema yan yana).
- [ ] **Step 2: Auth middleware'i pipeline'a ekle** — `app.UseApiKeyAuthentication()` yanında `app.UseAuthentication(); app.UseAuthorization();` (MapProductEndpoints ÖNCESİ). Not: artık JWT şeması kayıtlı → açılışta fırlatma tuzağı (eski söküm yorumu) geçerli değil; yorumu güncelle.
- [ ] **Step 3: Catalog messaging'e UserSearched publish ekle** — `MessagingExtensions.cs` mevcut `DeclareExchange`/`PublishMessage` desenine ekle: `DeclareExchange(RabbitMqConstants.UserSearched.Exchange, Fanout)` + `PublishMessage<IntegrationEvents.UserSearched>().ToRabbitExchange(...)`.
- [ ] **Step 4: Search ucunda login publish** — `/search` handler imzasına `ICurrentUser currentUser, HttpContext http, ILoggerFactory loggerFactory` ekle. Query sonrası, `Results.Ok` ÖNCESİ: `currentUser.Load(http.User); if (currentUser.Id != Guid.Empty && !string.IsNullOrWhiteSpace(q)) { try { await bus.PublishAsync(new IntegrationEvents.UserSearched(currentUser.Id, q!.Trim(), normalizedMode, result.Data?.Count ?? 0, DateTimeOffset.UtcNow)); } catch (Exception ex) { logger.LogWarning(ex, "..."); } }`. `normalizedMode` = `mode` boşsa `"keyword"` yoksa `mode!.Trim().ToLowerInvariant()`. Publish arama sonucunu ETKİLEMEZ.
- [ ] **Step 5: FLOW.md güncelle** — Catalog arama adımına `🟧 Kullanıcı aradı → UserSearched ⇨ Personalization` dikişi.
- [ ] **Step 6: Build + test** — Run: `dotnet build && dotnet test`; Expected: 0 hata, testler yeşil.
- [ ] **Step 7: Commit** — `feat(012): Catalog arama ucu login aramada UserSearched yayınlar`

---

### Task 3: BFF projesini ve tüm referanslarını sil

**Files:**
- Delete: `src/others/Bff/` (tüm proje)
- Modify: `ECommerceAgent.slnx:8` (`Bff.csproj` satırını sil)
- Modify: `src/aspire/AppHost/AppHost.cs` (bff `AddProject` bloğu ~173-176, gateway `.WithReference(bff)`/`.WaitFor(bff)` ~224/228, final kaynak listesindeki `bff` ~241; bff yorumları)
- Modify: `src/others/Gateway/appsettings.json` (`Routes.discovery` 24-27 + `Clusters.bff` 40-44 sil)

**Interfaces:**
- Produces: çözümde BFF yok; gateway'de `discovery` route/`bff` cluster yok; AppHost bff kaydı yok.

- [ ] **Step 1: AppHost'tan bff'i çıkar** — `AddProject<Projects.Bff>` bloğu, gateway'in `.WithReference(bff)`+`.WaitFor(bff)`, `builder.AddXxx(... bff)` final listesi. Personalization kaydı KALIR.
- [ ] **Step 2: Gateway route+cluster sil** — `discovery` route ve `bff` cluster JSON blokları.
- [ ] **Step 3: slnx'ten çıkar** — `Bff.csproj` proje satırı.
- [ ] **Step 4: BFF klasörünü sil** — `rm -rf src/others/Bff`.
- [ ] **Step 5: Build + test** — Run: `dotnet build && dotnet test`; Expected: 0 hata (hiçbir proje BFF'e referans vermez — doğrula), testler yeşil.
- [ ] **Step 6: Commit** — `refactor(012): app-BFF orkestratör katmanını sök (Catalog capture'a taşındı)`

---

### Task 4: React aramayı doğrudan Catalog'a yönlendir

**Files (repo: `~/dev/ECommerceWeb`):**
- Modify: `src/api/catalog.ts` (`searchProducts`: POST `/api/discovery/search` → GET `/api/catalog/products/search?q=&mode=&limit=`, opsiyonel bearer header)

**Interfaces:**
- Consumes: Catalog `/api/catalog/products/search` (GET, query string, opsiyonel `Authorization: Bearer`).
- Produces: `searchProducts(query, mode, limit, token?)` imzası AYNEN kalır (çağıran `ChatWorkspace.tsx:70` değişmez).

- [ ] **Step 1: searchProducts'ı GET'e çevir** — gövde yerine query string (`q`, `mode`, `limit`), `token` varsa `Authorization: Bearer ${token}` header. 503 hata mesajı davranışı korunur. Yorumu güncelle (BFF/discovery referansı → doğrudan Catalog).
- [ ] **Step 2: Build** — Run: `cd ~/dev/ECommerceWeb && npm run build`; Expected: tip hatası yok.
- [ ] **Step 3: Commit** (ECommerceWeb repo) — `refactor: arama doğrudan Catalog'a (app-BFF söküldü)`

---

### Task 5: Aspire canlı uçtan-uca doğrulama (acceptance)

**Files:** yok (manuel/komut doğrulama).

- [ ] **Step 1: Aspire + Identity ayakta** — kullanıcı AppHost'u başlatır; `curl -sk https://localhost:5001/.well-known/openid-configuration` → 200, dashboard catalog-api + personalization-api yeşil.
- [ ] **Step 2: Anonim arama** — gateway'e token'sız GET `/api/catalog/products/search?q=dostoyevski&mode=keyword` → 200 + sonuç; personalizationDb'de kayıt OLUŞMAZ (anonim).
- [ ] **Step 3: Login arama** — React çıkış/giriş (taze token) → login'ken ara VEYA taze bearer ile gateway'e GET. Doğrula: `mt_doc_usersearchrecord` satırı (Query/Mode/ResultCount/SearchedAt) + rabbit `personalization.user-searched` consume edilmiş.
- [ ] **Step 4: Bozuk token** — geçersiz bearer ile arama → 200 + sonuç (401 değil), kayıt oluşmaz.
- [ ] **Step 5: Eğer login kaydı oluşmazsa** — dashboard catalog-api log → `IDX*`/`authentication failed`/`Unable to obtain configuration` ara → JWKS/cert fix (dev `BackchannelHttpHandler` cert-ignore veya issuer). BFF'te çözemediğimiz nokta burada kapanır.
