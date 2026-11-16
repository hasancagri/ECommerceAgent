# Contract: Arama Geçmişi REST API (013)

Personalization BC'nin İLK HTTP yüzeyi. Tümü gateway'den (`/api/personalization/**` → personalization cluster). Tümü **auth zorunlu** (anonim → 401); kimlik JWT `sub`'tan, veri daima o kullanıcıya filtreli. Named scope YOK.

Taban: `/api/personalization/v1/search-history` (URL-segment sürümleme, conventions).

## GET /api/personalization/v1/search-history

Kullanıcının tekilleştirilmiş, sayfalı arama geçmişi (en yeni üstte).

**Query params**: `page` (1-tabanlı, default 1), `pageSize` (default 20).

**200 OK** — `FeaturePagedResultModel<SearchHistoryEntry>`:
```json
{
  "data": [
    { "query": "Dostoyevski", "mode": "keyword", "lastSearchedAt": "2026-11-16T19:46:09Z", "count": 5 }
  ],
  "page": 1, "pageSize": 20, "totalCount": 2
}
```
`normalizedQuery` DIŞA sızmaz. Boş geçmiş → boş `data`, `totalCount:0` (hata değil).

**401** — anonim/geçersiz token.

## DELETE /api/personalization/v1/search-history/item?query={rawQuery}

Verilen sorgunun (normalize eşleşen) TÜM ham kayıtlarını kullanıcı için KALICI siler.

**Query param**: `query` (ekranda görünen ham sorgu; sunucu normalize edip eşleştirir).

**200 OK** — `FeatureResultModel` (silinen satır sayısı opsiyonel mesajda). Eşleşme yoksa da başarı (idempotent).

**401** — anonim.

## DELETE /api/personalization/v1/search-history

Kullanıcının TÜM geçmişini KALICI siler (clear-all). İstemci onayı UI'da.

**200 OK** — `FeatureResultModel`.

**401** — anonim.

## Notlar

- Silme hard (geri getirilemez); yeniden aranırsa yeni kayıt (012 yolu).
- Tekrar-ara ucu YOK — frontend aksiyonu (React → Catalog `/api/catalog/products/search`, 012).
- Yetki: `.RequireAuthorization()` (authenticated); `userId = CurrentUser.Load(http.User).Id`, boşsa 401 (guard zaten authorization'da).
