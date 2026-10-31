# Research — 096 Catalog local embeddings

## R1 — Embedding modeli: bge-m3 (Ollama)

- **Decision**: `bge-m3` (BAAI, 567M, 1024 boyut, 8k token bağlam, 100+ dil) — Ollama'da hazır.
  Model adı config'te (`EmbeddingOptions.Model`); değişim FR-006 toplu tazelemeyi tetikler.
- **Rationale**: Türkçe kıyaslarda en iyi retrieval (2026 karşılaştırmaları); çok-dilli ortak uzay —
  Türkçe sorgu/metin uyumu. Boyut maliyeti kabul (11.4k × 1024 float ≈ 47 MB).
- **Alternatives**: `nomic-embed-text` (EN-ağırlıklı, Türkçe zayıf — red) · `qwen3-embedding:0.6b`
  (küçük/hızlı, çok-dilli — yavaşlık sorun olursa config'ten geçilebilir, mimari değişmez).
- Kaynaklar: [Ollama embedding kıyası](https://www.morphllm.com/ollama-embedding-models) ·
  [açık embedding modelleri 2026](https://www.bentoml.com/blog/a-guide-to-open-source-embedding-models) ·
  [Türkçe bge-m3 ince-ayar](https://huggingface.co/nezahatkorkmaz/turkce-embedding-bge-m3)

## R2 — Depolama: Marten dokümanı + UsePgVector (eski Storefront emsali)

- **Decision**: `ProductEmbedding` Marten dokümanı (Id=ProductId; `float[] Vector` JSONB içinde) +
  `opts.UsePgVector()` (extension kaydı). **HNSW/kNN index YOK** — tüketici yüzey yok; index kararı
  arama spec'inin işi.
- **Rationale**: Birebir emsal repo tarihinde (`git show 836de86~1:…/Storefront…/MartenExtensions.cs`):
  embedding JSONB'de float[], `UsePgVector()`, "20k satırda exact scan ms mertebesi — index bilinçli yok".
  Marten yönetiminde kalır → elle DDL/startup-ensure gerekmez, `ApplyAllDatabaseChangesOnStartup` işler.
- **Alternatives**: Ham SQL yan tablo + `vector` kolonu → elle DDL + Npgsql type-mapping yönetimi;
  tüketici doğunca gerekirse o spec'te `::vector` cast (emsal) ya da duplicated kolonla gelir, red.
  Genel polimorfik embeddings tablosu → FR-007'ye aykırı, red.

## R3 — Süreç: BackgroundService + PeriodicTimer (Process/)

- **Decision**: `Process/EmbeddingReconcileProcess` — `BackgroundService`; döngü: aday sorgusu
  (hash/model uyuşmayan ilk BatchSize ürün) → compose+embed(batch) → upsert → aday bitene dek devam;
  boşta `PeriodicTimer` (IntervalSeconds) ile uyu. Scope/session döngü başına.
- **Rationale**: Seed (`BookSeedHostedService`) emsali; Wolverine `ScheduleAsync` tick'i tek handler'lı
  bu iş için tören (dayanıklılık zaten reconcile'ın doğasında — tick kaçsa sonraki tarama kapatır).
- **Alternatives**: Wolverine scheduled message → durable ama gereksiz karmaşıklık, red; event-tetikli
  (ProductChanged dinle) → kaçan event bayat bırakır + fiyat değişimi boşa tetikler, red (spec R2 kararı).
- **TUZAK (memory)**: HttpClient timeout `TaskCanceledException` üretir — `stoppingToken` iptaliyle
  KARIŞTIRMA (yanlış catch host'u durdurur); ayrım `stoppingToken.IsCancellationRequested`.

## R4 — Ollama istemcisi: SDK'sız düz HttpClient

- **Decision**: `OllamaEmbeddingClient` — typed HttpClient; `POST {BaseUrl}/api/embed` gövde
  `{ "model": "...", "input": ["metin1", ...] }` → `{ "embeddings": [[...], ...] }` (batch native).
  Hata/erişilemezlik → istisna; süreç loglayıp sonraki döngüye bırakır (fail-soft, FR-005).
- **Rationale**: Tek endpoint için SDK bağımlılığı gereksiz (kullanıcı tercihi: dolaylama yerine düz
  kod); batch desteği API'de var. Kontrat: contracts/ollama-embed-api.md.
- **Alternatives**: OllamaSharp / Microsoft.Extensions.AI soyutlaması → ek paket + soyutlama katmanı,
  tek çağrı için red.

## R5 — Metin bileşimi + hash: tek saf çekirdek

- **Decision**: `EmbeddingText.Compose(name, description, authorNames, categoryChainNames)` →
  tek metin (satır formatı sabit); `MaxChars` (config, varsayılan ~6000) aşımında kırp;
  `Hash = SHA256(composed)`. İçerik-hash metinden; model kimliği AYRI kolonda karşılaştırılır.
- **Rationale**: FR-003 "tek yerde tanımlı"; hash kırpılmış metinden → tutarlılık (spec edge case).
  Test-first saf birim (İlke VI).
- **Alternatives**: Token-bazlı kırpma → tokenizer bağımlılığı, char-cap yeter (bge-m3 8k token ≈
  güvenli karakter aralığının üstü), red.

## R6 — Ollama konumlandırma: dış bağımlılık, Aspire kaydı YOK

- **Decision**: AppHost Ollama'yı başlatmaz/başlatmayı denemez; `EmbeddingOptions.BaseUrl` varsayılan
  `http://localhost:11434`. `Enabled=false` ile süreç tamamen kapatılabilir (CI/prod güvenlik supabı).
- **Rationale**: Kullanıcı makinesi kurulumu (spec assumption); container'da Ollama ağır (model GB'ları),
  dev deneyimi kötüleşir. Kapalıyken sistem zaten yara almıyor (US2).
- **Alternatives**: Aspire container olarak Ollama → model volume + GPU sorunları, red (ileride
  istenirse eklenebilir, mimari değişmez).

## R7 — ADR: arama katmanı Postgres'e katlandı + embedding local

- **Decision**: `docs/adr/adr-search-fold-to-postgres.md` — 086 ES kararının tersine dönüşü (Storefront+ES
  söküldü, keşif Catalog-içi pgvector omurgasına), embedding üretimi local (bulut API yasak), tüketici
  yüzeyler sonraki spec'ler. CLAUDE.md "Yapma listesi"ndeki ES maddesinin kalıcı gerekçe adresi olur.
- **Rationale**: FR-010; kural CLAUDE.md'de, gerekçe ADR'de (katman ayrımı).
