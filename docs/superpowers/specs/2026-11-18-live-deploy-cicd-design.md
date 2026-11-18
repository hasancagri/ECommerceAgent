# Tasarım: EC + Web Canlı — Push-CD Yeniden Hizalama

**Tarih:** 2026-11-18 · **Durum:** onay bekliyor · **Boyut:** proje

## Niyet + neden

Kullanıcı yaptığı geliştirmeleri **canlı ortamda görmek** istiyor. Asıl talep tek-seferlik
deploy değil: **push-CD'yi bir kez oturtmak** — master'a her merge sonrası yeni kod otomatik
canlıya aksın, bunun için ileride ekstra kod yazılmasın. İkincil hedef CI/CD'yi **öğrenerek**
kurmak (öğrenci modu; manifest'ler elle, şeffaf).

Cluster + push-SSH CD hattı 2026-10-12'de kuruldu (ADR `adr-k3s-push-cd-deployment`). Ama o
günden sonra (temiz-sayfa teardown 2026-10-30, ParadeDB arama, identity-içeri, scout-agent)
mimari değişti. Bugünkü CD artefaktları **hayalet topolojiyi** deploy eder → iş = yeniden hizalama.

## Kapsam

Canlıya yalnız **iki sistem**: `ECommerceAgent` (backend + içindeki Identity.Server) +
`ECommerceWeb` (React frontend). Cluster'daki diğerleri (`agentplatform`, `paymentgateway`,
BankSimulator) **kaldırılır**.

Mobil ayrı bir hat — bu belgenin DIŞINDA (CI/CD oturduktan sonra ayrı brainstorm).

## Hedefler

- `master` merge → EC servisleri otomatik canlıya (mevcut push-SSH CD yolu onarılmış).
- `ECommerceWeb` canlıda servis edilir (şu an hiç deploy edilmemiş, local-only).
- Canlı app **gerçekten çalışır**: arama, sohbet (scout-agent), login (Google dahil), kütüphane,
  yorumlar, kapak görselleri. Tüm sırlar gerçek bağlı.
- Eski uygulamalar canlıdan sökülür.

## Hedef-değil

- Mobil uygulama (ayrı hat).
- GitOps/ArgoCD (B yolu) — şimdilik değil; ileride bilinçli upgrade.
- Kurumsal SLA / çok-node / managed DB — dev/öğrenme seviyesi kabul.
- Ödeme akışının uçtan-uca tamamlanması (PaymentGateway kaldırıldığı için — bkz. Kısıtlar).

## Anti-hedef (teknik çalışsa bile başarısızlık sayılır)

