# Discount — Domain Süreci (EventStorming)

**BC ne yapar:** Admin-güdümlü **kampanya indirimi** yürütür: admin bir süzgeçle (kategori/yazar/yayınevi/
tek-kitap) yüzde indirim açar; sistem süzgeci kitap setine çözer, her kitaba **tek** indirim işler, vitrine
iter ve süre dolunca temizler. **Fiyat TUTMAZ** — yalnız yüzde otoritesi; etkin fiyatı tüketici hesaplar.

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. 🟧 **Ürün değişti** ⇦ Catalog → 🟪 **taksonomi izdüşümü beslenir** — Catalog ürün        `(CatalogConsumers`
   değişimi ürün→kategori/yazar/yayınevi izdüşümünü upsert eder (süzgeç çözümü).             ` → ProductCatalogRef.Apply)`
2. 🟦 **Kampanya aç** → 🟨 **Kampanya doğar** — admin ad + süzgeç + yüzde + pencere          `(CreateCampaign`
   verir; invariant'lar (yüzde 1-99, bitiş>başlangıç, ad/scopeRef dolu, scopeType            ` → Campaign.Create)`
   tanımlı) doğrulanır.
3. 🟪 **Aktifleşmede süzgeç kitaba çözülür** → 🟦 **İndirimi uygula** →                      `(CampaignSelectionResolver.Resolve;`
   🟧⭐ **Ürün indirimi değişti** ⇨ Storefront — startsAt≤now ise hemen, gelecekse            ` CampaignApplication.ActivateAsync`
   start-fire'da; her kitaba indirim yazılır (önceki varsa ÜZERİNE —                         ` → ProductDiscount.Create → ProductDiscountChanged)`
   son-gelen-kazanır) ve vitrine itilir.
4. 🟪 **Süre dayanıklı zamanlanır** — başlangıç/bitiş per-kampanya scheduled                 `(CampaignActivated / CampaignEnded`
   message; fire guard'lı idempotent (bayat mesaj no-op).                                    ` → CampaignScheduleHandler)`
5. 🟪 **Bitiş/iptal kitapları temizler** → 🟦 **İndirimi kaldır** — kampanyanın              `(CampaignApplication.ClearAsync;`
   kitaplarının indirimi silinir, sıfır-yüzde vitrine itilir ⇨ Storefront (vitrin            ` CancelCampaign → Campaign.Cancel)`
   liste fiyatına döner). Zaten iptal edilmiş / bulunamayan kampanya iptali hata
   (idempotency guard).
6. 🟦 **Aktif yüzdeyi sorgula** ⇦ Order — Order ödeme tutarını hesaplarken                   `(DiscountQueryGrpcService.GetProductDiscounts`
   ürünlerin AKTİF yüzdesini gRPC ile sorar (pencere dışı = indirim yok).                    ` → ProductDiscount.IsActiveAt)`

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
