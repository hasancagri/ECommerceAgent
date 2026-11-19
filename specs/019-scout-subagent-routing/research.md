# Phase 0 Research: ScoutAgent Router → Sub-Agent

## D1 — Yönlendirme mekanizması: agent-as-tool (`AsAIFunction`)
**Decision**: Router `ChatClientAgent`, her sub-agent'ı MAF `AIAgent.AsAIFunction()` ile *tool* olarak
alır. LLM router hangi sub-agent-tool'u çağıracağını seçer; seçilen sub-agent kendi `RunAsync`'ini
(kendi MCP tool döngüsüyle) çalıştırır.
**Rationale**: MAF 1.13.0'ın idiomatik çoklu-agent deseni; LLM-routing'i korur (014 D3'e sadık, kural-
sınıflandırıcı değil); tool döngüleri iç içe çalışır (router tool-call → sub-agent RunAsync).
**Alternatives**: (a) Workflows + `WorkflowContext` grafı — deterministik ama bu dilim için ağır
(çok-adım/fan-out-merge gerekmiyor); reddedildi, ileride ayrı dilim. (b) Deterministik kural
sınıflandırıcı — 014 D3 "kırılgan, çok-alan zayıf" diye reddetmişti; korunur.
**Risk/Verify**: `AsAIFunction` repoda sıfır kullanım. Implement'in İLK adımı küçük spike: router'ın
sub-agent-AIFunction'ı çağırdığında sub-agent'ın MCP tool döngüsünün gerçekten koştuğunu Aspire'da doğrula.

**BULGU (2026-11-19, spike-by-reasoning)**: `AsAIFunction` önceden kurulmuş bir agent'ı sarar → sub-agent
(ve MCP tool'ları) ROUTER ÇALIŞMADAN kurulur = 7 ucun hepsine eager bağlantı = SC-001 BOZULUR. Düzeltme:
`AsAIFunction` YERİNE **lazy `AIFunctionFactory.Create`** — tool invoke anında (router seçince) MCP ucuna
bağlan + sub-agent'ı kur + çalıştır. Seçilmeyen sub-agent hiç bağlanmaz. D1 "agent-as-tool" KORUNUR,
yalnız mekanizma lazy custom function (ScoutAgentFactory.MakeSubAgentTool). Derleme doğruladı; runtime
iç içe tool döngüsü canlı doğrulanacak (T011).

## D2 — Endpoint-başına MCP tool getirme
**Decision**: `McpToolSource`'a `GetToolsForEndpointAsync(endpointName, collector, ct)` eklenir; her
sub-agent kurulurken yalnız kendi ucunun tool'larını çeker. Bugünkü "tüm uçları paralel topla"
(`GetToolsAsync`) router kurulumunda KULLANILMAZ.
**Rationale**: SC-001 — seçilmeyen uca bağlantı sıfır. Sub-agent kurulurken (router onu çağırınca)
yalnız kendi ucu handshake olur.
**Alternatives**: Kör toplamayı koru (amacı bozar) — reddedildi.
**Not**: Tek `"mcp"` HttpClient + `UserTokenForwardingHandler` aynen (bearer login sub-agent'larda taşınır).

## D3 — Model-keyed `IChatClient` factory
**Decision**: Tek `AddSingleton<IChatClient>` yerine `ChatClientFactory` (model adıyla OpenAI client
üretir + `UseFunctionInvocation` + cache). Router + her sub-agent kendi model anahtarını alır.
**Rationale**: FR-004 agent-başına model. Router ucuz, zor sub-agent güçlü; sağlayıcı karışabilir.
**Alternatives**: Tek paylaşılan client (tek model) — FR-004'ü karşılamaz.
**Config**: `ScoutOptions.RouterModel` + `Agents: { catalog: "...", library: "...", ... }`; atanmamışsa
yapılandırılmış varsayılan (`ScoutOptions.Model`).

## D4 — Sub-agent tipli çıktı (en büyük teknik risk)
**Decision**: Her sub-agent `ChatOptions.ResponseFormat` (JSON şema) ile tipli çıktı hedefler.
**Risk**: MAF 1.13.0'da aynı agent'ta `UseFunctionInvocation` (tool döngüsü) + `ResponseFormat`
(yapısal nihai cevap) + `AsAIFunction` (iç içe) birlikte güvenilir mi — belirsiz, spike gerekir.
**Fallback (D4a)**: Yapısal nihai cevap tool döngüsüyle çakışırsa, sub-agent **düz metin** döner ve
yapı MCP tool'unun kendi `structuredContent`'inden `reply.items`'a akar (bugünkü yakalama deseni —
`items` zaten yapılı; sub-agent metni prose kalır). Kullanıcıya görünür fark yok (SC-002 korunur).
**Karar kuralı**: Spike'ta tipli-çıktı+tool güvenilir çalışırsa D4, değilse D4a. İkisi de SC'leri karşılar.

**UYGULANAN (2026-11-19): B — TOOL-başına tipli yakalama.** Sub-agent LLM'i `RunAsync<T>` KULLANMIYOR
(A reddedildi: kart verisini LLM yeniden yazardı → uydurma riski + token; ayrıca bir sub-agent birden çok
tool çağırır — catalog search=LİSTE + get_book=TEKİL — tek T sığmaz). Yerine: sub-agent düz prose döner
(router'a); kart verisi ham MCP `structuredContent`'ten **tool-başına ayna-tipe** (`ToolResults`, 8 adet)
DETERMİNİSTİK deserialize edilir → `reply.items` artık `JsonElement` DEĞİL **tipli** (liste/tekil tool'un
tipinde çözülü). `toolName→Type` haritası + case-insensitive. Uydurma YOK (FR-005), JsonElement jimnastiği
biter. Saf test: `ToolResultsTests` (liste/tekil/case-insensitive/fallback).

## D5 — Çok-alanlı sonuç birleştirme sırası
**Decision**: Router sub-agent-tool'ları **çağrı sırasıyla** işler; `CapturingFunction` iç MCP tool
sonuçlarını `ToolResultCollector`'a çağrı sırasına göre yazar → `reply.items` deterministik sıralı.
**Rationale**: FR-010 öngörülebilir sıra. Collector zaten sıralı liste (append).
**Not**: Collector her sub-agent'ın MCP tool'larına threadlenmeli (CapturingFunction inner tool'u sarar).

## D6 — 014 "D3" kararının revizyonu
**Decision**: 014 research D3 "tek düz agent + tool description + system prompt" idi. Bu spec onu
**revize eder**: LLM-routing KORUNUR (kural-sınıflandırıcı yine reddedilir) ama tek düz agent →
router+sub-agent. FLOW.md hotspot bunu open olarak taşır; implement sonu closed.
**Rationale**: Kör toplama maliyeti + tool yüzeyi büyüdükçe seçim bozulması + agent-başına model ihtiyacı.

## Özet
Yeni kalıcı veri/şema/event YOK. Tüm değişiklik agent-içi yapı + Options. En büyük belirsizlik D4
(tipli çıktı + tool döngüsü); D4a fallback hazır. İlk implement adımı D1+D4 spike'ı.