- Deploy "yeşil" ama app boot olmuyor (ör. catalog ParadeDB'siz pg_search bulamaz).
- Her cluster restart'ında 22k katalog + kullanıcı verisi uçuyor (ephemeral tuzağı).
- Sır değerleri manifest'e/git'e gömülü sızıyor (İlke VIII ihlali).

## Kısıtlar

- **k3s tek-node, API (6443) internete KAPALI** → CD runner port 22'den SSH, `kubectl`'i sunucuda koşar.
- **Dockerfile yok** — image'lar Aspire (`aspire do push`) ile AppHost grafiğinden üretilir.
- **AppHost zaten güncel** (ParadeDB `paradedb/paradedb:0.26.6-pg17`, ES yok, identity-server,
  scout-agent, gateway). Yani image tarafı doğru; bayat olan ci.yml retag listesi + `deploy/k8s` manifest'leri.
- **ServiceDefaults** ForwardedHeaders'ı kuruyor (✓) ama `/health` prod'da `IsDevelopment` guard'ı
  arkasında KAPALI → k8s probe'ları fail eder. Prod'da açılmalı.
- **ECommerceWeb ayrı repo** — kendi CI/image/manifest'i gerekir; API base URL'i canlı gateway host'una bakmalı.
- **Sır değerleri kullanıcıdan** — OpenAI, R2 erişim anahtarı, Google OAuth client+redirect, identity
  signing/admin, gateway admin key, GHCR_PAT, DEPLOY_SSH_KEY. Kod bunları üretemez.

## Değerlendirilen yollar

### Deploy mekanizması
- **A — GitHub Actions push-CD (SSH)** ✅ SEÇİLDİ. Zaten as-built; master merge → `aspire do push`
  → GHCR `:latest` → SSH → `kubectl apply` + `rollout restart`. En az yeni parça, VPS hafif.
- **B — GitOps (ArgoCD/Flux)** — modern CD altın standardı, öğretici; ama tek-node k3s'e ek yük,
  kurulum ağır. İleri kademe upgrade olarak açık.
- **C — Elle script** — reddedildi; "gelecekte kod yok" isteğini bozar.

### Web servis etme
- **(a)** ✅ SEÇİLDİ. Ayrı nginx container (React build) + ingress path-split: `/api`·`/files`·
  `/connect`(identity) → gateway/identity; `/` → web. Repo'lar ayrı, image'lar ayrı, CD bağımsız.
- **(b)** Gateway static serve (React gateway image'ına gömülü) — tek container ama repo'ları birbirine bağlar.

### Manifest kaynağı (yeni karar)
- **Elle `deploy/k8s/*` (koru + yeniden yaz)** ✅ ÖNERİ (benim çağrım). CD zaten bunu uyguluyor;
  ingress/TLS/PVC/secret özelleştirmesi aspire-üretimi manifest'te zor; öğrenme için şeffaf.
- Aspire-üretimi `k8s-artifacts/` — verbose/opak, ingress/secret yine elle; reddedildi.

## Kararlar (kilitli + benim çağrılarım)

**Kullanıcı kilitledi:** Yol A · Web (a) · tüm sırlar gerçek · yalnız EC+Web canlı · diğerleri sök.

**Benim çağrılarım (builder-seviyesi, kullanıcı veto edebilir):**

1. **Manifest = elle `deploy/k8s`, tam yeniden yazım.** Bugünkü topoloji: altyapı (ParadeDB-postgres +
   rabbitmq + redis; ES YOK) + 13 EC servisi (catalog/basket/order/payment/customer/reviews/library/
   stock/discount/file/personalization/scout-agent/gateway) + identity-server + web (nginx). 12 DB
   ParadeDB init'inde; catalogDb'de `CREATE EXTENSION pg_search, vector`.
2. **ci.yml onarımı:** ölü `Platform.Auth` GitHub Packages feed'i kaldır (artık kullanılmıyor);
   retag servis listesini güncelle (eski checkout-orchestrator/mail-mcp/notification-agent/storefront
   çıkar; identity-server/scout-agent/personalization-api/stock-api ekle); rollout grep'ini `elasticsearch`'süz.
3. **Health prod'da aç** — ServiceDefaults `IsDevelopment` guard'ını kaldır/gevşet; probe'lar çalışsın.
4. **Kalıcılık = PVC** (ephemeral değil). 22k katalog + kullanıcı/sohbet verisi restart'ta uçmasın:
   Postgres'e PVC (local-path), RabbitMQ'ya PVC. ADR'nin "ephemeral dev borcu"nu burada kapatıyoruz
   çünkü "canlıda gör" kalıcılık ister.
5. **DNS/TLS = sslip.io sürdür** (`*.91.132.49.70.sslip.io` + Let's Encrypt), gerçek domain yoksa.
   Web `app.<ip>.sslip.io`, gateway/api aynı host altında path, identity `id.<ip>.sslip.io`.
6. **Web CI bağımsız:** ECommerceWeb repo'suna kendi `ci.yml` (nginx image build → GHCR → SSH deploy
   kendi manifest'i) + `Dockerfile` (multi-stage: node build → nginx). API base URL build-arg/env =
   canlı gateway host.
7. **Sır teslimi:** hepsi cluster'da `kubectl create secret` ile bir kez (manifest `secretKeyRef`/
   `imagePullSecrets`); git'e/manifest'e değer girmez. Google OAuth redirect URI canlı identity host'u
   içermeli (Google Console — kullanıcı tarafı).

## Builder-check sonrası netleşen (benim çağrılarım)

8. **OIDC issuer/redirect sözleşmesi builder kapsamında.** Issuer `localhost:5001` → canlı
   `id.<ip>.sslip.io`; her servisin `IdentityOption.Address` + `react-web` client redirect/post-logout
   (`localhost:5173` → canlı web host) + CORS tek seferde yeniden yazılır. Hepsi aynı issuer string'inde
   anlaşmazsa auth sessizce 400/401. Host-şeması zaten kilitli (karar 5), kalan mekanik rewrite.
9. **Init idempotent olmalı (PVC gerilimi).** PVC kalıcı + mevcut init non-idempotent → ikinci rollout
   init'i koşmaz, yarım DB yeşil-ama-bozuk boot eder (anti-hedef). Çözüm: initContainer/Job guard'lı
   (DB/extension VARSA atla); `CREATE EXTENSION IF NOT EXISTS pg_search, vector` catalogDb'de.
10. **replicas: 1 her serviste.** Tek-node dev + scout-agent Wolverine in-proc (local queue dayanıklılığı
    + sohbet state tek-pod varsayar) → HA yok, çoğaltma yok. Kabul (dev seviyesi).
11. **Taze başlangıç, veri taşıma yok.** Eski cluster verisi eski topolojiden (düz postgres, sökülen
    servisler) → ParadeDB PVC'ye taşınmaz; sıfırdan. Benim çağrım (temiz-sayfa + ephemeral geçmiş).
12. **R2 kapakları pasif.** Kapaklar canlı R2 bucket'ında zaten duruyor (004); `RegistryBackfill` taze
    fileDb'yi idempotent doldurur → kapaklar yeni ingestion olmadan çalışır. (Kullanıcı teyidi bekler.)

## Builder'a bırakılan

- `deploy/k8s` manifest'lerinin tam içeriği (Deployment/Service/ConfigMap/Secret-ref/Ingress/PVC),
  AppHost referans grafiğine birebir (service-discovery env adları, WaitFor sırası = initContainer/probe).
- ParadeDB init script'i (12 DB + catalogDb extension'ları).
- ECommerceWeb `Dockerfile` + nginx config + `ci.yml`.
- Traefik IngressRoute + cert-manager/Let's Encrypt kablosu (as-built'ten uyarlanır).
- ServiceDefaults health guard değişikliği + testler.

