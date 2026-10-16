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
3. 🟦 **Stok kalemini düş** ⇦ Checkout — sağanın broker komutu; checkout stoğu                `(CommitStockCommand`
   doğrudan düşer (056): `OnHand >= adet` ise düş, değilse reddet — oversell imkânsız.         ` → ProductStock.Commit)`
4. 🟪 **Saga iptalinde telafi** → 🟦 **Commit'i geri sar** ⇦ Checkout — commit                `(RevertCommitStockCommand`
   edilmiş adet stoğa geri eklenir; yalnız daha önce commit edilmiş sipariş                    ` → ProductStock.RevertCommit)`
   geri alınabilir.
5. 👤 **Admin** → 🟦 **Stoğu mutlak SET et** — "stok N olsun" (058); SET                      `(AdminSetStock`
   semantiği delta'dan ayrı, negatif reddedilir.                                               ` → ProductStock.SetQuantity)`
6. 👤 **Admin** → 🟦 **Stoğu artı/eksi delta ile oynat** — +/- düzeltme (058);                `(AdminAdjustStock`
   sıfır delta ve negatife düşüş reddedilir.                                                   ` → ProductStock.Adjust)`

## Domain kuralları (süreci yöneten değişmezler)

- **OnHand otoritesi = Stock (050).** İlk OnHand `ProductAdded`'ten mutlak yazılır; sonraki güncelleme admin düzeltmeleriyle (058: mutlak set + artı/eksi delta). Negatife düşüş her iki yolda da reddedilir `(ProductStock.SetQuantity` / `ProductStock.Adjust)`.
- **Rezervasyon yok (056).** Sepet stok tutmaz; stok gerçeğinin tek anı checkout düşümü. Available ≡ OnHand.
- **Commit/Revert idempotent (028).** orderId anahtarıyla mükerrer teslimat no-op; commit'siz revert reddedilir `(_processedOps)`.
- **Eksiye düşüş imkânsız.** `Commit` yeterlilik guard'ıyla korunur; son-ürün yarışında ilk tamamlanan checkout kazanır, ikincisi reddedilir.

## Sınır (bu BC'nin dokunmadığı)

Ürün içeriği/kimlik/fiyat üretmez (Catalog otoritesi). Sepet/sipariş/ödeme sahibi değil — yalnız
stok düşüm/geri-alma hizmeti verir. Fiziksel depo/lojistik kapsam dışı.
