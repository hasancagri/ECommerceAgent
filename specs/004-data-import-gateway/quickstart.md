# Quickstart — 004 doğrulama kılavuzu

Ön koşul: Docker (Postgres/RabbitMQ/Redis Aspire kaldırır), R2 config'i (kapak için; yoksa kapaklar "alınamadı" düşer — iş yine biter).

```bash
dotnet build && dotnet test                                   # her zaman yeşil kalmalı
dotnet run --project src/aspire/AppHost/AppHost.csproj        # tüm sistem (Gateway dahil)
```

## 1) Gateway kenar-yetki (US2)
- `GET {gateway}/api/catalog/health` → 200 (anonim rota geçer)
- `POST {gateway}/api/admin/catalog/imports` anahtar BAŞLIKSIZ → 401 (servise ulaşmaz; Catalog loglarında istek izi yok)
- Aynı istek `X-Admin-Api-Key: <secret>` ile → 202

## 2) Küçük dosya import'u (US1)
- 20 satırlık deneme .xlsx hazırla (yeni + mükerrer ISBN + ISBN'siz satır karışık; 2 sayfalı olsun — sayfa adı = yayınevi)
- `POST .../imports` (multipart `file`) → 202 + jobId
- `GET .../imports/{jobId}` → sayaçlar: added + skipped + errored toplamı = satır sayısı
- Aynı dosyayı TEKRAR yükle → rapor: tamamı skipped (SC-002)
- `GET .../imports/{jobId}/errors` → ISBN'siz satır sebepli listede

## 3) Gerçek dosya (SC-001)
- `~/Desktop/ECommerce Notları/Kitapyurdu_Katalog_v2.xlsx` yüklenir → iş biter; added ≈ 23.600
- Postgres'te ürün sayısı + 5 yayınevi + kategori ağacı spot kontrol; fiyatsız ürünler IsSalable=false

## 4) Kapak zinciri (US3)
- Import sonrası örnek ürünlerde `ImageUrl` dolu (CoverIngested tüketildi)
- Aynı ISBN ikinci kez (R2'de varken) → File loglarında "indirme atlandı" yolu; RabbitMQ error-queue BOŞ
- ImageUrl tarayıcıda gerçek görseli döner

Kontratlar: [rest-import.md](contracts/rest-import.md) · [events.md](contracts/events.md) · [gateway-routes.md](contracts/gateway-routes.md) · model: [data-model.md](data-model.md)