---
status: Tarihsel
---

# ADR: Reviews moderasyonu BC'den ayrı broker-tabanlı worker'a taşındı

> 🗄️ **TARİHSEL (uygulanmaz):** Bu ADR'nin anlattığı moderasyon adımının TAMAMI 088'de (2026-10-05)
> söküldü — `Reviews.Moderation` worker + AI moderasyon + `ReviewModerationRequested`/`ReviewModerated`
> event'leri kaldırıldı; yorum artık serbest/anında görünür (moderasyon yok). Kayıt tarihsel tutulur:
> "agent kodu BC'den ayrı worker'a" kararının *neden*'i hâlâ öğretici. Bugünkü Reviews = `specs/088`.

> **Gerekçe/açıklama katmanı.** Bağlayıcı gerçek = kod + `../../CLAUDE.md` + `specs/046-reviews-moderation-agent`.
> İlgili: [adr-bounded-context-per-service](adr-bounded-context-per-service.md).

**Durum:** TARİHSEL — moderasyon adımı 088'de tümüyle söküldü · **Feature:** 046 · **Karar tarihi:** 2026-08-22

## Bağlam

044'te Reviews BC, moderasyonu **in-proc** `ModerationAgent` (Microsoft.Agents.AI `ChatClientAgent`,
Temp=0, structured JSON) ile yapıyordu — agent-framework kodu Reviews.Api projesinde YAZILIYDI. Aynı
desen (o dönem) Procurement `EnrichmentAgent`'ta da vardı (Procurement 050'de söküldü). Kullanıcı ilkesi: **agent yazımı yalnız `src/agents/`
altındaki projelerde olsun**; BC domain'iyle karışmasın (dağınıklık azalt).

## Karar

Moderasyon LLM adımı, `src/agents/Reviews.Moderation` adlı **DB'siz ayrı worker servisine** taşındı.
Reviews ↔ worker iletişimi yalnız **RabbitMQ fanout** iki event'le:
`ReviewModerationRequested` (Reviews→worker, PII yok) ve `ReviewModerated` (worker→Reviews).

- **Domain otoritesi Reviews'te kalır:** worker yalnız içerik-sınıflandırıcı verdict üretir; gizleme
  kararı (Visible→Hidden) hâlâ `Review.ApplyModeration` aggregate metodunda uygulanır.
- **Reviews'te sıfır agent-framework** (kaynak + csproj); OpenAI bağımlılığı worker'a taşındı.
- **Post-moderation + fail-open korundu:** yorum anında Visible; ihlalde sonradan Hidden. `SubmitReview`
  `[Transactional]` handler'ında transactional outbox → broker down olsa submit reviewsDb'ye commit olur,
  mesaj outbox'ta bekler (submit broker'a senkron bağlanmaz). Moderasyon kritik değildir.

## Alternatifler (elendi)

- **A — agents/ kütüphanesi (in-proc çalışır):** agent kodu agents/'a taşınır ama BC'nin bağımlılık
  grafiğinde agent-framework transitively kalır; runtime izolasyon yok. Kullanıcı fiziksel ayrımı istedi.
- **B — senkron gRPC worker:** moderasyon request/response değil, async fail-open — sync yanlış model.
- **C-broker (SEÇİLEN):** moderasyon zaten async/dayanıklı bir adım; repo'nun mevcut RabbitMQ fanout +
  "tüketici binding kurar" deseni (007 dersi) birebir oturuyor.

## Sonuçlar

- (+) Agent yazımı merkezi (agents/); Reviews domain'i temiz. (+) Runtime tam izolasyon.
- (−) +1 çalışan servis, +2 event, servisler-arası error-queue. Maliyet kabul (moderasyon kritik değil).
- **Anayasa İLKE I uyumu:** worker DB'siz → BC değil, **ChatAgent emsali** agent process; iletişim
  sanksiyonlu kanal (integration event). LLM retry→error queue worker'da.
- **Wolverine keşif tuzağı yine vuruldu:** worker `ReviewModerationEventHandlers` + Reviews
  `ReviewsEventHandlers` (rename) `IncludeType` gerektirdi (4. vaka).

## Kapsam dışı (değişmedi)

Satın-alma-kanıtı `HasConfirmedPurchase` gRPC (fail-closed), Reviews aggregate/`reviewsDb`,
`ReviewSummaryChanged`→Storefront, eligibility/query'ler. (Tarihsel: "Procurement `EnrichmentAgent`
hâlâ in-proc — aynı ilke ona da uygulanabilir" denmişti; ama **Procurement BC 050 first-party
pivot'ta tümüyle söküldü** → EnrichmentAgent artık YOK, konu düştü.)
