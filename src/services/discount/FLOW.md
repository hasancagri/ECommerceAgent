# Discount — Domain Süreci

**BC ne yapar:** Admin-güdümlü **kampanya indirimi** yürütür: admin bir süzgeçle (kategori/yazar/yayınevi/
tek-kitap) yüzde indirim açar; sistem süzgeci kitap setine çözer, her kitaba **tek** indirim işler, vitrine
iter ve süre dolunca temizler. **Fiyat TUTMAZ** — yalnız yüzde otoritesi; etkin fiyatı tüketici hesaplar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Katalog ürün↔taksonomi bağını besler.** Catalog ürün değişimi          `(CatalogConsumers`
   ürün→kategori/yazar/yayınevi izdüşümünü upsert eder (süzgeç çözümü).      ` → ProductCatalogRef.Apply)`
2. **Admin süzgeçle kampanya açar.** Ad + süzgeç + yüzde + pencere;          `(CreateCampaign`
   invariant'lar (yüzde 1-99, bitiş>başlangıç, ad/scopeRef dolu,             ` → Campaign.Create)`
   scopeType tanımlı) doğrulanır.
3. **Aktifleşmede süzgeç kitaba çözülür + uygulanır.** startsAt≤now ise      `(CampaignSelectionResolver.Resolve;`
   hemen, gelecekse start-fire'da; her kitaba indirim yazılır (önceki        ` CampaignApplication.ActivateAsync`
   varsa ÜZERİNE — son-gelen-kazanır) ve itilir.                            ` → ProductDiscount.Create → ProductDiscountChanged)`
4. **Süre dayanıklı zamanlanır.** Başlangıç/bitiş per-kampanya              `(CampaignActivated / CampaignEnded`
   scheduled message; fire guard'lı idempotent (bayat mesaj no-op).         ` → CampaignScheduleHandler)`
5. **Bitiş/İptal kitapları temizler.** Kampanyanın kitaplarının indirimi     `(CampaignApplication.ClearAsync;`
   silinir, sıfır-yüzde itilir (vitrin liste fiyatına döner). Zaten iptal    ` CancelCampaign → Campaign.Cancel)`
   edilmiş / bulunamayan kampanya iptali hata (idempotency guard).
6. **Checkout canlı doğrular (S2S).** Order ödeme tutarını hesaplarken       `(DiscountQueryGrpcService.GetProductDiscounts`
   ürünlerin AKTİF yüzdesini gRPC ile sorar (pencere dışı = indirim yok).    ` → ProductDiscount.IsActiveAt)`

## Domain kuralları (süreci yöneten değişmezler)

- **Kitap başına TEK ETKİN indirim.** `ProductDiscount` PK=ProductId; yeni kampanya kitabın kaydını EZER
  (son-gelen-kazanır, atlama yok). Bitiş/iptal yalnız satırın hâlâ AİT olduğu kampanyada (CampaignId) temizler.
- **Snapshot süzgeç.** Süzgeç aktifleşme anında kitap setine çözülür; sonradan eklenen kitap otomatik girmez.
- **Fiyat tutulmaz.** Yalnız yüzde + pencere; etkin fiyatı tüketici (vitrin/checkout) kendi liste fiyatından hesaplar.
- **Süre = ödeme anı geçerliliği.** Sepette grace yok; `Campaign.IsEffectiveAt` / `ProductDiscount.IsActiveAt` pencereye bakar.
- **Gelecek kampanya slot tutmaz.** Scheduled kampanya aktif olana dek `ProductDiscount` yazmaz.
- **Zengin aggregate (İLKE II).** `Campaign` `AggregateRoot`'tan türer; invariant + geçişler metotlarda.
- **İzole BC (İLKE I).** Kendi `discountDb`'si; dışarı yalnız fanout event (Storefront) + gRPC (checkout).

## Sınır (bu BC'nin dokunmadığı)

Liste fiyatı + ürün künyesi (Catalog); vitrin gösterimi + etkin fiyat hesabı (Storefront); ödeme + sipariş
tutarı bağlama (Order/Payment); stok. `ProductCatalogRef` senkronu (Catalog→Discount) gecikirse süzgeç 0
kitaba çözebilir — sync tutarlılığı Catalog'un sorumluluğu. Kupon/sabit-tutar/en-iyi-kazanır v1 dışı.
Discount indirim yüzdesini ve penceresini yönetir; para hareketi ya da fiyat hesabı yapmaz.
