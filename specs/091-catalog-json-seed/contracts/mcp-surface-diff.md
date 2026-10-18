# Sözleşme değişikliği: Catalog admin MCP yüzeyi (dış kırılma kaydı)

MCP tool adı dış sözleşmedir (CLAUDE.md); bu feature BİLİNÇLİ kırar. Tüketici: kullanıcının kendi Claude Desktop kayıtları — kabul edildi (plan oturumu 2026-10-18).

## Silinen tool'lar

| Tool | Const (`Shared/McpToolNames.cs`) | Neden |
|---|---|---|
| `admin_import_catalog` | `CatalogAdminTools.ImportCatalog` | Excel import yolu söküldü |
| `admin_publish_imported` | `CatalogAdminTools.PublishImported` | Import + yayın makinesi söküldü |
| `admin_get_import_status` | `CatalogAdminTools.GetImportStatus` | Import söküldü |
| `admin_set_published` | `CatalogAdminTools.SetPublished` | Yayın/gizleme ekseni söküldü |

İlgili `McpToolDescriptions` const'ları da silinir. `CatalogAdminSurface.SCOPES` değişmez (AdminCatalogRead/Write başka tool'larca kullanılıyor).

## Davranışı değişen tool'lar

| Tool | Değişiklik |
|---|---|
| `admin_create_product` | Ürün CANLI doğar (taslak yok); fiyat≤0 Result hatası; response'tan `IsPublished` düşer |
| `admin_update_product` | "Yayın durumunu değiştirmez" koruması anlamsızlaşır/kalkar; response'tan `IsPublished` düşer |
| `admin_get_product` / `admin_list_products` | Response'tan `IsPublished` düşer |
| `search_products` / `get_product` (müşteri keşif) | Yayın filtresi kalkar — tüm ürünler görünür |
| `admin_republish_products` | Kalır; "yayındaki" süzgeci yerine tüm ürünleri backfill eder |

## Silinen HTTP yüzeyi

- `GET/POST /catalog-import/{token}` (capability-link yükleme ekranı) — 404 olur. Anayasa İlke V istisnasının yaşayan tek örneğiydi → istisna UYKUDA işaretlenir (PATCH amendment).
