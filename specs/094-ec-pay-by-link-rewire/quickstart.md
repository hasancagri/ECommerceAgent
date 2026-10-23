# Quickstart / Doğrulama: EC pay-by-link rewire

Uçtan uca doğrulama. Canlı adımlar tüm geliştirme bitince topluca. **İki host** gerekir: PG AppHost +
EC AppHost (ayrı Aspire). Kart yolu NonSecure + SimBank.

## Önkoşul

- PG AppHost ayakta (091 onboarding + 089/090 pay-by-link canlı); en az bir mağaza-merchant **Live**
  (091 quickstart ile aktive edilmiş; MerchantId = EC `PgPaymentLinkOptions.MerchantId`).
- EC AppHost ayakta. **EC Payment.Api `POST /payments/callback` ucu PG'nin
  `StoreCallbackOptions.CallbackUrl` portunda** (dev: `:5080`) — uyumsuzsa PG config'i EC portuna ayarla.
- Secret'lar user-secrets: EC `PgPaymentLinkOptions` (StoreApiKey=`dev-ec-pg-key`,
  CallbackSecret=`dev-sim-callback-secret-0909`, MerchantId=Live merchant, BaseUrl=PG Payment.Api).
- OpenAI vb. gerekmez (ödeme LLM'siz).

## Birim test (domain-TDD, önce)

```bash
dotnet test tests/Payment.Api.Tests/Payment.Api.Tests.csproj
dotnet test tests/Order.Api.Tests/Order.Api.Tests.csproj
```

Beklenen (yeni yeşil):
- `PaymentIntentTests`: `Create` amount≤0 → error; `MarkSucceeded` Pending→Succeeded; tekrar Succeeded →
  no-op Ok (idempotent); Failed'den Succeeded → error; `MarkFailed` benzeri; `AttachPgRef` bir kez.
- `StartPaymentAmountTests` (Order, saf birim): liste−aktif-indirim toplamı; boş sepet → error.
- Mevcut 092/089/090 testleri bozulmaz.

## Build + guard

```bash
dotnet build
scripts/check-flow-links.sh         # order+payment FLOW.md yeni anchor'lar kodda
scripts/check-claude-spec-links.sh
```

## Canlı E2E (Aspire, iki host) — toplu, en sonda

Akış US1→US3 (spec). start_payment MCP (Claude Desktop) ya da doğrudan Order.Api üzerinden.

1. **US1 start (S1)**: Live merchant + dolu sepetle `start_payment` → Pending Order + PaymentIntent
   (Pending, Amount=liste−indirim) + yanıtta **HostedUrl** (PG linki). Yanıtta kart alanı YOK.
2. **US1 öde (S2)**: HostedUrl'i aç → PG hosted kart sayfası → geçerli test kartı (NonSecure) → PG charge.
3. **US1 webhook (S3)**: PG → EC `POST /payments/callback` (status=Paid, imzalı) → PaymentIntent Succeeded
   → `PaymentSucceeded` → 092: StockCommitRequested → StockCommitted → **Order Confirmed** + OrderCompleted
   fanout. Sipariş sorgusu Confirmed; stok düştü.
3b. **başarısızlık**: red kartı → webhook status=Failed → PaymentIntent Failed → `PaymentFailed` → Order
    Cancelled; stok düşmez.
4. **US2 idempotent/imza (S4)**: aynı Paid webhook'unu ikinci kez gönder → Order tek kez Confirmed (no-op).
   İmzasız/yanlış imza → 404, durum değişmez. amount oynatılmış gövde → reddedilir.
5. **US3 satışa-kapalı (S5)**: Live olmayan MerchantId ile start_payment → "mağaza satışa kapalı"
   (STORE_NOT_LIVE), HostedUrl yok, kalıcı Pending Order yok.
6. **Sır taraması (S6, İlke VIII)**: tüm yanıt/log/trace → kart PAN/CVV + CallbackSecret/StoreApiKey %0.

## Sınır (bu dilimde doğrulanmaz)

- 3D-Secure akışı (NonSecure ilk sürüm); gerçek banka; thank-you web sayfası; settlement/komisyon/cüzdan.
- Terk/TTL timer (PaymentIntent.Expire tetiği) — bu dilimde eklenmez.
