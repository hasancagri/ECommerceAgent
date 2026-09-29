# Stock — Domain Süreci

**BC ne yapar:** Her ürünün fiziksel stoğunun (**OnHand**) tek otoritesidir. Catalog'un ürün-bağ
olayından ilk OnHand'i yazar; checkout anında stoğu doğrudan düşer (056 — rezervasyon yok).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

> **050 pivot notu:** Feed (Procurement) söküldü; OnHand'i besleyen kanonik-ürün olayı kalktı.
> İlk stok `ProductAdded` ile gelir (051: ilk yayıncı kitap import); sonraki güncellemeler ürün-CRUD yoluyla.

## Süreç

1. **Ürün ilk eşlendiğinde barkod↔ProductId eşlemesi kurulur** ve      `(CatalogConsumers`
   OnHand başlangıç değeriyle yazılır (idempotent upsert).             ` → ProductAdded)`
2. **Stok her değişiminde vitrine bildirilir** — Storefront read-      `(IntegrationEvents`
   model'i güncel OnHand'i alır.                                       ` .StockChangedEvent)`
3. **Checkout stoğu doğrudan düşer (056).** Saga'nın broker komutu;    `(CommitStockCommand`
   `OnHand >= adet` ise düş, değilse reddet — oversell imkânsız.       ` → ProductStock.Commit)`
4. **Saga iptalinde commit edilmiş adet stoğa geri eklenir** (telafi). `(RevertCommitStockCommand`
   Yalnız daha önce commit edilmiş sipariş geri alınabilir.            ` → ProductStock.RevertCommit)`
5. **Admin stoğu mutlak SET eder (058)** — "stok N olsun"; SET         `(AdminSetStock`
   semantiği delta'dan ayrı, negatif reddedilir.                       ` → ProductStock.SetQuantity)`
6. **Admin stoğu artı/eksi delta ile oynatır (058).** +/- düzeltme;    `(AdminAdjustStock`
   sıfır delta ve negatife düşüş reddedilir.                           ` → ProductStock.Adjust)`

## Domain kuralları (süreci yöneten değişmezler)

- **OnHand otoritesi = Stock (050).** İlk OnHand `ProductAdded`'ten mutlak yazılır; sonraki güncelleme admin düzeltmeleriyle (058: mutlak set + artı/eksi delta). Negatife düşüş her iki yolda da reddedilir `(ProductStock.SetQuantity` / `ProductStock.Adjust)`.
- **Rezervasyon yok (056).** Sepet stok tutmaz; stok gerçeğinin tek anı checkout düşümü. Available ≡ OnHand.
- **Commit/Revert idempotent (028).** orderId anahtarıyla mükerrer teslimat no-op; commit'siz revert reddedilir `(_processedOps)`.
- **Eksiye düşüş imkânsız.** `Commit` yeterlilik guard'ıyla korunur; son-ürün yarışında ilk tamamlanan checkout kazanır, ikincisi reddedilir.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/kimlik/fiyat üretmez (Catalog otoritesi). Sepet/sipariş/ödeme sahibi değil — yalnız
stok düşüm/geri-alma hizmeti verir. Fiziksel depo/lojistik kapsam dışı.