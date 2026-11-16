# Research: Katalog Verisi Zenginleştirme (OpenAI, offline)

## 1. OpenAI erişim yöntemi + model

- **Decision**: Resmi `openai` Python SDK, Chat Completions + **structured output** (`response_format` JSON schema), model **gpt-4o-mini**.
- **Rationale**: Yapısal JSON dönüşü güvenilir (şema zorlar, parse hatası azalır); SDK retry/timeout verir. gpt-4o-mini ucuz + tool/JSON sağlam, 23.600 kayıt için maliyet ~$2–3.
- **Alternatives**: Ham `urllib` + JSON-mode (mevcut `fetch_descriptions.py` deseni) — SDK'sız ama şema zorlaması + retry elle; reddedildi (SDK daha az kod, daha sağlam). OpenAI **Batch API** (%50 ucuz, async) — submit/poll/download karmaşası; ileride flag olarak eklenebilir, v1'de sync.
- **Kurulum notu**: `openai` SDK ortamda **KURULU DEĞİL** → `pip install openai` görev adımı. `openpyxl` 3.1.5 kurulu.

## 2. Prompt stratejisi — grounding + kaçış

- **Decision**: Tek sistem-prompt'u; girdi = ad + yazar + kategori + kaynak açıklama. Kurallar:
  1. Kitabı **gerçekten biliyorsan** doğru, Türkçe, arama-dostu zengin açıklama yaz.
  2. **Bilmiyorsan** yalnız verilen kaynak açıklamadaki bilgiyi yeniden ifade et — yeni olay/karakter/tema **EKLEME**.
  3. Kaynak açıklama boş + kitabı bilmiyorsan **uydurma**; `kaynak="yetersiz"` döndür.
  4. Serbest Türkçe etiketler üret (tema/alt-tür/ruh hali); **tür/kategori etiketleme**.
  5. Çıktıda bir `kaynak` alanı: `model` | `kaynak_aciklama` | `yetersiz` — hangi temele dayandığını model kendi bildirir (denetim + rapor + SC-003 doğrulaması).
- **Rationale**: Cold-start + uzun-kuyruk uydurma riskini kuralla kapatır (spec FR-002, US3). `kaynak` alanı, uydurma denetimini ölçülebilir yapar.
- **Alternatives**: Tam serbest (her zaman zengin yaz) — uzun kuyrukta uydurur, reddedildi. Yalnız-kaynak (hiç dünya bilgisi) — ünlü kitaplarda zayıf; kullanıcı kararıyla reddedildi.

## 3. Excel okuma/yazma

- **Decision**: `openpyxl` ile kaynağı oku, **kopya** workbook'a yaz. Her sayfada (5 yayınevi) 27 sütun korunur, "Açıklama" OpenAI metniyle değişir, iki yeni sütun eklenir ("Kaynak Açıklama", "Etiketler"). Çıktı = kaynak + cache'ten yeniden üretilir (idempotent).
- **Rationale**: Orijinal bit-aynı kalır (FR-008, SC-005). Çıktıyı cache'ten türetmek kısmi koşuyu güvenli yapar — write her an tekrarlanabilir.
- **Alternatives**: Kaynağı yerinde değiştir — reddedildi (orijinal korunmalı). CSV ara-format — Türkçe/çok-sayfa/encoding riski, reddedildi.
- **Not**: 23.600 satır × 5 sayfa writable workbook'ta bellekte sorun değil (openpyxl normal mod).

## 4. Resumability + cache

- **Decision**: Tek `cache_enrich.json`, ISBN → `{aciklama, etiketler, kaynak, status}`. Her N kayıtta (ör. 100) checkpoint; atomik `tmp`+`os.replace` yazım. **Başarısız kayıt cache'e YAZILMAZ** (status=error kalıcılaşmaz) → sonraki koşu yeniden dener. Artımlı: cache'te `status=ok` olan ISBN atlanır.
- **Rationale**: `fetch_descriptions.py` kanıtlı deseni; kesinti/çökme ucuz toparlanır (FR-009/010/011, SC-004).
- **Alternatives**: SQLite cache — fazla ağır, reddedildi. Çıktı xlsx'i checkpoint olarak kullanma — yavaş/bozulabilir, reddedildi.

## 5. Eşzamanlılık + hız sınırı

- **Decision**: `ThreadPoolExecutor`, bounded worker (başlangıç ~8, env ayarlı). 429/5xx'te üstel backoff + retry (SDK + elle). Global throttle opsiyonu (mevcut desende var).
- **Rationale**: 23.600 sync çağrı seri çok yavaş; bounded paralellik TPM sınırını aşmadan hızlandırır.
- **Alternatives**: Tek-thread — saatlerce uzun, reddedildi. Yüksek worker — rate-limit/429 fırtınası, reddedildi.

## 6. API anahtarı (İlke VIII)

- **Decision**: Anahtar `OPENAI_API_KEY` ortam değişkeninden okunur. Log/trace/çıktı/konsola **yazılmaz**. Env'de yoksa araç net hata verip durur.
- **Rationale**: İlke VIII — sır insan-yüzeye/ize girmez. (Anahtar şu an ScoutAgent'ta user-secret; buraya env ile verilir.)
- **Alternatives**: Koda gömme/CLI arg — reddedildi (log/history sızıntısı).

## 7. Maliyet doğrulaması

- **Decision**: Kabul edilen bütçe ~$2–3 (girdi ~500 token: ~800-karakter açıklama + ad/yazar/kategori; çıktı ~150 token; 23.600 kayıt; gpt-4o-mini $0.15/$0.60 per 1M). `--limit` ile önce küçük örnek + gerçek token ölçümü.
- **Rationale**: Önce-ölç (limit koşusu) sonra tam koşu — sürpriz maliyet yok.

## Çözülen NEEDS CLARIFICATION

Yok — spec'te clarification marker'ı yoktu; tüm kararlar brainstorm'da verildi. Açık operasyonel detaylar (worker sayısı, checkpoint aralığı, çıktı dosya adı) yukarıda varsayılanlarla sabitlendi.
