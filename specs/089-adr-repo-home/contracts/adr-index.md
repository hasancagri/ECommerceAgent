# Contract: ADR İndeksi (`docs/adr/README.md`)

Tüm ADR'ler tek bakışta: slug (link) · başlık · durum (SC-003 / FR-003).

```markdown
# Mimari Karar Kayıtları (ADR)

Mimari kararların *gerekçe* katmanı. Bağlayıcı kurallar: `../../CLAUDE.md` +
`../../.specify/memory/constitution.md`. ADR = karar + neden; kural orada yaşar, ADR link verir.

Dev/ajan: bir karar sınırına dayandığında ("neden X?") gerekçeyi buradan grep'le — vault'a gitme.

| ADR | Başlık | Durum |
|---|---|---|
| [adr-aop-caching-mechanism](adr-aop-caching-mechanism.md) | AOP Query Caching Mekanizması | Kabul |
| [adr-bounded-context-per-service](adr-bounded-context-per-service.md) | Bounded Context = Mikroservis | Kabul |
| [adr-cache-vs-readmodel](adr-cache-vs-readmodel.md) | Cache vs Read-Model | Kabul |
| [adr-checkout-saga-orchestration](adr-checkout-saga-orchestration.md) | Checkout Saga Orchestration | Kabul |
| [adr-credential-link-forward-protection](adr-credential-link-forward-protection.md) | Credential-Link Forward Protection | Öneri |
| [adr-external-agent-oauth-061](adr-external-agent-oauth-061.md) | Dış Agent MCP OAuth (061) | Kabul |
| [adr-hybrid-search-slice](adr-hybrid-search-slice.md) | Hibrit Arama Slice | Superseded |
| [adr-ingestion-llm-writers](adr-ingestion-llm-writers.md) | Ingestion LLM Writers | Superseded |
| [adr-mcp-control-plane-no-secret-return](adr-mcp-control-plane-no-secret-return.md) | MCP Kontrol Düzlemi — Sır Dönüşte Yok | Kabul |
| [adr-moderation-agent-extraction](adr-moderation-agent-extraction.md) | Moderation Agent Extraction | Tarihsel |
```

## Kurallar

1. **Tam kapsam:** `docs/adr/adr-*.md`'deki her dosya için tam bir satır (10 satır; SC-003). Eksik/fazla yok.
2. **Link çözülür:** her satırın linki gerçek `docs/adr/` dosyasına gider (0 ölü satır; SC-001).
3. **Durum = dosya frontmatter'ı:** indeks `status` ile ADR `status:` birebir aynı (D5 vokabüleri).
4. **Başlıklar yukarıda tahminîdir** — implement sırasında her ADR'nin gerçek `# ADR:` başlığından
   alınır (bu tablo referans şablon, kesin metin değil).
5. **Yeni ADR → yeni satır:** ileride ADR eklenince bu tabloya bir satır eklenir (yaşayan indeks).
