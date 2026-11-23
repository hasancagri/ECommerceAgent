# Stock — Domain Süreci (EventStorming)

**BC ne yapar:** Kitapların kurgusal envanterini tutar (ürün başına tek stok kaydı) ve her adet
değişimini duyurur. Adedin TEK yazarıdır — başka hiçbir BC stok yazamaz. (002, fiziki kitaba dönüş;
eski Stock BC dijital pivotta sökülmüştü, rezervasyon modeli GERİ GELMEDİ.)

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🟩 read-model ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; rename yapan anchor'ı da günceller (guard yok).

## Süreç

1. 🟦 **Stok kaydı açılır / adet atanır** — 👤 admin ürüne mutlak adet girer                `(StockItem.Create,`
   (0 geçerli: tükenmiş kitap kayıtla doğabilir); ürün başına TEK kayıt                    ` StockItem.SetQuantity)`
   (DB-seviyesi teklik). _(giriş yüzeyi yok — REST admin sonraki dilim.)_
1b. 🟪 **Ürün doğunca stok seed** (010) ⇦ Catalog — `ProductAdded` tüketilir →            `(CatalogConsumers,`
   adet=rastgele 30-50 (consumer'da üretilir, demo/eğitim verisi — Excel'de gerçek            ` StockItem.Create)`
   adet yok) → `StockItem.Create(productId, adet)` → `StockChanged` yayılır ⇨ Catalog.
   Rastgele ALTYAPIDA; aggregate deterministik (İlke VI). Ürün başına tek kayıt (idempotent).
   _(019: event'teki InStock sinyaline göre ayrışır — false (Kitapyurdu "SATIŞ YOK") → adet 0
   ile doğar; true → rastgele 20-50. Bayrak/adet çelişmez; gerçek adet yine ithal edilmez.)_
2. 🟦 **Satış düşümü** — sipariş akışı eldeki adedi düşer; yetersizse RED                  `(StockItem.Decrease)`
   (aşırı-satış engeli). _(checkout bağlantısı sonraki dilim — bugün çağıranı yok.)_
3. 🟧⭐ **Adet değişti** ⇨ Catalog — her başarılı atama/düşümde MUTLAK değerle              `(StockChanged)`
   yayılır; Catalog üründeki adet kopyasını günceller (IsSalable stok bacağı).
   Mutlak değer → tüketici idempotent (son değer kazanır).

## Domain kuralları (süreci yöneten değişmezler)

- **Adet ≥0.** Negatif atama ve yetersiz düşüm reddedilir; eldeki değer asla negatife inmez `(StockItem.SetQuantity, StockItem.Decrease)`.
- **Ürün başına tek kayıt.** `ProductId` computed unique index — DB-seviyesi guard.
- **Tek yazar Stock.** Adet yalnız bu BC'de yazılır; Catalog'daki `StockCount` event-fed KOPYADIR (bayatlayabilir; para alınan an otoriter düşüm burada).
- **Rezervasyon yok (056 sürer).** Sepete ekleme stok tutmaz; stok gerçeği sipariş anında düşülür.

## Sınır (bu BC'nin dokunmadığı)

Kitap künyesi/fiyat (Catalog), sipariş yaşam döngüsü (Order), indirim (Discount). Depo/lokasyon/
tedarik kavramı yok — envanter kurgusal.