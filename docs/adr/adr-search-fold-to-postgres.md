---
status: Kabul
---

# ADR + Öğrenme Notu: Arama Postgres'e Katlandı + Embedding Local (096)

> **Gerekçe + öğrenme katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` +
> `specs/096-catalog-local-embeddings`. İlgili: [adr-cache-vs-readmodel](adr-cache-vs-readmodel.md),
> [adr-hybrid-search-slice](adr-hybrid-search-slice.md) (Superseded — eski hibrit arama dilimi).

**Durum:** Kabul · **Feature:** 096-catalog-local-embeddings · **Tersine çevirdiği karar:** 086 (Storefront+ES)

---

## 1. Karar — tek cümle

**Arama/keşif katmanı ayrı bir Storefront BC'si + Elasticsearch'te DEĞİL, Catalog'un kendi Postgres'inde
yaşayacak (pg_search BM25 + pgvector + RRF hedefi); embedding üretimi LOCAL modelle (Ollama, bge-m3) —
bulut embedding API'si yasak.** 096 bu omurganın ilk taşını döşer: `ProductEmbedding` dokümanı +
BC-içi reconcile süreci. Tüketici yüzeyler (hibrit arama, benzer-kitap, danışman agent) sonraki spec'ler.

## 2. Neden 086'yı tersine çevirdik

086, read-model'i ES'e taşımıştı (LLM ES-DSL üretir, Kibana gözlem). Araya giren gerçekler dengeyi değiştirdi:

- **Temiz-sayfa pivotu (2026-10-30):** BYO-agent/MCP yüzeyi terk edildi; `query_storefront`/ES-DSL'in
  tüketicisi kalmadı. Storefront BC + ES + Kibana söküldü.
- **Ölçek ES'i haklı çıkarmıyor:** 11.4k kitapta Postgres exact scan ms mertebesi; ayrı arama kümesinin
  işletme yükü (index yaşam döngüsü, çift-yazım senkronu, service-token) karşılığını vermiyor.
- **BC izolasyonu sadeleşir:** read-model Catalog'un KENDİ verisinin türevi — ayrı BC + broker projection
  zinciri yerine aynı DB'de yan doküman (`ProductChanged` fanout'u dış tüketiciler için sürer).
- **Kişiselleştirme kuzey-yıldızı embedding uzayı istiyor** (arama + benzer + zevk vektörü aynı uzaydan);
  uzayın evi ürün verisinin evi olmalı ki reconcile ucuz ve atomik kalsın.

## 3. Neden LOCAL embedding (bulut yasak — FR-004)

- **Maliyet/çıkış bağımlılığı:** 11.4k × her metin değişimi × model tazelemeleri — bulut API faturası ve
  key yönetimi (OpenAI key iptal dersi) kalıcı sürtünme; local model sıfır marjinal maliyet.
- **Veri zaten public künye** — gizlilik değil, bağımsızlık argümanı: dev ortamı çevrimdışı çalışır.
- **bge-m3 (1024d) Türkçe retrieval'da güçlü** ve Ollama'da hazır; model adı config'te
  (`EmbeddingOptions.Model`), değişim toplu tazelemeyi kendiliğinden tetikler (hash/model iki bağımsız
  bayatlık ekseni).

## 4. Mimari biçim (096 as-built)

- `ProductEmbedding` Marten dokümanı (Id=ProductId, float[] JSONB) — aggregate DEĞİL, invariant yok.
- `Process/EmbeddingReconcileProcess` BackgroundService: eksik/bayat kayıtları tarar, Ollama'ya batch,
  upsert; Ollama kapalıyken loglayıp bekler (fail-soft — host çökmez, sistem yara almaz).
- **Broker bilinçli YOK:** aynı DB içi türetilmiş veri; event-tetikli üretim kaçan event'te kalıcı bayatlık
  bırakır, reconcile taraması bırakmaz.
- **Vektör index'i (HNSW) bilinçli YOK:** tüketici yüzey yokken index kararı prematüre; arama spec'inin işi.
- Ollama **dış bağımlılık** — AppHost başlatmaz (model GB'ları container'ı şişirir); `Enabled=false` supabı.

## 5. Ders

Read-model teknolojisi tüketicisiyle seçilir: 086'da "LLM'e ES-DSL ürettirme" hedefi ES'i getirdi; hedef
ölünce teknoloji de öldü. Omurga (embedding) ile yüzey (arama) ayrı spec'lenince geri dönüş ucuz kaldı.
