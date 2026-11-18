# deploy/k8s — ECommerce k8s manifest'leri + CD

Elle yazılmış (öğrenme/dev) k8s manifest'leri. CI/CD (`.github/workflows/ci.yml`) master merge'inde
image basar, GHCR'ye iter, buradaki manifest'leri SSH ile cluster'a uygular + app rollout.

## Dosyalar
- `00-infra.yaml` — namespace `ecommerce` + Postgres(**ParadeDB** pg17: pg_search+pgvector) + RabbitMQ + Redis.
  Postgres+RabbitMQ **PVC kalıcı** (local-path); init (idempotent .sh) 12 BC DB'si + catalogDb extension.
- `10-services.yaml` — 14 servis (identity-server + 13 API). Image `:latest` + `imagePullPolicy Always`.
  Issuer tek string `https://id.hasancagridemiriz.com` (slash YOK). Sırlar `secretKeyRef`. Probe /alive+/health.
- `20-ingress.yaml` — Traefik ingress + cert-manager Let's Encrypt. `app` host (web+API path-split),
  `id` host (identity). **checkout kapsam DIŞI** (payment-api boot olur, PG/BankSim yok).

## CD akışı (push-tabanlı, GitHub Actions)
```
push master → build-test → image (aspire do push + :latest retag, 14 servis) → deploy (SSH → kubectl)
```
k3s API (6443) internete KAPALI → `deploy` job port 22'den SSH, `kubectl apply` + app `rollout restart`
(altyapı postgres/rabbitmq/redis hariç tutulur → PVC verisi korunur).

## BİR KERELİK ön şartlar (CD'de DEĞİL — elle, sunucuda)

### 1. GitHub repo secret'ları (Settings → Actions)
- `DEPLOY_SSH_KEY` — dehost_vds **private** key (runner SSH eder).
- `GHCR_PAT` — `write:packages` PAT (user-namespace'e image push; GITHUB_TOKEN yetmez).

### 2. cert-manager kur (k3s'te Traefik gömülü, cert-manager DEĞİL)
```bash
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml
```

### 3. Cluster secret'ları (ns ecommerce — manifest'e GÖMÜLMEZ, secretKeyRef hedefi)
```bash
kubectl -n ecommerce create secret docker-registry ghcr-pull \
  --docker-server=ghcr.io --docker-username=hasancagri --docker-password='<WRITE_PACKAGES_PAT>'
kubectl -n ecommerce create secret generic openai         --from-literal=apikey='<OPENAI_KEY>'
kubectl -n ecommerce create secret generic r2             --from-literal=accesskeyid='<R2_KEY_ID>' --from-literal=secretaccesskey='<R2_SECRET>'
kubectl -n ecommerce create secret generic google         --from-literal=clientid='<G_CLIENT_ID>' --from-literal=clientsecret='<G_SECRET>'
kubectl -n ecommerce create secret generic identity-admin --from-literal=email='<ADMIN_EMAIL>' --from-literal=password='<ADMIN_PASS>'
kubectl -n ecommerce create secret generic gateway-admin  --from-literal=apikey='<ADMIN_API_KEY>'
```

### 4. Google Console
Yetkili redirect URI ekle: `https://id.hasancagridemiriz.com/signin-google`.

### 5. İlk bring-up (sıfırdan cluster — CD'den önce elle)
```bash
kubectl apply -f 00-infra.yaml      # altyapı + PVC + init; pod Ready + PVC Bound bekle
kubectl apply -f 20-ingress.yaml    # cert-manager + ingress (certificate Ready bekle)
# 10-services.yaml ilk deploy CD'den akar (image GHCR'da olmalı → önce master merge)
```

## DEV uyarıları (prod değil)
- PG parolası manifest'te düz (cluster-only, ufw-kapalı 5432). Prod: Secret + managed DB.
- Redis PVC'siz (bayat-toleranslı cache → ephemeral kabul). Postgres+RabbitMQ PVC'li (veri durur).
- Web image + manifest ayrı repo'da (ECommerceWeb): `deploy/k8s/web.yaml`, kendi CD'siyle deploy edilir.
