# EC + Web Canlı — Push-CD Yeniden Hizalama — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mevcut push-SSH CD hattını bugünkü mimariye hizala; ECommerceAgent + ECommerceWeb'i tek k3s cluster'ına canlıya al, master merge otomatik deploy etsin.

**Architecture:** k3s tek-node (`91.132.49.70`), GitHub Actions push-CD (SSH → `kubectl apply` + rollout). Image'lar Aspire (`aspire do push`) → GHCR `:latest`. Manifest'ler elle `deploy/k8s`. Altyapı in-cluster (ParadeDB + RabbitMQ + Redis, PVC kalıcı). Web ayrı repo: nginx image, kendi CI, aynı cluster. Dış erişim Traefik ingress + sslip.io + Let's Encrypt.

**Tech Stack:** k3s, Traefik, cert-manager, ParadeDB (paradedb/paradedb:0.26.6-pg17), RabbitMQ, Redis, .NET 10 Aspire, GitHub Actions, GHCR, Vite/React, nginx.

**Spec:** `docs/superpowers/specs/2026-11-18-live-deploy-cicd-design.md`

## Global Constraints

- **Host şeması (sabit):** api/gateway = `api.91.132.49.70.sslip.io` · identity = `id.91.132.49.70.sslip.io` · web = `app.91.132.49.70.sslip.io`.
- **OIDC issuer tek string, trailing-slash YOK:** `https://id.91.132.49.70.sslip.io` — tüm servis `IdentityOption__Issuer` + react-web `authority` + token `iss` BİREBİR aynı (slash tuzağı: memory `mcp-remote-issuer-slash-gotcha`).
- **Sır değeri git'e/manifest'e GİRMEZ** — `secretKeyRef` / `imagePullSecrets`; cluster'da bir kez `kubectl create secret`.
- **ParadeDB image tag SABİT** `0.26.6-pg17` (`latest-pg17`'ye güvenme).
- **replicas: 1** her serviste (tek-node; scout-agent in-proc).
- **Kapaklar aynı R2 bucket** (canlı+test ortak); `CoverMigration__Enabled=false`, `RegistryBackfill__Enabled=true` (pasif).
- **Checkout kapsam DIŞI** — payment-api deploy edilir ama PG/BankSim yok; checkout bağlanmaz.
- **InvariantGlobalization=true** repo-geneli (Marten ı-tablo + virgül tuzağı kökten çözülü).
- Integration event alanları additive/default'lu (eski tüketici kırılmaz).

## Review Focus

- **OIDC issuer uyuşmazlığı** → her servis + react-web `iss` tam eşleşmezse auth sessiz 401/400. Pin: Task 6 identity, Task 11 web doğrular (canlı login + token `iss` denetimi).
- **catalogDb `pg_search`/`vector` extension yok** → catalog boot/arama 500. Pin: Task 2 init `CREATE EXTENSION IF NOT EXISTS` + psql doğrular.
- **Init 2. rollout'ta tekrar koşmaz (non-idempotent)** → yarım DB yeşil-ama-bozuk. Pin: Task 2 guard'lı init + ikinci apply doğrular.
- **ForwardedHeaders KnownProxies temizli değil** → ingress TLS arkası login 400. Pin: Task 3 ServiceDefaults doğrular (zaten `UseForwardedHeaders`; KnownProxies/Networks temizliği teyit).
- **Google redirect URI canlı host'ta kayıtlı değil** → Google login fail. Pin: Task 5 (sır) notu + Task 11 canlı Google login doğrular.
- **PVC bağlanmıyor (local-path)** → postgres Pending. Pin: Task 2 `kubectl get pvc` Bound doğrular.

---

### Task 1: Eski uygulamaları cluster'dan sök

**Files:** (operasyonel — sunucuda komut; repo değişikliği yok)

**Interfaces:** Produces: temiz cluster (yalnız `ecommerce` ns yaşayacak).

- [ ] **Step 1: Mevcut namespace'leri listele**
Run (sunucuda): `kubectl get ns`
Expected: `agentplatform`, `paymentgateway` (ve/veya banksim) görünür.

- [ ] **Step 2: Eski ns'leri sil**
Run: `kubectl delete ns agentplatform paymentgateway` (varsa banksim ns'i de).

- [ ] **Step 3: Diğer repo CD'lerini durdur**
AgentPlatform + PaymentGateway repo'larında master-push CD'sini devre dışı bırak (workflow `on:` kısıtla veya Actions kapat) — yoksa sonraki push ns'i geri kurar. (Kullanıcı tarafı; onay al.)

- [ ] **Step 4: Doğrula**
Run: `kubectl get ns`
Expected: eski ns'ler YOK; `ecommerce` kalır (sonraki task'larda yeniden kurulur).

---

### Task 2: Altyapı manifest'i yeniden yaz (ParadeDB + PVC + idempotent init)

**Files:**
- Modify: `deploy/k8s/00-infra.yaml` (tam yeniden yazım)

**Interfaces:**
- Produces: `ecommerce` ns · Service `postgres:5432` (ParadeDB) · `rabbitmq:5672/15672` · `redis:6379` · PVC bağlı kalıcı depo · 12 DB + catalogDb extensions.
- Consumes: yok.

**Değişiklikler (mevcut dosyadan delta):**
- Postgres image `postgres:16-alpine` → `paradedb/paradedb:0.26.6-pg17`.
- **PVC ekle:** Postgres + RabbitMQ için `PersistentVolumeClaim` (storageClass `local-path`, ReadWriteOnce); Deployment → volumeMount data dizinine (`/var/lib/postgresql/data`, `/var/lib/rabbitmq`). (Deployment+PVC yerine StatefulSet de kabul; implementer seçer.)
- **Redis ekle:** `redis:7-alpine` Deployment + Service `redis:6379` (cache PVC'siz — ephemeral kabul).
- **Elasticsearch BLOĞUNU SİL** (Deployment + Service).
- **DB listesi güncelle** — init.sql şu 12: `catalogDb basketDb orderDb paymentDb customerDb fileDb identityDb reviewsDb libraryDb stockDb discountDb personalizationDb scoutDb`. (SİL: storefrontDb, checkoutDb. EKLE: identityDb, personalizationDb, scoutDb.)
- **Init idempotent + extension:** `CREATE DATABASE` her biri guard'lı (`SELECT 'CREATE DATABASE "x"' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname='x')\gexec` deseni) + catalogDb'ye bağlanıp `CREATE EXTENSION IF NOT EXISTS pg_search; CREATE EXTENSION IF NOT EXISTS vector;`. (docker-entrypoint-initdb.d yalnız boş volume'da koşar → PVC + ilk açılışta tek sefer; ek güvenlik için guard.)

- [ ] **Step 1: 00-infra.yaml'ı yukarıdaki delta'larla yeniden yaz.**

- [ ] **Step 2: Apply + pod'ları bekle**
Run (sunucuda, Task 4 CD'den önce elle ilk bring-up): `kubectl apply -f deploy/k8s/00-infra.yaml && kubectl -n ecommerce get pods -w`
Expected: postgres/rabbitmq/redis `Running`.

- [ ] **Step 3: PVC bağlı mı**
Run: `kubectl -n ecommerce get pvc`
Expected: STATUS `Bound`.

- [ ] **Step 4: DB + extension doğrula**
Run: `kubectl -n ecommerce exec deploy/postgres -- psql -U postgres -c "\l"` + `... -d catalogDb -c "\dx"`
Expected: 12 DB listelenir; catalogDb'de `pg_search` + `vector` extension görünür.

- [ ] **Step 5: İdempotenslik doğrula**
Run: `kubectl delete pod -n ecommerce -l app=postgres` (restart; PVC kalır) → tekrar Ready; `\l` hâlâ 12 DB, veri kaybı yok.

- [ ] **Step 6: Commit** (`deploy(infra): ParadeDB + PVC + idempotent init, ES söküldü`)

---

### Task 3: EC kod — health prod'da aç + ForwardedHeaders teyidi + identity-server image push

**Files:**
- Modify: `src/aspire/ServiceDefaults/Extensions.cs` (`MapHealthEndpoints` IsDevelopment guard'ı + ForwardedHeadersOptions ~line 24)
- Modify: `src/aspire/AppHost/AppHost.cs` (ghcr foreach)

**Interfaces:**
- Produces: tüm servislerde `/health` + `/alive` prod dahil açık; ingress arkası doğru şema; identity-server image GHCR'a push edilir.
- Consumes: yok.

- [ ] **Step 1: `MapHealthEndpoints`'teki `if (app.Environment.IsDevelopment())` guard'ını kaldır** — health + aliveness map'leri her ortamda koşsun (k8s probe şartı; ADR gereği).

- [ ] **Step 2: ForwardedHeadersOptions teyidi** — `options.KnownNetworks.Clear(); options.KnownProxies.Clear();` mevcut mu kontrol et; yoksa ekle (k8s proxy IP dinamik).

- [ ] **Step 3: AppHost ghcr foreach'ine `identity-server` ekle** — mevcut foreach 13 servisi alıyor, `identity-server` DIŞLANMIŞ → `aspire do push` image'ını basmaz, deploy çekemez. `builder.AddProject<Projects.Identity_Server>(...)` dönüşünü değişkene al, foreach dizisine ekle (veya ayrı `.WithContainerRegistry(ghcr)`).

- [ ] **Step 4: Build**
Run: `dotnet build`
Expected: 0 hata.

- [ ] **Step 5: Commit** (`fix(infra): health prod açık + ForwardedHeaders + identity-server ghcr push`)

---

### Task 4: Servis manifest'lerini yeniden yaz (13 servis + identity, sslip.io, secretKeyRef)

**Files:**
- Modify: `deploy/k8s/10-services.yaml` (tam yeniden yazım)

**Interfaces:**
- Consumes: Task 2 altyapı (postgres/rabbitmq/redis Service'leri), Task 5 secret'ları (`secretKeyRef`).
- Produces: her servis Deployment+Service, `:latest`+`Always`, `replicas:1`, probe'lu.

**Topoloji (AppHost grafiğine birebir) — Deployment+Service her biri:**
`identity-server · catalog-api · basket-api · order-api · payment-api · customer-api · reviews-api · library-api · stock-api · discount-api · file-api · personalization-api · scout-agent · gateway`
(SİL: storefront-api, checkout-orchestrator, mail-mcp, notification-agent, kibana, mailpit.)

**Her servise ortak (mevcut desenden delta):**
- Tüm `IdentityOption__Address`/`__Issuer` → Global Constraints host'u (`https://id.91.132.49.70.sslip.io`, slash YOK). Eski `hasancagridemiriz.com` değerlerini değiştir.
- **Probe ekle:** `livenessProbe`/`readinessProbe` httpGet `/health` port 8080.
- **Sırlar `secretKeyRef`** (düz value DEĞİL): catalog/gateway `AdminApiKeyOptions__AdminApiKey`←`gateway-admin`; scout-agent `ScoutOptions__ApiKey`←`openai`; file-api `R2__AccessKeyId`/`R2__SecretAccessKey`←`r2` (AccountId/Bucket/base non-secret inline); identity-server `BootstrapAdmin__Email`/`Password`←`identity-admin`, Google `Authentication__Google__ClientId`/`ClientSecret`←`google`.
- file-api env: `CoverStore__Backend=R2`, `CoverMigration__Enabled=false`, `CoverMigration__RegistryBackfill__Enabled=true`, `R2__AccountId=92f68fb7048d6312c566a6c28e08dcdb`, `R2__BucketName=ecommercebucket`, `StorageBaseUrls__Bases__R2=https://pub-5232b4cfb5174c27bcea9dcdd6c4fec5.r2.dev`, `CoverStore__RootPath=/data/covers` (+ emptyDir veya PVC).
- **identity-server yeni:** connStr `identityDb`, issuer host, Google secret'ları, `ASPNETCORE_URLS=http://+:8080`. (Not: Identity HTTPS zorunlu — TLS ingress'te sonlanır, ForwardedHeaders şart; Task 3 + Task 7.)
- **scout-agent:** connStr `scoutDb` + MCP downstream service-discovery (catalog-api vb. in-cluster DNS `http://catalog-api:8080`).
- **gateway:** downstream referansları in-cluster DNS; React origin (CORS canlı web host'u `https://app.91.132.49.70.sslip.io`).
- catalog-api `SeedOptions__SourceJsonPath` sil/boş (seed JSON söküldü; veri Excel import'tan).

- [ ] **Step 1: 10-services.yaml'ı yukarıdaki topoloji+delta ile yeniden yaz** (mevcut dosyanın blok desenini izle).

- [ ] **Step 2: YAML geçerlilik (dry-run)**
Run (sunucuda, Task 5 secret'ları kurulduktan sonra): `kubectl apply --dry-run=server -f deploy/k8s/10-services.yaml`
Expected: hata yok (secret ref'leri çözülür).

- [ ] **Step 3: Commit** (`deploy(services): bugünkü 14 servis topolojisi + sslip.io + secretKeyRef`)
(Canlı apply + Ready doğrulaması Task 8 CD'de.)

---

### Task 5: Cluster sırlarını bir kez kur (sunucuda)

**Files:** (operasyonel — sunucuda komut; değerler kullanıcıdan)

**Interfaces:** Produces: `ecommerce` ns'inde Secret'lar (Task 4 `secretKeyRef`'in hedefi) + GitHub repo secret'ları.

- [ ] **Step 1: GHCR çekme secret'ı**
`kubectl -n ecommerce create secret docker-registry ghcr-pull --docker-server=ghcr.io --docker-username=hasancagri --docker-password='<READ_PACKAGES_PAT>'`

- [ ] **Step 2: Uygulama sırları** (değerler kullanıcıdan — İlke VIII: loglama)
`openai` (`--from-literal=apikey=…`), `r2` (`accesskeyid`,`secretaccesskey`), `google` (`clientid`,`clientsecret`), `identity-admin` (`email`,`password`), `gateway-admin` (`apikey`), postgres parola secret'ı (opsiyonel; dev düz kabul).

- [ ] **Step 3: GitHub repo secret'ları** (Settings → Actions): `DEPLOY_SSH_KEY` (dehost_vds private), `GHCR_PAT` (write:packages). Teyit/yenile.

- [ ] **Step 4: Google Console** — canlı redirect URI ekle: `https://id.91.132.49.70.sslip.io/signin-google` (identity external login callback). (Kullanıcı tarafı.)

- [ ] **Step 5: Doğrula**
Run: `kubectl -n ecommerce get secret`
Expected: ghcr-pull, openai, r2, google, identity-admin, gateway-admin listelenir.

---

### Task 6: ci.yml onar (ölü feed + retag listesi + rollout)

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `GHCR_PAT`, `DEPLOY_SSH_KEY` (Task 5).
- Produces: master merge → build-test → image+`:latest` → SSH deploy (güncel servis listesi).

**Değişiklikler:**
- **Ölü `Platform.Auth` kablosunu kaldır:** `GH_PACKAGES_TOKEN` env + NuGet GitHub Packages restore adımları (feed öldü). `dotnet build/test` + `aspire do push` restore'u artık Platform.Auth çekmiyorsa gereksiz; build kırılırsa NuGet.config'teki ölü feed'i de temizle.
- **retag listesi güncelle:** eski `checkout-orchestrator mail-mcp notification-agent storefront-api` ÇIKAR; `identity-server personalization-api scout-agent` EKLE. Güncel 14: `catalog-api basket-api order-api payment-api customer-api reviews-api library-api stock-api discount-api file-api personalization-api scout-agent gateway identity-server`.
- **rollout grep:** `grep -vE "postgres|rabbitmq|redis"` (elasticsearch çıkar, redis ekle).
- deploy job manifest kopyası `deploy/k8s` aynı kalır.

- [ ] **Step 1: ci.yml'ı yukarıdaki delta'larla düzenle.**

- [ ] **Step 2: Build-test kapısını doğrula** — feature branch'e push.
Run: GitHub Actions `build-test` job.
Expected: yeşil (Platform.Auth restore 401'i YOK).

- [ ] **Step 3: Commit** (`ci: ölü Platform.Auth feed çıkar, retag listesi + rollout güncelle`)

---

### Task 7: Traefik ingress + Let's Encrypt (api · id · app)

**Files:**
- Create: `deploy/k8s/20-ingress.yaml`

**Interfaces:**
- Consumes: Task 4 Service'leri (`gateway`, `identity-server`, `web`).
- Produces: dış HTTPS — `api.…sslip.io`→gateway, `id.…sslip.io`→identity-server, `app.…sslip.io`→web; trusted cert.

**İçerik:**
- cert-manager `ClusterIssuer` (Let's Encrypt, HTTP-01 via Traefik) — yoksa kur (as-built ADR'den uyarla).
- 3 IngressRoute/Ingress: host → Service:8080. TLS cert-manager annotation.
- Web (a) path-split değil ayrı host — basit; `app` host web, `api` host gateway. (React `VITE_API_BASE_URL=https://api.…`.)

- [ ] **Step 1: 20-ingress.yaml yaz** (cert-manager issuer + 3 route).

- [ ] **Step 2: Apply + cert**
Run (sunucuda): `kubectl apply -f deploy/k8s/20-ingress.yaml && kubectl -n ecommerce get certificate`
Expected: certificate `Ready=True`.

- [ ] **Step 3: HTTPS doğrula**
Run: `curl -sI https://api.91.132.49.70.sslip.io/health`
Expected: `200`, trusted TLS (cert hatası yok).

- [ ] **Step 4: Commit** (`deploy(ingress): traefik + lets-encrypt api/id/app hostları`)

---

### Task 8: Backend'i canlıya al + Ready doğrula (ilk tam deploy)

**Files:** (operasyonel; Task 2/4/5/7 çıktıları)

**Interfaces:** Consumes: tüm önceki task'lar. Produces: çalışan backend.

- [ ] **Step 1: master'a merge** (Task 2-7 commit'leri) → CD tetikle (image + SSH deploy).

- [ ] **Step 2: Tüm pod'lar Ready**
Run (sunucuda): `kubectl -n ecommerce get pods`
Expected: 14 servis + altyapı `Running`/`Ready`, CrashLoop YOK.

- [ ] **Step 3: Gateway üzerinden sağlık**
Run: `curl -s https://api.91.132.49.70.sslip.io/health` + bir catalog arama ucu (boş sonuç OK — veri Task 11).
Expected: 200.

- [ ] **Step 4: identity login sayfası**
Run: `curl -sI https://id.91.132.49.70.sslip.io/` (OIDC discovery `/.well-known/openid-configuration` `issuer` = host, slash yok).
Expected: 200; issuer doğru.

---

### Task 9: ECommerceWeb — OIDC + API base'i env-parametrik yap

**Files (repo `~/dev/ECommerceWeb`):**
- Modify: `src/auth/authConfig.ts` (hardcoded `localhost:5001`/`5173`)
- Modify: `.env.example` (yeni VITE anahtarları)

**Interfaces:**
- Produces: build-time env ile değişen `authority`, `redirect_uri`, `post_logout_redirect_uri`, `VITE_API_BASE_URL`.

- [ ] **Step 1: authConfig.ts** — `authority: import.meta.env.VITE_OIDC_AUTHORITY`, `redirect_uri: import.meta.env.VITE_OIDC_REDIRECT`, `post_logout_redirect_uri: import.meta.env.VITE_OIDC_POST_LOGOUT`. Fallback local değerler (`localhost:5001`/`5173`) dev için korunur.

- [ ] **Step 2: .env.example** — `VITE_API_BASE_URL`, `VITE_OIDC_AUTHORITY`, `VITE_OIDC_REDIRECT`, `VITE_OIDC_POST_LOGOUT` dokümante.

- [ ] **Step 3: Local build doğrula**
Run: `npm run build`
Expected: `dist/` üretilir, 0 hata.

- [ ] **Step 4: Commit** (`feat: OIDC + API base build-env parametrik (canlı deploy için)`)

---

### Task 10: ECommerceWeb — Dockerfile (nginx) + ci.yml + manifest

**Files (repo `~/dev/ECommerceWeb`):**
- Create: `Dockerfile` (multi-stage node build → nginx)
- Create: `nginx.conf` (SPA fallback `try_files … /index.html`)
- Create: `.github/workflows/ci.yml`
- Create: `deploy/k8s/web.yaml` (Deployment+Service, ns `ecommerce`)

**Interfaces:**
- Consumes: Task 9 env anahtarları (build-arg olarak gömülür), Task 1 cluster, Task 7 `app` host.
- Produces: GHCR image `ghcr.io/hasancagri/ecommerce/web:latest`, `web` Service:80, SSH deploy.

- [ ] **Step 1: Dockerfile** — stage1 `node:22 … npm ci && npm run build` (VITE_* build-arg env ile: `VITE_API_BASE_URL=https://api.91.132.49.70.sslip.io`, OIDC authority/redirect canlı `app` host); stage2 `nginx:alpine` serve `dist/`.

- [ ] **Step 2: nginx.conf** — SPA fallback; `/` statik.

- [ ] **Step 3: web.yaml** — Deployment (`ghcr.io/hasancagri/ecommerce/web:latest`, `Always`, `ghcr-pull`, probe `/`) + Service `web:80`. ns `ecommerce`.

- [ ] **Step 4: ci.yml** — EC deseni: build → docker build+push GHCR (`GHCR_PAT`) → SSH deploy (`DEPLOY_SSH_KEY`) `kubectl apply -f deploy/k8s/web.yaml` + `rollout restart deploy/web`. Repo secret'ları kur.

- [ ] **Step 5: Deploy + doğrula**
Run: master'a push → CD → `curl -sI https://app.91.132.49.70.sslip.io/`
Expected: 200, React index.

- [ ] **Step 6: Commit'ler** (Dockerfile/nginx/ci/manifest).

---

### Task 11: Veri tohumu (Excel import) + uçtan-uca duman testi

**Files:** (operasyonel)

**Interfaces:** Consumes: canlı backend (Task 8) + web (Task 10). Produces: dolu katalog + doğrulanmış yüzey.

- [ ] **Step 1: xlsx'i cluster'a ulaştır** — `Kitapyurdu_Katalog_v2.xlsx` admin import ucuna yükle (gateway `/api/admin/catalog/imports`, `X-Admin-Api-Key`).

- [ ] **Step 2: İki-faz import** — dry-run (rapor) → `POST .../{id}/confirm` → ImportJobWorker `Completed`.
Expected: ~22k ürün INSERT; ProductAdded → kapak zinciri R2'den pasif.

- [ ] **Step 3: Katalog + kapak doğrula**
Run: web `app` host'ta arama yap.
Expected: sonuçlar + kapak görselleri render.

- [ ] **Step 4: Uçtan-uca duman** (canlı web üzerinden, elle):
browse · arama · scout-agent sohbet · login (Google dahil — token `iss` = canlı issuer) · kütüphane · bir kitabın yorumları.
Expected: hepsi çalışır; checkout hariç (kapsam dışı — "ödeme başlatılamadı" beklenen).

- [ ] **Step 5: İdempotens/kalıcılık** — bir servisi restart et, veri durur (PVC).

---

## Notlar
- Task 2/5/7 ilk bring-up'ta sunucuda ELLE koşar (cluster sıfırdan); sonraki değişiklikler CD'den akar.
- CLAUDE.md BC haritası + `deploy/k8s/README.md` güncellenmeli (as-built) — Task 8 sonrası ayrı küçük commit.
