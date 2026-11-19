# Contracts: ScoutAgent Router → Sub-Agent

## HTTP yüzeyi — DEĞİŞMEZ
Dış kontrat aynen kalır (React/gateway etkilenmez):
- `POST /api/chat/v1/messages` — istek `{ conversationId?, message, history? }`, yanıt
  `{ conversationId, title?, reply: { text, items[] } }`. **İmza + şekil değişmez.**
- `GET /conversations*`, `PATCH/DELETE /conversations/{id}` — değişmez.

`reply.items[]` = `{ tool, result }` (result yapılı JSON) — bugünküyle aynı; artık yaprak MCP tool
sonuçları sub-agent'lar içinden toplanır (sıra = çağrı sırası).

## İç kontrat — sub-agent registry (config hizası)
`SubAgentDefinition.Key` ↔ `McpEndpointsOptions.Endpoints[].Name` birebir hizalı olmalı. Yeni BC
eklenince: (1) `McpEndpointsOptions`'a uç satırı, (2) `SubAgentDefinitions`'a kayıt — ikisi aynı Key.

## Config şeması (`ScoutOptions`)
```
ScoutOptions:
  Model: "gpt-4o-mini"          # genel varsayılan (mevcut)
  RouterModel: "gpt-4o-mini"    # YENİ — niyet-dağıtıcı
  Agents:                       # YENİ — BC → model (eksik = Model)
    catalog: "gpt-4o-mini"
    library: "gpt-4o"
    # ...
```
Bağlama: `AddOptions<ScoutOptions>().BindConfiguration(nameof(ScoutOptions))` (mevcut). IConfiguration
doğrudan okunmaz.

## MCP tool kontratları — DEĞİŞMEZ
BC tarafı `search_books`/`get_book`/`get_book_reviews`/`get_basket`/`get_my_orders`/`get_my_library`/
`get_my_addresses`/`get_search_history` imzaları + dönüşleri aynen. Bu feature yalnız tüketici tarafını
(agent-içi yapı) değiştirir.
