# Research: MCP Query-Only Dönüşü + ScoutAgent

## 1. MCP server hosting (her BC kendi uçunu açar)

**Karar:** Her BC `ModelContextProtocol.AspNetCore` ile kendi HTTP MCP ucunu host eder:
`AddMcpServer().WithHttpTransport().WithToolsFromAssembly()` + `app.MapMcp("/mcp")`. Tool'lar
`[McpServerTool]`+`[Description]` metotları (slice içinde). ScoutAgent bu uçlara MCP CLIENT olarak bağlanır.

**Gerekçe:** FR-005a — araya fasat/keşif katmanı YOK; ScoutAgent 4 uca doğrudan bağlanır (service
discovery statik liste). Eski MCP gateway fasadı (090 downstream + DiscoveryTokenSource) geri gelmez.

**Alternatif (red):** tek merkezi MCP server tüm BC'leri proxy'ler → BC izolasyonu bozar, eski fasat acısı.

## 2. Tool yerleşimi (konvansiyon dirilir)

**Karar:** Tool sarmalayıcısı `Domains/<Agg>/Features/Agents/Queries/<Name>.cs`'te slice'la AYNI dosyada
(`[McpServerToolType]`, conventions.md VSA kuralı). Tool ince: `IQuerySession`'dan okur ya da
`IMessageBus` ile query slice'ını çağırır; iş mantığı eklemez (İlke III).

**Gerekçe:** conventions.md "MCP tool wrapper slice'la aynı dosyada" kuralı tam bu senaryo için yazılmış;
`WithToolsFromAssembly` konumdan bağımsız tarar.

## 3. Kullanıcı token'ını MCP'ye taşıma (kritik)

**Karar:** ScoutAgent Singleton; kullanıcı token'ı ÇAĞRI ANINDA taşınır. MCP client'ın HTTP transport'una
`DelegatingHandler` takılır — `IHttpContextAccessor`'dan mevcut isteğin bearer token'ını okuyup downstream
MCP çağrısına `Authorization` header'ı olarak ekler (mevcut `SagaTokenHandler` deseninin kullanıcı-token
muadili; makine token değil, isteğin kendi token'ı).

**Gerekçe:** orchestrator-agent-singleton kuralı (agent Singleton, per-user = token invocation'da). Anonim
istekte token yok → yalnız anonim tool'lar çağrılabilir.

**Alternatif (red):** per-request McpClient kurmak → bağlantı maliyeti + MAF agent Singleton'la çelişir.

## 4. İki katman girişsiz koruma

**Karar:** (a) ScoutAgent anonim oturumda kişisel tool'ları LLM'in araç setine KOYMAZ (toolset gating —
giriş durumuna göre filtrelenir); (b) BC MCP ucu `.RequireAuthorization(scope)` ile korunur (gerçek sınır).

**Gerekçe:** FR-005. (a) deneyim (model "giriş gerekli" der, 403 görmez), (b) güvenlik backstop.

## 5. Agent çerçevesi + model

**Karar:** Microsoft Agent Framework 1.x (Nisan 2026 stable; MCP + IChatClient native). Model = yerel
Ollama, IChatClient üstünden. Tool-calling destekli model seçilir (ör. qwen2.5/llama3.1 ailesi — plan
sonrası Aspire'da elle doğrulanır). MCP tool → `McpClient.ListToolsAsync()` → `AITool` → agent'a verilir.

**Gerekçe:** kullanıcı kararı (MAF). Eski alpha `prev_response_id` tuzağı 1.x'te geçmişte kaldı; zaten
hafızayı framework'e değil kendi DB'mize dayandırıyoruz (FR-012).

**Sürüm hizası:** props'ta MAF paketleri karışık (1.13.0 + alpha). Plan görevinde stabil hatta hizalanır;
Order.Api'deki kullanılmayan `A2A`+`Microsoft.Agents.AI.A2A` referansı silinir (ölü, 024 artığı).

## 6. ScoutAgent yerleşimi + deposu

**Karar:** Yeni servis `src/services/scout/ScoutAgent` + kendi Postgres'i (`scoutDb`, Marten) — sohbet
transkripti + özet burada. AppHost'a kayıt (mevcut desen). Gateway'e `/api/chat/**` rotası. React tek
origin kuralı gereği Gateway üzerinden erişir.

**Gerekçe:** İlke I — ScoutAgent kendi verisini (sohbet) kendi DB'sinde tutar; başka BC'ye MCP ile konuşur
(sanksiyonlu kanal: "MCP'yi yalnız agent tüketir" — ScoutAgent agent'tır). Marten tutarlılığı (tek istisna
Identity EF Core; ScoutAgent Marten kalır).

**TUZAK (tr-TR ı):** ScoutAgent Marten belgeleri (`Conversation`) için `DocumentAlias` elle lowercase+I'sız
verilir (`conversation`) — açılış crashloop'u önler (CLAUDE.md tuzağı).

## 7. Hafıza katmanları

**Karar:** (1) pencere: son N mesaj ham; (2) rolling summary: eskiler tek özet bloğuna damıtılır (model
kendi özetler, her X mesajda); (3) arşiv: tam transkript DB'de. Modele giden bağlam = `[özet] + [son N]`.
Ollama `num_ctx` modelin kaldırdığı kadar açılır (varsayılan küçük). Tool sonuçları bağlama ham gömülmez.

**Gerekçe:** FR-011. Context builder saf fonksiyon (transkript → kırpılmış bağlam) → İlke VI test-first adayı.

**Alternatif (red):** her mesajda tam transkript → küçük modelin bağlam penceresi dolar, yavaşlar/sapıtır.

## 8. Cevap biçimi

**Karar:** Cevap = `{ text, items[] }` — `text` modelin cümlesi, `items` tool dönüşünden BİREBİR (kitap/
adres/sipariş). React `items`'ı kart olarak çizer. v1 tek-parça (stream yok, FR-013).

**Gerekçe:** FR-006 (uydurma yasak — fiyat/kapak tool'dan). Stream v2 (MAF streaming + SSE ayrı iş).
