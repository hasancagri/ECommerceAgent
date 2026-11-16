# Implementation Plan: Katalog Verisi Zenginleştirme (OpenAI, offline)

**Branch**: `008-catalog-enrichment` | **Date**: 2026-11-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/008-catalog-enrichment/spec.md`

## Summary

Kitapyurdu Excel katalogunu (23.600 kitap) OpenAI ile offline zenginleştiren bir Python CLI aracı. Her kitap için arama-dostu bir açıklama (grounding+kaçış kuralıyla, uydurma yok) + serbest etiketler üretir; orijinali bozmadan zenginleştirilmiş bir kopya Excel yazar. Araç `book-crawl/`'da yaşar, mevcut `fetch_descriptions.py` desenini (ISBN-anahtarlı resumable JSON cache, checkpoint, atomik yazım, limit/report flag) tekrar kullanır. **.NET solution dışıdır** — import/FTS/semantik kablosu bu dilimde yok.

## Technical Context

**Language/Version**: Python 3 (mevcut `book-crawl/` ortamı)

**Primary Dependencies**: `openai` (resmi Python SDK — yapısal JSON çıktı; KURULMASI gerek), `openpyxl` 3.1.5 (kurulu), stdlib `concurrent.futures`/`json`/`os`

**Storage**: Dosya — ISBN-anahtarlı JSON cache (`cache_enrich.json`) + çıktı kopya Excel. DB yok.

**Testing**: Saf fonksiyon birim testi (satır→prompt eşleme, cache-merge, etiket-serileştirme) + `--limit` örnek koşu + elle spot-check. Host/entegrasyon harness yok (Anayasa İlke VI bu dilime domain-TDD dayatmaz — .NET domain yok).

**Target Platform**: Yerel CLI (macOS, kullanıcı makinesi; kaynak Excel repo-dışı)

**Project Type**: Tek-amaçlı veri-hazırlama CLI aracı

**Performance Goals**: ThreadPool eşzamanlılığı; ~23.600 çağrı birkaç saatte; gpt-4o-mini TPM sınırları içinde (bounded worker + backoff)

**Constraints**: Resumable + atomik yazım (yarım dosya yok); uydurma yok (FR-002); maliyet ~$2–3; OpenAI API anahtarı insan-yüzeye/log'a sızmaz (İlke VIII)

**Scale/Scope**: 23.600 kitap, 5 yayınevi sayfası, 27→29 sütun

## Constitution Check

*GATE: Phase 0 öncesi geçmeli. Phase 1 sonrası yeniden kontrol.*

Bu dilim **.NET solution dışı sanksiyonlu offline araçtır** (mevcut `book-crawl/` script'leri gibi; CLAUDE.md "çözüme dahil olmayan klasör kapsam dışı"). BC/aggregate/servis ilkeleri konu-dışıdır.

| İlke | Durum | Not |
|---|---|---|
| I BC İzolasyonu | N/A | Yeni BC/servis yok; araç veriyi hazırlar, çalışma-anı sisteme dokunmaz |
| II/III Aggregate + VSA/CQRS | N/A | .NET domain yok |
| IV Result Pattern | N/A | Python araç |
| V Scope Yetki | N/A | Dış yüzey/endpoint yok |
| VI Domain-TDD | N/A | Saf .NET domain mantığı yok; saf Python helper'lara hafif test (zorunlu değil) |
| VII FLOW.md | N/A | BC değil |
| **VIII Sır insan-yüzeyde görünmez** | **GEÇER (ilgili)** | OpenAI API anahtarı env/secret'tan; log/trace/çıktıya YAZILMAZ. Ek: "uydurma yok" disiplini (FR-002) VIII ruhuyla hizalı — insan-yüzeye yanlış değer sızmaz |
| Teknoloji kısıtları (.NET/Marten/Aspire) | N/A | Bilinçli solution-dışı araç |

**Gate: PASS** — ihlal yok. Tek bağlayıcı kurallar: API anahtarı gizliliği (VIII) + uydurma yasağı (FR-002).

## Project Structure

### Documentation (this feature)

```text
specs/008-catalog-enrichment/
├── plan.md              # Bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── openai-output-schema.md     # OpenAI dönüş sözleşmesi + davranış kuralları
│   └── enriched-xlsx-columns.md    # Çıktı Excel kolon sözleşmesi (import'un okuyacağı)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

Araç **ECommerceAgent repo/solution dışında**, mevcut Python dizininde yaşar:

```text
~/dev/book-crawl/
├── enrich_books.py       # YENİ: ana CLI (enrich / write / report / limit)
├── cache_enrich.json     # YENİ: ISBN -> {aciklama, etiketler, kaynak, status} (resumable)
├── fetch_descriptions.py # mevcut (desen kaynağı; dokunulmaz)
└── ...

# Girdi:  ~/Desktop/ECommerce Notları/Kitapyurdu_Katalog_v2.xlsx           (değişmez)
# Çıktı:  ~/Desktop/ECommerce Notları/Kitapyurdu_Katalog_v3_enriched.xlsx  (yeni kopya)
```

**Structure Decision**: Tek dosyalık Python CLI (`enrich_books.py`) + yanında ISBN-anahtarlı JSON cache. Çıktı Excel = kaynak + cache'in saf fonksiyonu (idempotent, kısmi-güvenli). Yeni bir proje/klasör iskeleti kurulmaz; `book-crawl/` deseni genişletilir.

## Complexity Tracking

Anayasa ihlali yok — tablo boş.
