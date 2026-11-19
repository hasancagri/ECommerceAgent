# Phase 1 Data Model: ScoutAgent Router → Sub-Agent

**Kalıcı aggregate/tablo/şema YOK.** Bu feature Marten'e hiçbir şey eklemez; `Conversation` (scoutDb)
değişmez. Aşağıdakiler **bellek-içi yapı + yapılandırma** — domain aggregate'i değil (İlke II dışı).

## SubAgentDefinition (bellek-içi kayıt)
BC-başına bir sub-agent'ı tanımlar. `SubAgentDefinitions.cs` holder'ında statik liste (7 kayıt).

| Alan | Tip | Açıklama |
|---|---|---|
| Key | string | BC anahtarı (`catalog`, `reviews`, `basket`, `order`, `library`, `customer`, `personalization`) — MCP endpoint adıyla eşleşir |
| McpEndpointName | string | Bağlanılacak TEK `/mcp` ucu (`McpEndpointsOptions.Endpoints` adıyla hizalı) |
| ModelKey | string? | Bu sub-agent'ın model anahtarı; null → `ScoutOptions.Model` varsayılan |
| Instructions | string | Dar, alana özel system prompt (dev "Kasif" prompt'u parçalanır) |
| ToolName | string | Router'a `AsAIFunction` ile sunulan tool adı (ör. `catalog_agent`) |
| ToolDescription | string | Router LLM'inin bu sub-agent'ı seçmesi için açıklama (niyet sinyali) |

**İnvariant**: Key ↔ McpEndpointName birebir; her Key tekil. Tanım statik (Create-only, runtime değişmez).

## ScoutOptions eklentileri (yapılandırma — Options pattern)
| Alan | Tip | Varsayılan | Açıklama |
|---|---|---|---|
| RouterModel | string | `Model` | Niyet-dağıtıcının modeli (ucuz/hızlı) |
| Agents | Dictionary<string,string> | boş | BC anahtarı → model adı; eksik anahtar → `Model` |

(Mevcut `Model` = genel varsayılan; `ApiKey`, rate-limit alanları değişmez.)

## ToolResults — tool-başına ayna-tipler (019 B)
MCP tool sonucunun ScoutAgent tarafındaki tipli yansıması (BC tipine erişilmez → şekil kopyalanır,
sözleşme = AD; bilinçli tekrar). **Tool başına** (sub-agent başına değil — bir sub-agent çok tool
çağırır, şekil tool'a bağlı: liste/tekil). 8 tip: `SearchBooksResult`(liste) · `GetBookResult`(tekil) ·
`GetBookReviewsResult`(liste+ort) · `GetBasketResult`(tekil-aggregate) · `GetMyOrdersResult`(liste) ·
`GetMyLibraryResult`(liste) · `GetMyAddressesResult`(liste) · `GetSearchHistoryResult`(liste). `ByTool`
haritası `toolName→Type`; `ToTyped` ham iç JSON'u deserialize eder (case-insensitive; bilinmeyen → ham
JsonElement fallback). `reply.items[].Result` artık bu tipli nesne.

## ToolResultCollector (bellek-içi, KORUNUR)
Değişmez — her sohbet turu için `(Tool, Result)` listesi. Artık sub-agent'ların MCP tool'ları
(CapturingFunction ile sarılı) buraya yazar; router seviyesindeki sub-agent-tool çağrıları
yakalanmaz (yalnız yaprak MCP tool sonuçları `items`'a girer). Sıra = çağrı sırası (D5).

## Durum geçişi
Yok — tüm bu yapılar durumsuz/statik. Tek kalıcı durum (`Conversation`) bu feature'da değişmez.
