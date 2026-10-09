# Mimari Karar Kayıtları (ADR)

Mimari kararların *gerekçe* katmanı. Bağlayıcı kurallar: `../../CLAUDE.md` +
`../../.specify/memory/constitution.md`. ADR = karar + neden; taşınabilir kural orada yaşar, ADR link verir.

Dev/ajan: bir karar sınırına dayandığında ("neden BC-per-service?", "sır neden ekrana basılmaz?")
gerekçeyi **buradan** grep'le — harici not defterine (vault) gitme.

**Durum vokabüleri:** `Kabul` (yürürlükte) · `Öneri` (henüz uygulanmadı) · `Superseded` (yerine
başka karar geçti) · `Tarihsel` (anlattığı pratik söküldü; kayıt öğretici olduğundan saklanır).

| ADR | Başlık | Durum |
|---|---|---|
| [adr-aop-caching-mechanism](adr-aop-caching-mechanism.md) | AOP Query Caching mekanizması — neden IMessageBus decorator? | Kabul |
| [adr-bounded-context-per-service](adr-bounded-context-per-service.md) | Bounded Context = Mikroservis | Kabul |
| [adr-cache-vs-readmodel](adr-cache-vs-readmodel.md) | Cache mi, Read Model mi? | Kabul |
| [adr-checkout-saga-orchestration](adr-checkout-saga-orchestration.md) | Checkout Saga — Orchestration (028) | Kabul |
| [adr-credential-link-forward-protection](adr-credential-link-forward-protection.md) | Credential-Giriş Linki Forward Koruması | Öneri |
| [adr-external-agent-oauth-061](adr-external-agent-oauth-061.md) | Dış Agent MCP Erişimi — OAuth 2.1 (061) | Kabul |
| [adr-hybrid-search-slice](adr-hybrid-search-slice.md) | SearchStorefrontProducts — hibrit aramanın ince işçiliği (019) | Superseded |
| [adr-ingestion-llm-writers](adr-ingestion-llm-writers.md) | Ingestion yazıcıları LLM-sürücülü (007 tersine çevirme) | Superseded |
| [adr-mcp-control-plane-no-secret-return](adr-mcp-control-plane-no-secret-return.md) | MCP Kontrol Düzlemidir — Hassas Veri Tool Dönüşüne / LLM'e Girmez | Kabul |
| [adr-moderation-agent-extraction](adr-moderation-agent-extraction.md) | Reviews moderasyonu ayrı broker-worker'a taşındı | Tarihsel |

> Yeni ADR → bu tabloya bir satır ekle (yaşayan indeks).
