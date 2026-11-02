---
status: Kabul
---

# ADR: Canlı Dağıtım — k3s + Push-tabanlı CD (SSH), sslip.io, in-cluster altyapı (dev)

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + 3 reponun `.github/workflows/ci.yml`
> ile `deploy/k8s/*`. As-built: ECommerceAgent `deploy/k8s/{00-infra,10-services}.yaml`, AgentPlatform
> `deploy/k8s/agentplatform.yaml`, PaymentGateway `deploy/k8s/paymentgateway.yaml`; her repo `ci.yml`
> (build-test → image+:latest → deploy). İlgili: [adr-external-agent-oauth-061](adr-external-agent-oauth-061.md)
> (connector OAuth), [adr-mcp-control-plane-no-secret-return](adr-mcp-control-plane-no-secret-return.md)
> (sırlar). İlk canlı dağıtım 2026-10-12 (Dehost VDS `91.132.49.70`, tek-node).

## Karar (tek cümle)

Üç repo (ECommerceAgent + AgentPlatform + PaymentGateway) **tek k3s cluster**'ına (ayrı namespace),
**GitHub Actions push-tabanlı CD** ile deploy edilir: master merge → image (Aspire `aspire do push` → GHCR
`:latest`) → **SSH ile sunucuda `kubectl apply` + app rollout**. Dış erişim traefik ingress + **sslip.io DNS**
+ Let's Encrypt TLS; dış AI istemcisi (Claude Desktop connector) platform MCP facade'ına OAuth'la bağlanır.

## Dert (kullanıcının niyeti)

Hedef: mağazayı herhangi cihazdan (telefon dahil) Claude Desktop connector'la tek noktadan yönetmek + CI/CD'yi
**öğrenerek** kurmak. Soru zinciri: nerede koşsun (k8s mı), image nasıl üretilsin, CD nasıl tetiklensin,
domain/TLS nasıl, altyapı (DB/broker) nerede dursun.

## Kararlar + gerekçeler

### 1. k3s (tam k8s değil), tek-node
Tam k8s kurulumu ağır (ayrı control-plane parçaları, etcd, ~2GB+). k3s = tek-binary, prod-grade, pilleri
dahil (containerd + traefik + coredns + local-path). Tek-node VDS + öğrenme için isabet; "yerel-kind glue
acısı" (eski 004 dersi) elenir. Büyük/çok-node gerekirse k3s yükseltilir.

### 2. Postgres + RabbitMQ **in-cluster** — künye hedefinden BİLİNÇLİ SAPMA
`CLAUDE.md`/künye hedefi: Postgres + RabbitMQ **cluster-DIŞI** (managed). İlk canlı/öğrenme adımında
managed provision yok → **in-cluster, ephemeral** (PVC yok) kondu. **Kabul edilen borç:** restart'ta veri
gider. Prod yolu: dışarı taşı (managed) + PVC. Sapma dev-seviyesinde kasıtlı, prod'da geri alınır.

### 3. Push-tabanlı CD, **SSH yolu** (GitOps/ArgoCD değil; 6443 açma)
CD modeli iki seçenek: push (Actions→cluster) vs pull (GitOps/ArgoCD). Öğrenme + tek cluster için **push**
en az yeni parçayla kavramı öğretir. Runner cluster'a nasıl ulaşır? k3s API (6443) **internete kapalı**
(ufw). İki yol: 6443'ü aç (API dışarı, risk) **ya da** port 22'den **SSH** edip `kubectl`'i sunucuda koştur.
**SSH seçildi** — yeni kapı açmaz, elle yapılan adımın birebir otomatiği. `DEPLOY_SSH_KEY` repo secret.
GitOps (ArgoCD) ileri/prod yolu olarak açık bırakıldı.

### 4. Image: Aspire `aspire do push` + kararlı `:latest`
Dockerfile elle YOK — Aspire AppHost grafiğinden image üretir (`aspire do push` → GHCR). Aspire tag'i
zaman-damgalı (değişken) → CD'nin sabit hedefi için her image ayrıca **`:latest`** retag+push; manifest
`:latest` + `imagePullPolicy: Always` + `rollout restart` ile yeni kodu çeker. GHCR push `GITHUB_TOKEN`
ile değil **`write:packages` PAT** (`GHCR_PAT`) ile (user-namespace `permission_denied: write_package`).

### 5. DNS: **sslip.io** (domain yerine)
Hedef domain'ler ölüydü (`hasancagri.com` kayıtsız, `hasancagridemiriz.com` redemptionPeriod). Domain
satın alma/yenileme beklemeden `mcp.<IP>.sslip.io` sihirli DNS → IP'ye çözülür, Let's Encrypt trusted cert
verir, connector bağlanır. Gerçek domain gelince yalnız host adı değişir (ingress + issuer + IdentityOption
+ connector URL).

## Sonuçlar (zorunlu kılınan konvansiyonlar — bkz. conventions.md)

Bu topoloji iki taşınabilir infra kuralını ZORUNLU kılar (yoksa sessiz kırılır):

- **ForwardedHeaders:** ingress TLS'i kenarda sonlandırıp backend'e http konuşur → .NET app isteği http
  sanar → `SameSite=None;Secure` cookie + antiforgery + OIDC redirect/issuer bozulur (**connector login 400**).
  Her servis pipeline başına `UseForwardedHeaders` (KnownProxies/Networks temizli — k8s proxy IP dinamik).
  As-built: AgentPlatform `Identity.Server/Program.cs`. İdeali: `ServiceDefaults` (tek yer, tüm servisler).
- **Health endpoint prod'da açık:** k8s probe'ları `/health`'i yoklar; `ServiceDefaults` IsDevelopment guard
  prod'da kapatıyordu → probe konulamaz. Prod'da aç ya da probe'u bilinçli atla (dev'de probe-suz koşuldu).
- **Marten `ApplyAllDatabaseChangesOnStartup` + rolling update:** migration'lar geriye-uyumlu (expand-contract).
- **env-drift:** CD `kubectl apply` ile canlı `kubectl set env` field-owner çakışırsa apply env'i zorlayamaz
  → `kubectl apply --server-side --force-conflicts` (manifest = kesin gerçek). Sır değerleri manifest'e
  gömülmez: `secretKeyRef` / `imagePullSecrets` (cluster'da bir kez kurulan Secret'a işaret).

## Alternatifler (neden seçilmedi)
- **Aspir8 (aspirate):** .NET 10 `project.v1`'i tanımıyor → native `aspire publish`/`do push` seçildi.
- **GitOps (ArgoCD):** daha temiz prod yolu ama bir bileşen daha; öğrenme için push-SSH önce (ileri kademe).
- **6443'ü internete açmak:** API yüzeyi dışarı → SSH tüneli yerine tercih edilmedi.

## Durum
Kabul (as-built, dev/öğrenme seviyesi). Prod cilası: managed DB + PVC + gerçek domain + GitOps + gerçek sırlar.
