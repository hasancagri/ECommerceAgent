# deploy/k8s — ECommerce k8s manifest'leri + CD

Elle yazılmış (öğrenme/dev) k8s manifest'leri. CI/CD (`.github/workflows/ci.yml`) master merge'inde
image basar, GHCR'ye iter, buradaki manifest'leri cluster'a uygular.

## Dosyalar
- `00-infra.yaml` — namespace `ecommerce` + Postgres (init ile 12 BC DB'si) + RabbitMQ + Elasticsearch.
- `10-services.yaml` — 15 ECommerce servisi (image `:latest` + `imagePullPolicy: Always`).

## CD akışı (push-tabanlı, GitHub Actions)
```
push master → build-test → image (aspire do push + :latest retag) → deploy (SSH → kubectl)
```
k3s API (6443) internete KAPALI → `deploy` job port 22'den SSH eder, `kubectl`'i SUNUCUDA koşar
(`kubectl apply` + app rollout restart; altyapı pg/rabbit/es hariç tutulur → ephemeral veri korunur).

## BİR KERELİK ön şartlar (CD'de DEĞİL — elle)

### 1. GitHub repo secret: `DEPLOY_SSH_KEY`
Runner'ın SSH edebilmesi için deploy key'in **private** kısmı:
```
Settings → Secrets and variables → Actions → New repository secret
Name: DEPLOY_SSH_KEY   Value: ~/.ssh/dehost_vds içeriği (private key, tümü)
```

### 2. Cluster secret'ları (sunucuda, bir kez)
Sırlar manifest'e GÖMÜLMEZ; cluster'da Secret olarak kurulur, manifest `secretKeyRef`/`imagePullSecrets`'le işaret eder:
```bash
# GHCR private image çekme (PAT ile):
kubectl -n ecommerce create secret docker-registry ghcr-pull \
  --docker-server=ghcr.io --docker-username=hasancagri --docker-password='<READ_PACKAGES_PAT>'
# OpenAI (storefront + notification):
kubectl -n ecommerce create secret generic openai --from-literal=apikey='<OPENAI_KEY>'
```

### 3. Host sysctl (ES 8.x şartı, bir kez)
```bash
sysctl -w vm.max_map_count=262144 && echo 'vm.max_map_count=262144' >> /etc/sysctl.conf
```

## DEV uyarıları (prod değil)
- PG parolası manifest'te düz (cluster-only, ufw-kapalı 5432). Prod: Secret.
- Postgres + Elasticsearch **ephemeral** (PVC yok) → restart'ta veri gider. Prod: PVC/managed.
- `IdentityOption/SagaAuth/Payment/R2/Smtp` = placeholder → gerçek auth/ödeme/mail çalışmaz.
- Probe yok (health prod'da kapalı, `ServiceDefaults` IsDevelopment guard — prod'da açılmalı).
