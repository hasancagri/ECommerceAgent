---
status: Superseded
---

# ADR: Ingestion yazıcıları LLM-sürücülü — 007 "NO LLM writers" duruşunun tersine çevrilmesi

> 🗄️ ARŞİV (uygulanmaz): IngestionAgent + LLM yazıcı zinciri 050 first-party pivotunda söküldü. Tarihsel karar kaydı. Bugün ürün girişi = Excel import (083) + admin create_product (074).

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + `specs/015-ingestion-llm-writers`.
> İlgili: [adr-bounded-context-per-service](adr-bounded-context-per-service.md).

**Durum:** SUPERSEDED (2026-08-19, 041) — LLM yazıcı zinciri IngestionAgent'la birlikte SÖKÜLDÜ;
tek yazım yolu Procurement event'leri, AI yalnız `EnrichmentAgent`'ta (eksik içerik, MCP'siz).
· **Feature:** 015 · **Karar tarihi:** 2026-07-27 — aşağısı tarihsel gerekçe kaydı.

> **Evrim (karar sonrası):** 016 zincire Brand/Category yazıcılarını ekledi (5 adım); 018 Discount'ı
> sistemden silince zincir **4 LLM yazıcıya** indi (Brand → Category → Catalog → Stock) ve aşağıdaki
> "tek gerçek karar noktası discount'ta" tespiti tarihsel kaldı — bugün tüm adımlar tekdüze yazımdır.
> Kararın özü (yazıcılar LLM-sürücülü) yürürlükte.

## Bağlam

007, IngestionAgent'ı bilinçle **LLM'siz** kurdu: MCP tool'ları koddan `CallToolAsync` ile doğrudan
çağrılıyordu (deterministik, ucuz, hızlı). Ama bu kullanımda MCP düz bir RPC sarmalayıcısından
farksızdı — tool açıklamaları/şemaları hiçbir model tarafından okunmuyordu. Kullanıcının ifadesiyle:
"LLM olmadan MCP anlamsız bir tören."

## Karar

Üç yazıcı adım (o günkü zincir: catalog/stock/discount) kendi **ChatClientAgent**'ına taşındı; tool'ları artık LLM
çağırıyor. Bu **fonksiyonel bir kazanç değil, bilinçli mimari/pedagojik yatırımdır** (kullanıcı
kararı, 2026-07-27): MCP'yi anlamlı kılmak + gelecekte ham-feed normalizasyonu (LLM'in gerçek değer
kattığı yer) için iskeleti bugünden kurmak.

## Değerlendirilen yollar

- **007'de kalmak (LLM'siz)** — ELENDİ: çalışıyor ama MCP töreni sürüyor; proje adı AgentFramework,
  LLM'i yazma yoluna katmak asıl amaçtı. Ürün kararıyla tersine çevrildi.
- **LLM-sürücülü yazıcılar** — SEÇİLDİ: adım başına scope'lu agent (catalog: upsert; stock: set;
  discount: set/remove), tek paylaşılan IChatClient/model.

## Sınırlar / bedeller ve hafifletmeler

- **Sahte-başarı riski** (yazma olmadan "ok"): temperature 0 + dar allowlist + structured output
  (`WriterResult`) + catalog'da "başarı ProductId'siz olamaz" emniyeti. Deterministik geri-okuma
  doğrulaması bilinçle kapsam dışı (gelecek sertleştirme adayı).
- **Maliyet/gecikme:** feed diff-only → hacim düşük; kayıt başına ≤3 LLM adımı kabul edildi.
  Replay'de geçilmiş adımlar da yeniden LLM'e mal olur (idempotent yazmalar yakınsamayı garanti eder).
- **Dış davranış sözleşmesi korunur:** kuyruk/DLQ adları, 10/30/60 retry, `IngestionWriteException`
  köprüsü, `WORKFLOW_INCOMPLETE` (S4) bire bir aynı.
- **Short-circuit** artık MAF conditional edge + terminal collector'da; semantik spike ile kanıtlı
  (`WorkflowSemanticsSpikeTests`, FR-015): başarısız adımdan sonrakiler HİÇ tetiklenmiyor.
- **Tekdüzelik:** stok adımında LLM'in vereceği gerçek karar yok — yine de LLM-sürücülü (bilinçli
  tercih). Tek gerçek karar noktası discount'ta (set-mi-remove-mu prompt kuralıyla LLM'de).

## Ripple

- `McpConnector`/`McpToolInvoker` + zarf ayna tipleri (`ToolOutcome` vb.) silindi (SC-005).
- ChatAgent'ın `PerUserMcpTool` deseni token'sız kopyalandı (`AnonymousMcpTool`); ortak kütüphaneye
  çıkarma 2 kopya için erken bulundu.
- Gateway'e ingestion sonucu ack'i tartışıldı ve **reddedildi** (fanout ayrışması bozulur; DLQ +
  idempotent replay + 014 feed-otoritesi boşluğu kabul edilebilir kılıyor) — gelecek adayı olarak
  ayrı feature.
