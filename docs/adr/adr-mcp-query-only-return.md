---
status: Kabul
---

# ADR: MCP Query-Only Dönüşü + ScoutAgent (first-party sohbet) — 007

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + `spec 007-mcp-agent`.
> İlgili: [adr-mcp-control-plane-no-secret-return](adr-mcp-control-plane-no-secret-return.md),
> [adr-external-agent-oauth-061](adr-external-agent-oauth-061.md) (dış-agent yüzeyi — GERİ GELMEDİ),
> [adr-bounded-context-per-service](adr-bounded-context-per-service.md).

**Durum:** Kabul edildi (yürürlükte) · **Feature:** 007-mcp-agent

## Bağlam

2026-10-30 temiz-sayfa sökümünde EC'deki tüm `/mcp` uçları + fasat + DLQ tool'ları kaldırıldı; yüzey
"REST first-party" ilan edildi. CLAUDE.md "Yapma listesi" MCP'nin ancak **bilinçli mimari karar +
spec'le** dönebileceğini şart koşmuştu. 007 o karardır: first-party bir sohbet yüzeyi (Kâşif) kitap
keşfini + kullanıcının kendi verisini (adres/sipariş/kitaplık) doğal dille açar; bunun için query-only
MCP geri gelir.

## Karar

1. **MCP query-only geri — YALNIZ first-party.** Her BC kendi `/mcp` ucunu host eder
   (`ModelContextProtocol.AspNetCore`, `WithToolsFromAssembly`); tool'lar `Features/Agents/Queries/`
   slice'ında `[McpServerTool]`. Kapalı liste, hepsi OKUR. Dış-agent/DCR/PRM/consent yüzeyi GERİ GELMEZ
   (061'in dış erişim çizgisi kapalı kalır) — tek tüketici first-party `ScoutAgent`.
2. **Fasat/keşif katmanı YOK (FR-005a).** ScoutAgent 4 uca DOĞRUDAN MCP client'ı olarak bağlanır; uç
   listesi CONFIG-GÜDÜMLÜ (`McpEndpointsOptions` — yeni BC = tek satır). Eski MCP gateway fasadı +
   downstream discovery-token dansı dirilmez.
3. **Kimlik çağrı anında (agent Singleton).** Kullanıcı token'ı `UserTokenForwardingHandler` ile gelen
   istekten downstream kişisel `/mcp` çağrısına taşınır. İki katman: (a) anonim oturumda kişisel tool'lar
   araç kutusuna HİÇ konmaz (UX); (b) kişisel uç `.RequireAuthorization(scope)` ile her istekte doğrular
   (gerçek sınır). Girişsiz hiçbir koşulda kişisel veri DÖNMEZ.
4. **ScoutAgent audience doğrulamaz.** Agent kullanıcının KENDİ (SPA'ya basılmış) token'ını yalnız `sub`
   için okur; issuer+imza+ömür doğrular. Audience servis-başına (library.api…) olduğundan SPA token'ında
   scout.api yoktur; audience+scope zorlaması DOWNSTREAM BC'de kalır. Yeni scope/resource GEREKMEZ.
5. **Yerel model + hafıza.** Yerel Ollama (OllamaSharp native IChatClient — num_ctx gibi Ollama-özel
   ayarlar geçer; bulut LLM yok). Hafıza katmanlı: pencere (son N ham) + rolling summary (eskiler model
   özeti) + tam arşiv `scoutDb`'de. Devamlılık ScoutAgent'ın KENDİ kaydından kurulur — agent çerçevesinin
   conversation/previous-response state'ine GÜVENİLMEZ (write-only tuzağı).
6. **ScoutAgent kendi BC-benzeri servisi** (`src/agents/ScoutAgent.Api`, kendi `scoutDb`'si); başka BC'ye
   tek kanalı MCP (İlke I: "MCP'yi yalnız agent tüketir" — ScoutAgent agent'tır). Sohbet = tek yeni kalıcı
   veri; BC verileri yalnız OKUNUR, değişmez.

## Sonuçlar

- (+) Keşif + kişisel veri tek doğal-dil yüzeyinde; BC izolasyonu MCP sanksiyonlu kanalıyla korunur.
- (+) Yeni MCP ucu eklemek = config satırı (fasatsız, kodsuz).
- (−) Audience doğrulamama bilinçli taviz — gerçek yetki downstream'de; ScoutAgent yalnız kimlik okur.
- Sır dönüşü yasağı (`adr-mcp-control-plane-no-secret-return`) aynen geçerli: dönen veri kullanıcının
  kendi verisi, GUID gizli, credential/PII tool dönüşüne girmez.
