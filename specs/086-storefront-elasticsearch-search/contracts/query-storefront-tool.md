# MCP Tool Sözleşmesi: `query_storefront` (ES DSL)

Müşteri yüzeyinin TEK arama ucu (FR-010). İnce sarmalayıcı → `Features/Agents/Queries/QueryStorefront`
(İLKE III). Storefront MCP anonim (İLKE V, CLAUDE.md). Guard ertelendi — minimal sunucu rail bağlar.

## Giriş

```
query_storefront(query: string)
```

- `query` = **ham Elasticsearch Query DSL (JSON)**, sabit arama index'ine karşı request body.
- Anlamsal arama: body içinde `{{EMBED:"tema metni"}}` yer-tutucusu → sunucu OpenAI
  `IEmbeddingGenerator` ile 1536-boyut vektöre çevirir ve `knn.query_vector`'a basar. LLM ham vektör
  YAZMAZ (token + sızıntı). İkame bekçiden/çalıştırmadan ÖNCE.

## Çıkış

`FeatureObjectResultModel<QueryStorefrontResponse>`:

```
Ok: bool
Rows: [{ alan: değer }]      # _source whitelist'i (embedding YOK); ES _source alanları
RowCount: int
Truncated: bool              # size tavanı (≤50) aşıldıysa
```

- Hata → Result.Error, `MessageItem.Code` = resource sabiti (ret kodu makine-okur, FR-009 düzeltme döngüsü).
- Boş sonuç = başarı + boş Rows (hata DEĞİL; dürüst "bulunamadı", FR-009/SC-005 — uydurma yok).

## Minimal sunucu rail (tam JSON guard DEĞİL — ayrı iş)

| Rail | Davranış |
|---|---|
| Index sabit | LLM index vermez; sunucu `storefront-books`'a koşar |
| `size` tavan | ≤50'ye clamp; aşım → `Truncated=true` |
| `_source` whitelist | dönüş alanları sabit; `embedding` asla dönmez |
| Timeout | sunucu-tarafı istek timeout'u (sonsuz sorgu yok) |
| İz | ret DAHİL her çağrı `AgentQueryLog` (Executed/Rejected/Failed) |

> NOT: clause/alan whitelist, script/scroll reddi, knn.k tavanı = **JSON guard** (R7, KAPSAM DIŞI).
> Bu sürüm güvenilir istemci ortamı varsayar (spec assumption).

## Tool Description (playbook — 070 kanonik ev)

ES DSL ile yeniden yazılır; [MCP Tool Description Standardı]'na uyar, `Shared/McpToolDescriptions.cs`
const'ından referanslanır (inline string bırakma — CLAUDE.md yapma listesi). İçerik:
- Index şeması (alan adları + tipleri, satılabilir kitaplar; `contracts/search-index-mapping.json` özeti).
- DSL kalıpları: `match`/`multi_match` (fuzzy `fuzziness:"AUTO"` — FR-002), `term`/`range` (fiyat/puan/stok
  filtre), `nested` (specs), `knn` + `{{EMBED}}` (semantik — FR-003), `bool` birleşim (tek sorguda
  text+filter+kNN — FR-001). Benzerlik: hedef ürünün embedding'iyle kNN.
- Kategori İngilizce taksonomi çeviri kılavuzu (mevcut playbook'tan taşınır).
- Dürüstlük bloğu: kapsam-dışı istek → yönlendir; boş/eşik-altı → "bulunamadı"; satış adedi vitrinde YOK.