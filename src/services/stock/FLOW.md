# Stock — Domain Süreci (EventStorming)

**BC ne yapar:** Her ürünün fiziksel stoğunun (**OnHand**) tek otoritesidir. Catalog'un ürün-bağ
olayından ilk OnHand'i yazar; checkout anında stoğu doğrudan düşer (056 — rezervasyon yok).

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 👤 aktör ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **050 pivot notu:** Feed (Procurement) söküldü; OnHand'i besleyen kanonik-ürün olayı kalktı.
> İlk stok `ProductAdded` ile gelir (051: ilk yayıncı kitap import); sonraki güncellemeler ürün-CRUD yoluyla.

## Süreç

1. 🟧 **Ürün eklendi** ⇦ Catalog → 🟨 **Stok kaydı doğar** — ürün ilk eşlendiğinde            `(CatalogConsumers`
   barkod↔ProductId eşlemesi kurulur ve OnHand başlangıç değeriyle yazılır                     ` → ProductAdded)`
   (idempotent upsert).
2. 🟪 **Stok her değişiminde vitrine bildirilir** → 🟧 **Stok değişti** ⇨ Storefront —        `(IntegrationEvents`
   Storefront read-model'i güncel OnHand'i alır.                                               ` .StockChangedEvent)`
3. 🟦 **Sipariş stoğunu commit et** ⇦ Order → 🟧 **Stok commit edildi / yetersiz** ⇨ Order — `(OrderConsumers →`
   siparişin TÜM kalemleri TEK transaction'da atomik düşülür (092 koreografi):               ` ProductStock.Commit)`
   `OnHand >= adet` hepsinde sağlanırsa düşülür + `StockCommitted`; biri yetersizse
   HİÇBİRİ düşmez (ya hep ya hiç) + `StockCommitFailed`. Oversell imkânsız.
4. 👤 **Admin** → 🟦 **Stoğu mutlak SET et** — "stok N olsun" (058); SET                      `(AdminSetStock`
   semantiği delta'dan ayrı, negatif reddedilir.                                               ` → ProductStock.SetQuantity)`
5. 👤 **Admin** → 🟦 **Stoğu artı/eksi delta ile oynat** — +/- düzeltme (058);                `(AdminAdjustStock`
   sıfır delta ve negatife düşüş reddedilir.                                                   ` → ProductStock.Adjust)`

## Domain kuralları (süreci yöneten değişmezler)

- **OnHand otoritesi = Stock (050).** İlk OnHand `ProductAdded`'ten mutlak yazılır; sonraki güncelleme admin düzeltmeleriyle (058: mutlak set + artı/eksi delta). Negatife düşüş her iki yolda da reddedilir `(ProductStock.SetQuantity` / `ProductStock.Adjust)`.
- **Rezervasyon yok (056).** Sepet stok tutmaz; stok gerçeğinin tek anı checkout düşümü. Available ≡ OnHand.
- **Atomik batch commit (092).** Siparişin kalemleri tek transaction'da düşülür; biri yetersizse hiçbiri yazılmaz → geri-alma/telafi (RevertCommit) GEREKMEZ, söküldü. `Commit` orderId anahtarıyla idempotent (mükerrer teslimat no-op) `(_processedOps)`.
- **Eksiye düşüş imkânsız.** `Commit` yeterlilik guard'ıyla korunur; son-ürün yarışında ilk tamamlanan checkout kazanır, ikincisi reddedilir (`StockCommitFailed`).

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/kimlik/fiyat üretmez (Catalog otoritesi). Sepet/sipariş/ödeme sahibi değil — yalnız
stok düşüm hizmeti verir. Fiziksel depo/lojistik kapsam dışı.
