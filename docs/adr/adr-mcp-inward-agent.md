---
status: Kabul
---

# ADR: MCP Geri Geliyor — Ama Yalnız First-Party Agent-İçi Taşıma (014)

> **Gerekçe katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + spec `014-scout-agent-mcp`.
> İlgili: [adr-external-agent-oauth-061](adr-external-agent-oauth-061.md) (dış-agent/DCR dünyası —
> HÂLÂ kapalı; bu ADR onu diriltmez).

**Durum:** Kabul · **Feature:** 014-scout-agent-mcp · **Yumuşattığı kural:** CLAUDE.md "Yapma listesi"
— "MCP yüzeyini parça parça geri getirme" (temiz-sayfa sökümü, 2026-10-30)

---

## 1. Karar — tek cümle

**MCP, ECommerce içinde geri geliyor — ama YALNIZ first-party ScoutAgent'ın BC query tool'larını
tükettiği agent-içi taşıma olarak.** BC'ler query-only `/mcp` ucu açar (`search_books`, `get_basket`,
`get_my_orders`…); yalnız kendi ScoutAgent'ımız client olarak bağlanır. Dış/BYO agent yüzeyi, DCR,
consent, PRM keşfi, operatör DLQ tool'ları GERİ GELMEZ.

## 2. Neden "MCP geri getirme" kuralını yumuşatıyoruz

Temiz-sayfa sökümü (2026-10-30) MCP'yi tamamen kaldırdı çünkü o günkü yüzey **dış BYO-agent**'tı
(müşteri kendi AI istemcisiyle platforma bağlanır). O duruş terk edildi; yüzey first-party React +
kendi sohbet agent'ımıza döndü. Araya giren gerçek:

- **ScoutAgent (Kâşif) kararı:** Keşif = sohbet. Kullanıcı "Sabahattin Ali'nin kitaplarını listeler
  misin" der; LLM niyeti anlar, doğru BC'nin aracını seçer. Bu, **dinamik tool keşfi + LLM seçimi**
  gerektirir — agent'ı sıradan orkestratör servisten ayıran tam da budur.
- **Kullanıcı kararı:** Agent BC'lere REST/gRPC ile DEĞİL, yalnız MCP ile erişsin. "Diğer türlü
  agent'ın diğer servislerden farkı olmaz."

## 3. Neden bu anayasa ihlali DEĞİL

Anayasa **İlke I** context-arası meşru kanalları sayarken MCP'yi AÇIKÇA içerir: "integration event +
**MCP** + sanksiyonlu gRPC". conventions.md da: "**MCP yalnız agent tüketir**; agent-olmayan kod →
REST/gRPC". Yani MCP'nin yaşadığı yer tam da first-party agent'tır. Sökülen şey anayasa ilkesi değil,
bir operasyonel yüzey tercihiydi (dış BYO). Bu ADR o tercihi — agent geri geldiği için — geri alır;
ilke zaten MCP'yi kutsuyordu.

## 4. Sınırlar (geri GELMEYENLER)

- Dış/BYO agent bağlantısı, DCR istemci kaydı, consent ekranı, PRM/`.well-known` keşfi.
- Operatör DLQ MCP tool'ları (inceleme/replay elle sürer).
- **Yazma MCP tool'u YOK** — tüm agent tool'ları query-only; yaz (sepete-ekle/sipariş/yorum/adres/
  arama-geçmişi-silme) REST+UI'da (keşif=sohbet, aksiyon=tık).
- Admin query MCP yüzeyi.

## 5. Sonuçlar

- Her BC query-only `/mcp` açar (`AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` +
  `MapMcp("/mcp")`); tool'lar `Features/Agents/Queries/*.cs` içinde `[McpServerToolType]`.
- ScoutAgent config-güdümlü uç listesiyle bağlanır (`McpEndpointsOptions`); kişisel uçta kullanıcı
  token'ı taşınır (gerçek yetki sınırı uçta). Anon araç kutusu kişisel tool'ları görmez.
- Sır (OpenAI key) insan-yüzeyde/log'da görünmez (İlke VIII korunur).
- MCP tekrar genişlerse (dış yüzey vb.) yeni bilinçli karar + ADR gerekir — bu ADR yalnız agent-içi
  query taşımasını açar.
