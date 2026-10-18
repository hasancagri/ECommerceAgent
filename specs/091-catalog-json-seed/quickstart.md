# Quickstart — 091 canlı doğrulama

## Ön koşul

- Dev full-reset (Docker volume'ları sil — eski taslak ürünler/Import tabloları gider; göç yok).
- `books.json` üretilmiş + commit'li (bkz. [contracts/seed-record.md](contracts/seed-record.md)); `stock` alanı dolu.
- OpenAI key'leri set (Storefront embedding — mevcut kural).

## Adımlar

```bash
dotnet build && dotnet test            # yeni invariant testleri + silinen import testleri sonrası yeşil
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

1. **Seed doluşu (US1)**: Catalog loglarında seed başlangıç/bitiş satırı; ikinci açılışta "atlandı" (idempotent). Doğrula: `search_products` ürün döner; Stock'ta `admin_list_all_stock` satır değerleri seed `stock` alanıyla eşleşir.
2. **Yüzey sökümü (US2)**: MCP tool listesinde `admin_import_catalog` / `admin_publish_imported` / `admin_get_import_status` / `admin_set_published` YOK. `admin_create_product` fiyat>0 ile tek adımda canlı ürün; fiyat=0 Result hatası. Tarayıcıda `/catalog-import/herhangi-token` → 404.
3. **Kapak (US3)**: Temiz staging'le açılışta File.Api migration seed JSON'dan kapak indirir (`{RootPath}/covers/`); ürün `CoverIngested` zinciriyle ImageUrl kazanır; `GET /files/v1/covers/{isbn}` servis eder.
4. **Vitrin zinciri**: `query_storefront` seed ürünlerini bulur (ES doluşu `ProductChangedEvent`+projection ile; gerekirse `admin_republish_products`).

## Guard'lar (commit öncesi)

```bash
scripts/check-claude-spec-links.sh     # BC haritası spec yolları
scripts/check-flow-links.sh            # FLOW.md anchor'ları (silinen tipler FLOW'dan da düşmeli)
scripts/check-agent-query-schema.sh
```

## Beklenen kalıcı değişiklikler

- catalogDb'de importsession/importrow tabloları YENİ ortamda hiç doğmaz.
- ClosedXML hiçbir csproj'da ve `Directory.Packages.props`'ta kalmaz.
- `~/dev/catalog-data/catalog-import.xlsx` artık hiçbir şey tarafından okunmaz.
