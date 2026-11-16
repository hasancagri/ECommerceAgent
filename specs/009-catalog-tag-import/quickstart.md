# Quickstart: Katalog Etiket Import Doğrulama

## Ön koşullar

- Aspire AppHost ayakta (`dotnet run --project src/aspire/AppHost/AppHost.csproj`).
- Boş catalogDb (taze import varsayımı — 098 seed yok).
- Kaynak: 008 çıktısı `~/Desktop/ECommerce Notları/Kitapyurdu_Katalog_v3_enriched.xlsx` (`Etiketler` sütunlu).

## Birim test (test-first — İlke VI)

```bash
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj
```

Kapsam: `BookTagTests` (Create normalize/boş-guard), `ProductTagTests` (SetTags dedup/replace/boş), `ExcelRowMapperTests` (etiket parse + sütun-yok geriye-uyumluluk).

## Canlı doğrulama (acceptance ↔ test)

| Senaryo | Adım | Beklenen |
|---|---|---|
| **US1** etiketli import | `_v3_enriched.xlsx` yükle (gateway `/api/admin/catalog/imports`, X-Admin-Api-Key) → dry-run raporu → confirm | Dry-run "etiketli satır" sayacı dolu; onay sonrası ürünler etiket taşır |
| **US1** etiketsiz satır | `Etiketler` boş bir ISBN | Ürün etiketsiz, hatasız doğar; iş durmaz |
| **US2** gruplama/İ-ı | `Distopya`/`distopya`/`DİSTOPYA` ayrı satırlarda | Üçünün `BookTag.Value` normalize değeri aynı |
| **Marten tuzağı** | Import sonrası etiketli ürünü OKU (sorgu/okuma yolu) | 500 YOK — BookTag deserialize sorunsuz (unit test yetmez, canlı adım; research §3) |
| **INSERT-ONLY** | Aynı dosyayı tekrar import et | Var olan ISBN atlanır; etiket değişmez (ezilmez) |
| **geriye-uyumluluk** | `Etiketler` sütunu OLMAYAN eski Excel | Import hatasız geçer, ürün etiketsiz |

## Kapsam dışı (bu dilimde çalıştırılmaz)

Etiketle arama/keşif (pg-hibrit gruplama), etiket gösterim UI'ı, var-olan-ürün backfill — sonraki dilim(ler).
