# Quickstart: Checkout Choreography Doğrulama

Önkoşul: `dotnet build` + `dotnet test` yeşil. Sistem Aspire'dan kaldırılır:
`dotnet run --project src/aspire/AppHost/AppHost.csproj`. Checkout akışı dış MCP istemcisinden
(Claude Desktop) sürülür. Detay sözleşme: [contracts/integration-events.md](contracts/integration-events.md),
davranış: [data-model.md](data-model.md).

## S0 — Sistem sadeleşmesi (SC-003)
- Aspire dashboard'da `Checkout.Orchestrator` servisi YOK, hata YOK.
- `checkoutDb` Postgres kaydı YOK. Sistem hatasız açılır.

## S1 — Mutlu yol (SC-001)
1. Claude Desktop'tan sepete 2+ farklı ürün ekle (yeterli stoklu).
2. `start_payment` → hosted link → PG callback'i **success** ile taklit et (077 akışı).
3. Doğrula:
   - Her ürünün stoğu kalem adedince düştü (`get_stock` / `admin_list_all_stock`).
   - Sipariş `Confirmed`.
   - Sepet silindi (boş).
   - Reviews uygunluğu + Library satın-alma geçmişi oluştu (bugünküyle aynı).
4. Beklenen event izi: `PaymentSucceeded → StockCommitRequested → StockCommitted → OrderCompleted`.

## S2 — Stok yetersiz, atomik iptal (SC-002)
1. Sepete biri stoğu aşan çok kalem koy (ör. stok 1, sepet 3).
2. `start_payment` → callback **success**.
3. Doğrula:
   - Sipariş `Cancelled` (sebep `STOCK_INSUFFICIENT`).
   - **Hiçbir** ürünün stoğu değişmedi (atomik rollback — yeten kalem bile düşmedi).
   - Sepet KORUNDU (silinmedi).
   - Log'da "ödendi-stok yok, manuel iade" uyarısı.
4. Beklenen iz: `PaymentSucceeded → StockCommitRequested → StockCommitFailed → Order.Cancel`.

## S3 — İdempotency
- Aynı `PaymentSucceeded`/`StockCommitRequested` iki kez teslim edilse sonuç değişmez
  (stok tekrar düşmez, sipariş tekrar onaylanmaz). Marten/Wolverine at-least-once altında güvenli.

## S4 — Operatör görünürlüğü, watchdog yok (SC-004)
1. Stock tüketicisinde geçici hata simüle et (ör. DB/RabbitMQ kısa kesinti).
2. Wolverine retry merdiveni tükenince mesaj Stock DLQ'suna düşer.
3. `admin_list_dead_letters` → mesaj görünür; takılı sipariş durum sorgusunda `Pending`.
4. `admin_replay_dead_letter` → akış kaldığı yerden idempotent tamamlanır.

## S5 — Guard'lar + testler (SC-005)
```bash
dotnet build && dotnet test
scripts/check-claude-spec-links.sh     # checkout origin spec'i haritadan çıktı → yeşil
scripts/check-flow-links.sh            # order/stock/basket FLOW.md anchor'ları güncel → yeşil
```
Yeni domain testleri (test-first): `ProductStock.Commit` yeterli/yetersiz/idempotent davranışı.
Batch atomikliği (tek tx ya-hep-ya-hiç) host gerektirir → S2 ile canlı doğrulanır (İlke VI kapsam dışı).
