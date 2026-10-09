---
status: Kabul
---

# ADR: MCP Kontrol Düzlemidir — Hassas Veri Tool Dönüşüne / LLM'e Girmez

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md`. As-built örnek:
> `src/services/payment/Payment.Api/Grpc/MerchantKeyClient.cs` (gRPC ile key çeker, agent'a DÖNMEZ),
> `src/others/Shared/McpToolDescriptions.cs` ("MerchantId/MerchantKey'i sohbetten isteme ve asla göster").
> İlgili: [adr-credential-link-forward-protection](adr-credential-link-forward-protection.md) (078 — bu ilkenin bir örneği),
> [adr-external-agent-oauth-061](adr-external-agent-oauth-061.md).
> S4 politikası bu ADR'ye dayanır: `specs/087-merchant-id-visibility/spec.md` FR-001.

## Karar (tek cümle)

MCP **tetikler, veriyi taşımaz**: tool handler sunucuda koşar, hassas veriyi (sır + PII) gRPC/DB
ile çekip kullanabilir ama Claude'a **yalnız sır-free sonuç** (status/link/opak handle) döner.
Hassas byte'lar veri düzleminde (S2S + hosted ekran) kalır; LLM sır-olmayan tutamaçla çalışır.

## Dert (kullanıcının niyeti)

Sistem agent-only / BYO-agent — **başlangıç noktası MCP**. Haklı soru: "Her şey MCP'den başlıyorsa
merchant bilgisi (MerchantId/MerchantKey) ister istemez LLM'e gelmez mi?" Cevap: **gelmez** — veri
LLM'e ancak tool **geri dönüşünde** o veriyi döndürürse gelir. Döndürmezsek gelmez.

## İki düzlem

- **Kontrol düzlemi (MCP / LLM):** "ne yapılacak" der. Komut verir, orkestra eder.
- **Veri düzlemi (sunucu + hosted ekran + S2S gRPC/REST):** hassas byte'ları taşır. LLM burada YOK.

LLM orkestra şefi — kargoyu taşımaz. Tool handler LLM'in İÇİNDE değil, **sunucuda** çalışır; sır
handler'ın bellek kapsamında doğar-ölür, dönüş JSON'una girmezse model onu hiç görmez.

As-built: `MerchantKeyClient` Payment.Api içinde gRPC'yle key'i çeker, `X-Api-Key` header'ına koyar,
PG'ye gider — hiçbir agent'a dönmez. 078 credential-link: MCP tool yalnız `{url, expiresAt}` döner,
credential değil (bkz. [adr-credential-link-forward-protection](adr-credential-link-forward-protection.md)).

## KVKK + güvenlik ayrımı (neden önemli)

Veriyi üçe ayır — hepsi aynı değil:

| Veri | KVKK kişisel veri mi? | Asıl risk |
|---|---|---|
| MerchantKey | Hayır — sır/credential | **Sır sızması** |
| MerchantId | Merchant tüzel kişiyse hayır; şahıs firması sınırda | Düşük |
| Onboarding e-posta/ad | Evet (gerçek kişi) | KVKK tam kapsam |

Asıl bağlayıcı kısıt **KVKK değil, sır hijyeni**: key'i MCP'den döndürmek = LLM + onu barındıran taraf
(Anthropic) düz metin sır görür. PII (e-posta/ad) için ek olarak MCP→LLM→Anthropic = **yurt dışına +
üçüncü-taraf işleyene aktarım** (KVKK'nın en sert yeri). Her iki veri tipi için sonuç aynı: **hassas
veri MCP dönüşünde olmaz.** (KVKK yorumu teknik akıl yürütme — bağlayıcı hukuki görüş danışman işi.)

## Nöbetçi kurallar (invariant — ihlal = sızıntı)

1. **Tool dönüş payload'ı sır/PII-free.** gRPC reply objesini ham döndürme (key alanı sızar) — sır-free
   DTO'ya map'le.
2. **Trace/log sırrı taşımaz** (observability sırrı sızdırmasın).
3. **LLM'e yalnız sır-olmayan tutamaç** — GUID/key değil, e-posta/ad ya da kısa opak token; sistem
   sunucuda Id/key'e çözer (D5 `admin_find_merchant` tam burası).

## Maskeli gösterim gerekiyorsa

- **Sunucu-tutar:** tam değer sayfada YOK; "aç/kopyala" S2S ucundan tek-kullanımlık çeker → gerçek
  koruma (DOM/view-source/screenshot temiz).
- **DOM-gizli:** tam değer sayfada gizli alanda, maske yalnız görsel → **kozmetik, yasak** (aldatıcı
  güven verir). Sır maskelemek tek başına tiyatro; gerçek kapı reveal-once + kopyala aksiyonudur.

## Durum

**Accepted** — kod bu invariant'ı zaten taşıyor (078 hosted ekran + `MerchantKeyClient` gRPC + tool
description yasakları). ADR ilkeyi açık yazar ki her yeni merchant/sır-gösteren yüzey aynı kurala
uysun. S4 (`087-merchant-id-visibility`) bu ADR'nin somut uygulaması: insan-yüzeyde maskeli, LLM-
yüzeyde hiç yok, tam değer sunucu-tutar + S2S/hosted-ekran.