## Kullanıcıdan gereken (operasyonel, kod değil)

1. **Sır değerleri:** OpenAI key · R2 access-key-id + secret · Google OAuth client-id + secret ·
   identity signing cert + admin email/parola · gateway admin api key · GHCR_PAT · DEPLOY_SSH_KEY
   (çoğu geçmişte vardı — teyit/yenile).
2. **Google Console:** canlı identity redirect URI kaydı.
3. **Veri tohumu:** ✅ KARAR = **Excel import, canlıya karşı**. `Kitapyurdu_Katalog_v2.xlsx` cluster'a
   ulaştırılır, admin REST iki-faz (dry-run → onay → insert) bir kez koşulur; kapaklar R2'den pasif
   akar. ImportJobWorker + gateway admin key + xlsx erişimi gün-1 çalışmalı.
4. **Diğer repo CD'si:** AgentPlatform + PaymentGateway repo'larının master-push CD'si duruyor → namespace
   silinse bile sonraki push geri kurar. O repo CD'leri durdurulmalı (kullanıcı tarafı).

## Bilinen sınır / sonuç

- **Checkout canlı kapsam DIŞI (karar).** PaymentGateway + BankSimulator kaldırılır; ödeme/checkout
  akışı canlıya alınmaz. Canlı yüzey: browse · arama · sohbet (scout-agent) · login (Google dahil) ·
  kütüphane · yorumlar · kapaklar. Sepet görünse de checkout bağlanmaz. İleride PG dönerse ayrı iş.
- **Hedef-değil'e eklenir:** ödeme/checkout'un canlıda çalışması.
