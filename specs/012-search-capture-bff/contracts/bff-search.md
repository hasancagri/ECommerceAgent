# Contract: BFF Arama Endpoint'i (istemci yüzeyi)

## Rota

- `POST /api/discovery/search` (gateway `/api/discovery/**` → bff cluster).
- Gövde: `{ query, mode?, limit? }` (mode: keyword|semantic|hybrid, default keyword).
- Auth: **opsiyonel** bearer. Token varsa login (userId çözülür); yoksa anonim.

## Davranış

1. BFF, Catalog arama yüzeyini çağırır (`/api/catalog/products/search`, service discovery) → `ProductSummary[]`.
2. Sonucu istemciye **aynen** döner (şekil/sıra Catalog ile birebir).
3. **Login ise** `UserSearched {userId, query, mode, resultCount=sonuç uzunluğu, searchedAt=now}` yayınlar (fanout). Anonim → yayınlamaz.
4. Kayıt/publish arama yanıtını **bloklamaz**; publish hatası arama sonucunu etkilemez (FR-006).

## Çıktı

- `200` + `ProductSummary[]` (boş liste dahil). Hata → Catalog hatası yansıtılır; publish hatası sessiz (loglanır), sonuç yine döner.

## Değişmezler

- Catalog'a dokunulmaz (public REST istemcisi). BFF durumsuz. Anonim arama açık (keşif login istemez).
