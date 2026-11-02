---
status: Öneri
---

# ADR: Credential-Giriş Linki Forward Koruması (kimlik bağlama)

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md`. Üst-ilke: sır tool dönüşünde
> yok → [adr-mcp-control-plane-no-secret-return](adr-mcp-control-plane-no-secret-return.md) (bu ADR onun bir örneği).
> İlgili: [adr-external-agent-oauth-061](adr-external-agent-oauth-061.md).
> Not: key-handoff makineye kaydedildikten sonra bu ADR'nin sub-kıyası/forward koruması
> insan-yüzeyli credential formu erişimine hâlâ geçerli.
> ⚠️ Not (087): elle credential-giriş ekranı `/merchant-credentials/{token}` + `CredentialEntrySession`/
> `SubmitMerchantCredentials` SÖKÜLDÜ (makine-handoff onboarding). Aşağısı o ekran döneminin tarihsel
> gerekçesi; forward-koruma ilkesi insan-yüzeyli herhangi bir credential formuna uygulanabilir kalır.

## Karar (tek cümle)

Merchant credential-giriş ekranı (`/merchant-credentials/{token}`) forward edilse bile **yanlış
kullanıcıda açılmasın**: link tek başına yetki OLMAKTAN çıkar — ekran açılırken **login zorunlu**,
sunucu giren kişinin `sub`'ını linkin sahibi `RequestedByUserId` ile kıyaslar; eşleşmezse nötr 404.
Yeni Razor app YOK — mevcut gömülü HTML ekran korunur.

## Dert (kullanıcının niyeti)

"Bana gelen linki başkasına verdim diyelim, verdiğim kişide açılmaması gerekir." Sır (MerchantKey)
sohbete giremediği için tarayıcı hop'u şart; ama link bearer capability olunca kime giderse ona
açılıyor. Soru: linki forward edersem yabancı açabilir mi?

## Kök sorun: token = pre-authenticated bearer

Mevcut kod (`CredentialEntryEndpointExtension`): GET/POST **anonim**, yalnız `{token}` ile
`CredentialEntrySession` sorgulanıyor. `RequestedByUserId` saklı AMA açan kişiyle **kıyaslanmıyor**
(yalnız denetim izi). JWT sadece link ÜRETİMİNDE vardı (`AdminRequestCredentialEntryLink` MCP tool'u);
KULLANIMINDA yok. Yani token forward edilirse yabancı açar. Maruziyet penceresi = tüm TTL (GET
tüketmez, yalnız başarılı POST `Consume` eder).

## MCP elicitation spec bunu birebir "Phishing" diye yazmış (2026-07-28)

Mevcut ekran = **elle yapılmış URL-mode elicitation**. Spec normatif:

- Sır (password/API key/token/payment) için **form mode YASAK, URL mode ZORUNLU** (veri client/LLM'den
  geçmez). "Hiç tarayıcı" hayali sır için ölür.
- Spec'in "Phishing" senaryosu = bu forward saldırısı (Alice URL üretir, Bob'a yollar, Bob tamamlar,
  token Alice'e bağlanır = account takeover).
- Zorunlu önlem: *"server **MUST** ensure that the user who started the elicitation request is the same
  user who completes the flow... comparing the `sub` claim from the AS to the subject from the session
  cookie... **MUST** be resilient to attacks where an attacker can modify the elicitation URL."*
- Ayrıca: *"Servers **MUST NOT** provide a URL which is pre-authenticated to access a protected
  resource."* → mevcut capability token bu yasağı ihlal ediyor.

Yani mevcut kod **iki MUST ihlal ediyor**: (1) açılışta `sub` kıyası yok, (2) token = pre-auth bearer.

## Çözüm akışı (update yolu — merchant hesabı VAR)

1. Yabancı linki açar → **login'e yönlendirilir**
2. Kendi kimliğiyle giriş yapar (`sub`)
3. Sunucu kıyaslar: `sub == RequestedByUserId`?
4. Sahibi değil → **nötr 404**. Link seni tanımaz, oturum tanır.

Link yetki değil; **login yetki**. Başkasının linki + senin login'in = uyuşmaz = bloke.

## "Tekrar login mi?" — hayır, sessiz SSO

Claude Desktop MCP OAuth'u ([adr-external-agent-oauth-061](adr-external-agent-oauth-061.md)) bağlanırken tarayıcıda IdP login yaptı
→ **oturum cookie'si** bıraktı. Credential ekranı **aynı tarayıcıda** açılınca OIDC redirect → IdP
cookie'yi görür → **sessiz SSO** (parola sormaz). Koşul: aynı tarayıcı + cookie süresi geçmemiş.
Tutmazsa tek sefer login. Claude Desktop token'ı ekranda DOĞRUDAN kullanılamaz (ayrı kanal, ayrı
depo `~/.mcp-auth`); kimlik tarayıcı oturumundan gelir, Desktop token'ından değil.

## Onboarding çatalı (hesap YOK → oturum yok)

İlk onboarding'de merchant'ın hesabı olmayabilir → bağlanacak `sub` yok. Spec bunu kabul eder:
*"the server may not be able to use a session cookie... must use a different mechanism... resilient to
URL modification."* → oraya **OTP** (doğrulanmış mail/SMS kod) + kısa TTL + tek-kullanım. Link
teslimi de chat yerine **doğrulanmış mail** (KVKK: sır/link LLM transkriptine düşmez).

## Native elicitation'a tam geçiş — şimdilik DEĞİL

Native URL-mode elicitation client'ın (Claude Desktop) hop'u sürmesini sağlar (consent + domain
gösterimi + güvenli açma). Ama Claude Desktop desteği **buggy** (issue #1046: URL-mode print-mode'da
düşüyor, tool call 180s hang). Claude Code CLI destekliyor. Karar: **elle link kısa vadede korunur**,
üstüne iki MUST (sub kıyası + token'ı bearer'dan korelasyon-handle'a indir) eklenir; native geçiş
Desktop desteği stabilleşince.

## Vizyona uyum (tek MCP / agent-only)

İhlal DEĞİL. MCP zaten OAuth için tarayıcıya sıçrıyor; credential hop'u aynı desen. "Tek MCP" =
tek giriş + dil işlemleri sohbette; sır/binary hiç dil değildi. MCP her adımı orkestra eder, dil-
olmayan tek adımı güvenli hop'a devreder, döner. Düz merchant alanları (ad/adres) sohbette MCP
tool ile; yalnız sır ekranda.

## Durum

**Proposed** — kod henüz kıyası yapmıyor (forward gap açık). İş kalemi: `tech-debt` S4 komşusu.
Uygulanınca `CredentialEntryEndpointExtension` GET/POST `.RequireAuthorization()` + handler'da
`sub == RequestedByUserId` + onboarding OTP. Ek borç: MerchantKey at-rest şifreleme (düz string mi
doğrula), `AdminReissueMerchantKey`'in yeni key'i nereye kalıcı yazdığı belirsizliği.
