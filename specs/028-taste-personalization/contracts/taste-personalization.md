# Contracts — Taste-vektör kişiselleştirme (Dilim-4)

> **REVİZYON (B):** gRPC İPTAL. Güncel: Personalization `get_my_taste` PERSONAL MCP tool (user JWT) →
> ScoutAgent → Catalog `recommend_books(float[] taste, int count)` ANONİM MCP tool. Aşağıdaki gRPC bölümü
> tarihsel/geçersiz; güncel kontrat ↓ "B — MCP" başlığında.

## B — MCP kontratı (güncel)

### Personalization `get_my_taste` (PERSONAL MCP tool, user JWT)
Yalnız ScoutAgent; token-forward ile user bearer → `currentUser` (013 deseni; `/mcp` RequireAuthorization).
**Girdi:** yok. **Çıktı:** `{ vector: float[] (boş=cold/sinyal yok), signalCount: int }`.
**Davranış:** çağıranın `UserSearchRecord` sorguları → OpenAI embed → ağırlıklı merkez. Vektör İlke VIII: yalnız S2S, loglanmaz.

### Catalog `recommend_books(float[] taste, int count)` (ANONİM MCP tool)
**Davranış:** boş taste → küratörlü (popülerlik browse). Dolu → `product_embedding <=> taste` kNN + hard filtre
(satılabilir/dil/seviye) + `DISTINCT ON WorkKey` + çeşitlilik → LIMIT. Çıktı: `{items,total}` (kart şekli).

### ScoutAgent `GET /api/chat/v1/feed` (login)
get_my_taste (token forward) → signalCount<3/boş ise recommend_books([]) değilse recommend_books(vector) → kartlar.
Anonim → 401. Vektör ScoutAgent'ta geçici, loglanmaz.

---

## (TARİHSEL) gRPC: `TasteQuery.GetTaste` — B ile İPTAL, referans için bırakıldı

Proto: `src/others/Shared/Protos/taste_vector.proto`, `csharp_namespace = Shared.Grpc.Personalization`.

```proto
service TasteQuery {
  // Yetki: personalization.read scope (Catalog makine token'i; Basket GetBasketItems deseni).
  rpc GetTaste (GetTasteRequest) returns (TasteReply);
}
message GetTasteRequest { string user_id = 1; }   // Guid
message TasteReply { repeated float vector = 1; int32 signal_count = 2; }  // vector boş = cold/sinyal yok
```

**Server (Personalization) davranış:** userId → `UserSearchRecord` sorguları (distinct) → OpenAI embed → ağırlıklı
merkez. Sinyal yoksa boş vector + signal_count=0. S2S scope-korumalı (Catalog makine token'i).

## MCP tool: `recommend_books` (Catalog, agent-içi, query-only, PERSONAL)

ScoutAgent tüketir; **token forward** ile kullanıcı bearer'ı taşınır → Catalog userId çözer (search_books opsiyonel-JWT deseni).

**Girdi:** `count` (int, varsayılan 10, en çok 20). userId forward edilen token'dan (arg değil).
**Çıktı:** `{ items: BookItem[], total }` (search/similar/semantic kart şekli).

**Davranış:**
1. Token'dan userId; yoksa (anonim) → küratörlü (popülerlik browse) döndür.
2. gRPC `GetTaste(userId)` → (vector, signalCount).
3. `signalCount < 3` ya da vector boş → **küratörlü** (çok-satan/popülerlik; 018 browse yolu).
4. Değilse kNN: `product_embedding <=> taste` + hard filtre (satılabilir/dil/seviye) + `DISTINCT ON WorkKey`
   + çeşitlilik → LIMIT count. Taste-vektör `::vector` CAST (VectorLiteral; 025/026 deseni).

## REST: `GET /api/chat/v1/feed` (ScoutAgent, login şart)

**Davranış:** login kullanıcı → `recommend_books` MCP (token forward) → `ChatResponse` benzeri `{reply:{text, items}}`
(React mevcut kart parse'ı). Anonim → 401 (feed kişisel; `RequireAuthorization`). Cevap `reply.items[].tool="recommend_books"`.

React: `RecommendBooksResult` kart dağıtıcısında (search_books case'ine eklenir — `{items}` aynı şekil).

## Integration / değişmeyen (regresyon kalkanı)
- `search_books` / `semantic_search` / `similar_books` / REST `/products/search` — DOKUNULMAZ (spesifik sorgu yolları).
- `UserSearched` (012) — taste'in sinyal kaynağı; değişmez.
- Taste-vektör yalnız gRPC'de (opak); hiçbir REST/MCP/log yüzeyine çıkmaz (İlke VIII).
