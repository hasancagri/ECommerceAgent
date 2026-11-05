# REST Kontratı — Admin Excel Import (Catalog.Api, gateway üzerinden)

Tüm uçlar admin sınıfı: gateway `X-Admin-Api-Key` doğrular; servis de aynı anahtarı custom auth şemasıyla doğrular (çift kat). Anahtar değeri yanıt/log'a yazılmaz (İlke VIII).

## POST /api/admin/catalog/imports
- Gövde: multipart/form-data, `file` = .xlsx
- 202 Accepted → `{ "jobId": "<guid>" }` (iş kuyruğa alındı)
- 400: dosya yok / .xlsx değil / beklenen sütunlar eksik (hiçbir satır işlenmez)
- 409: aktif iş varken ikinci istek (tek-iş kuralı, FR-008)

## GET /api/admin/catalog/imports/{jobId}
- 200 → `{ "status": "Queued|Running|Completed|Failed", "processed": n, "added": n, "skipped": n, "errored": n, "startedAt": "...", "finishedAt": "...|null" }`
- 404: bilinmeyen iş

## GET /api/admin/catalog/imports/{jobId}/errors
- 200 → `[ { "sheet": "Can Yayınları", "row": 142, "reason": "ISBN boş" }, ... ]` (sayfalı değil; hata listesi sınırlı tutulur)

Not: VSA — her uç kendi slice'ı (`Domains/ImportJobs/Features/Commands/StartImport.cs`, `Queries/GetImportStatus.cs`, `Queries/GetImportErrors.cs`) + `ImportJobEndpointExtension`.