# Kontrat — Ollama embed API (TÜKETİLEN dış kontrat)

Bu feature dış yüzey AÇMAZ; tek kontrat Ollama'nın tüketilen HTTP API'si.

## İstek

```
POST {EmbeddingOptions.BaseUrl}/api/embed
Content-Type: application/json

{ "model": "bge-m3", "input": ["metin 1", "metin 2", "..."] }
```

- `input` dizi = batch (BatchSize kadar); tek string de geçerli ama hep dizi gönderilir.
- Kimlik/sır YOK (localhost); İlke VIII kapsamına giren değer taşınmaz.

## Yanıt

```
{ "model": "bge-m3", "embeddings": [[0.01, -0.03, ...], ...] }
```

- `embeddings[i]` ↔ `input[i]` sıra eşleşmesi; eleman sayısı = model boyutu (bge-m3: 1024).
- Boyut, dönen ilk vektörden doğrulanır (beklenmedik boyut → batch reddi + log).

## Hata davranışı (istemci tarafı)

| Durum | Davranış |
|---|---|
| Bağlantı yok / timeout | İstisna → süreç loglar, batch sonraki döngüye (FR-005; host ÇÖKMEZ) |
| 404 model yok | Aynı; log mesajı model adını söyler ("ollama pull bge-m3") — spec edge case |
| Kısmi/bozuk yanıt (sayı uyuşmaz) | Batch reddedilir, yazım yok (yarım batch yazılmaz) |

Timeout TUZAĞI: HttpClient timeout'u `TaskCanceledException` — `stoppingToken` iptali sanılıp host
durdurulmaz (memory: httpclient-timeout-taskcanceled-stophost).
