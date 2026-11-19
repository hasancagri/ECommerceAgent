# Quickstart: ScoutAgent Router → Sub-Agent doğrulama

Ön koşul: Docker açık; AppHost user-secrets'ta `Parameters:scout-openai-key` var. Sistem Aspire'dan
kaldırılır: `dotnet run --project src/aspire/AppHost/AppHost.csproj`. Doğrulama **elle** (İlke VI —
host/entegrasyon harness yok). Her senaryoda scout-agent log'u (Aspire dashboard) + chat yanıtı gözlenir.

## Build + unit
```bash
dotnet build
dotnet test tests/ScoutAgent.Api.Tests/ScoutAgent.Api.Tests.csproj
```
Beklenen: 0 hata; saf unit testler (eşleme/model-çözümleme/tool-kümesi/merge-sırası) yeşil.

## S1 — Niyete göre seçici bağlanma (US1 / SC-001)
Anonim chat: `POST /api/chat/v1/messages { "message": "Dostoyevski'nin ucuz kitapları" }`.
- **Beklenen yanıt**: kitap kartları (`reply.items` dolu), bugünküyle eşdeğer.
- **Beklenen log**: yalnız `catalog` `/mcp` ucuna bağlantı/`ListTools`; `reviews/basket/order/library/
  customer/personalization` uçlarına **bağlantı YOK** (SC-001).

## S2 — Kitap detay niyeti (US1)
`{ "message": "Suç ve Ceza kaç sayfa" }` → yalnız `catalog` (get_book) devreye girer, künye döner.

## S3 — Giriş gerektiren alan (US3 / SC-004)
Bearer token ile: `{ "message": "sepetimde ne var" }`.
- **Beklenen**: yalnız `basket` ucu, token taşınır, sepet kartı döner.
- Aynı istek **tokensız** → kişisel veri YOK; nazik giriş yönlendirmesi (SC-004, uydurma yok).

## S4 — Agent-başına model (US2 / SC-003)
Config: `RouterModel` = ucuz model, `Agents:catalog` = farklı model. Bir kitap araması turunda
log/trace'te router çağrısı ≠ catalog sub-agent çağrısı modeli.

## S5 — Dayanıklılık (SC-005)
Bir BC'yi durdur (ör. basket-api), girişli `{ "message": "sepetim" }` → tur yine tamamlanır; teknik
hata sızmaz; nazik yanıt. Diğer alanlar etkilenmez.

## S6 — Çok-alanlı (Edge / FR-010)
Girişli `{ "message": "sepetim ve Dostoyevski kitapları" }` → hem basket hem catalog devreye girer;
`reply.items` iki alanın kartlarını öngörülebilir sırayla taşır.

## S7 — Kalıcılık/sadakat regresyonu (FR-009)
Girişli sohbet: mesaj + kartlar kaydedilir; sohbet yeniden açılınca kartlar AYNEN gelir (017).
`UserSearched` yalnız login+dolu-sorguda yayılır (012/013 invariant korunur).

## Kabul
Tüm SC'ler (SC-001..005) S1-S7 ile gözlemlenir. D4 spike sonucu (tipli çıktı mı fallback mı) S1/S2'de
`items` yapısının bozulmadığı doğrulanarak kapatılır.
