# Discount — Domain Süreci (EventStorming)

**BC ne yapar:** Kitaplara yüzde-indirim kampanyaları tanımlar ve ürünün ETKİN oranı değiştikçe
duyurur. İndirim AYRI kavramdır: liste fiyatını EZMEZ (fiyat Catalog'da değişmez; indirimli fiyat
okuyanda hesaplanır). (003, dönüş — eski Discount 095'te sökülmüştü, tarihçe 079.)

> Duvar dili: 🟦 komut · 🟧 event (⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟥 hotspot · 🟩 read-model ·
> ⇨/⇦ BC-arası dikiş (hedef/kaynak BC). Satır sonu backtick `(…)` = koda köprü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; rename yapan anchor'ı da günceller (guard yok).

## Süreç

1. 🟦 **Kampanya tanımlanır** — 👤 admin tek ürüne oran (%1-%90) + zaman penceresi                `(Campaign.Create)`
   girer; bitiş > başlangıç. _(giriş yüzeyi yok — REST admin sonraki dilim.)_
2. 🟦 **Kampanya iptal edilir** — pencere bitmeden etkinlik düşer; kayıt silinmez                `(Campaign.Cancel)`
   (durum değişimi — geçmiş raporlama için durur).
3. 🟪 **Etkin oran çözülür** — üründe aynı anda EN FAZLA BİR etkin indirim:                      `(Campaign.IsActiveAt,`
   pencere içi ∧ iptal-değil; çakışanda SON OLUŞTURULAN kazanır (079 kuralı).                    ` CampaignSelection.ResolveActiveRate)`
4. 🟧⭐ Etkin oran değişti ⇨ Catalog _(planned — yürürlük süreci sonraki dilim)_ —
   başlama/bitme/iptal/ezilme anında ProductDiscountChanged (mutlak oran; null =
   indirim yok) yayılır; Catalog üründeki oran kopyasını günceller/temizler.
   Kontrat + tüketici HAZIR; yayını tetikleyecek CampaignWindowProcess ertelendi.

## Domain kuralları (süreci yöneten değişmezler)

- **Oran %1-%90.** Sıfır/negatif/aşırı oran reddedilir `(Campaign.Create)` — tam-bedava/negatif fiyat kazası engeli.
- **Pencere geçerli.** Bitiş > başlangıç; etkinlik = pencere içi ∧ iptal-değil `(Campaign.IsActiveAt)`; zaman DIŞARIDAN verilir (domain saat okumaz).
- **Üründe ≤1 etkin indirim; son-gelen-kazanır.** Karar seçim helper'ında `(CampaignSelection.ResolveActiveRate)` — aggregate diğer kampanyaları bilmez.
- **Fiyat ezilmez.** İndirimli fiyat hiçbir yerde saklanmaz; Catalog kopyası yalnız ORAN taşır, okuyan hesaplar.
- **Satışa-uygunluğa etkisiz.** İndirim IsSalable kriterine girmez (Catalog kuralı).

## Sınır (bu BC'nin dokunmadığı)

Liste fiyatı/künye (Catalog), stok (Stock), sipariş tutarı hesabı (checkout bağlantısı sonraki dilim).
Kupon/sepet-indirim kavramı yok — yalnız ürün kampanyası.