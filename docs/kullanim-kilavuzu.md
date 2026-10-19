# Kullanım Kılavuzu

Bu mağazanın vitrini, kasası ve raflarıyla konuşan tek muhatap **senin kendi AI asistanın**dır.
Bu kılavuz "uygulamayı açtım, bomboş — şimdi ne yapacağım?" sorusunun cevabıdır.

## 1. Bu mağaza nedir?

- Ekranı, menüsü, sepet ikonu olmayan bir **kitapçı**. Alışverişin tamamı sohbetle yürür.
- "Uygulama" dediğimiz şey kendi AI istemcin (ör. Claude Desktop). Mağaza ona bağlanır,
  asistanın senin adına arar, sepete atar, sipariş verir — **her kalıcı işlemde önce senden onay alır**.
- Yazmak yeter: "merhaba" bile desen asistan mağazayı tanıtır ve ne yapabileceğini anlatır.

## 2. Kurulum (kuran kişi için)

1. Claude Desktop → **Settings → Connectors → Add custom connector**.
2. Mağaza adresini gir: `https://<magaza-adresi>/mcp` (tek giriş noktası; mağazanın duyurduğu URL).
3. İlk bağlantıda tarayıcı açılır → mağaza hesabıyla **giriş + izin onayı** (tek seferlik).
   Hesap yoksa aynı ekrandan kayıt olunur; kayıt otomatik "müşteri" yetkisi verir.
4. Bitti. Kullanacak kişiye tek cümle öğret: **"Bir şey yaz, gerisini asistan anlatır."**

## 3. Neler yapabilirsin?

Her başlıkta örnek cümle var — aynen yazabilirsin. Parantezdeki adlar teknik meraklısı için
asistanın kullandığı araçlardır; bilmen gerekmez.

### Kitap keşfi ve arama
> "Bilim kurgu klasiklerinden, 300 TL altı, stokta olan bir şeyler öner."

Vitrin araması doğal dille çalışır; indirim, stok ve okur puanını birlikte görür
(`query_storefront`). Katalog detayı da sorulur: yazar, yayınevi, kategori listeleri,
tek kitabın künyesi, fiyat geçmişi, stok adedi (`search_products`, `get_product`,
`get_price_history`, `list_categories`, `list_authors`, `list_publishers`, `get_stock`).

### Sepet
> "Şu ikinciyi sepete ekle." · "Sepetimde ne var?" · "Onu çıkar, berikinden 2 tane olsun."

(`add_to_cart`, `get_basket`, `update_basket_quantity`, `remove_basket_item`)

### Sipariş ve ödeme
> "Sepeti onayla, Üsküdar'daki adresime gönder." · "Siparişim ne durumda?"

Ödeme güvenli sayfada tamamlanır: asistan sana bir **ödeme linki** verir, kart bilgilerini
tarayıcıda o sayfaya girersin — **asistana asla kart numarası yazma, istemez de.**
(`place_order`, `start_payment`, `get_orders`, `get_my_payments`)

### Adres ve kart defteri
> "Yeni adres ekle: …" · "Varsayılan adresim Kadıköy'deki olsun." · "Kayıtlı kartlarımı say."

Kartlar yalnız görüntülenir (son 4 hane); kart ekleme/değiştirme ödeme sayfasının işidir.
(`list_addresses`, `add_address`, `update_address`, `remove_address`, `set_default_address`, `list_cards`)

### Yorum
> "Bu kitaba 5 yıldız ver, şunu da yaz: …" · "Okur yorumları nasıl?"

Yorum yazmak satın almış olmayı gerektirir; yorumun hemen yayınlanır.
(`get_reviews`, `check_review_eligibility`, `submit_review`)

### Fiyat alarmı
> "Bu kitap 200 TL altına düşerse bana mail at."

Fiyat düştüğünde e-posta gelir; alarmı sorabilir, kaldırabilirsin.
(`create_price_alarm`, `get_price_alarm`, `remove_price_alarm`)

### Oturum
> "Çıkış yap." (`logout`)

## 4. Güvenlik

- Asistan **yazma işlemlerinde** (sipariş, adres, yorum, alarm…) ne yapacağını söyler ve onayını bekler.
  Emin değilsen "iptal" de — hiçbir şey olmaz.
- Kart numarası (PAN) sistemde tutulmaz; ödeme, bankanın güvenli sayfasında biter.
- Asistan sana ürün/adres kodu (GUID) gibi iç kimlikler göstermez; kayıtlarından insan diliyle bahseder.

## 5. SSS

- **Ekran/site yok mu?** Yok; mağaza bilinçli olarak yalnız asistanla konuşur. Tek istisna ödeme sayfası.
- **Giriş her seferinde mi?** Hayır — ilk bağlantıda bir kez; oturum istemcide kalır.
- **Yanlış bir şey onaylarsam?** Sipariş öncesi her adım geri alınır ("sepetten çıkar" yeter).
  Sipariş sonrası için asistana "siparişimi iptal etmek istiyorum" de.
- **Fiyat düşünce haber verir mi?** Kendiliğinden değil — fiyat alarmı kurarsan mail gelir.
- **Asistanım yanlış fiyat söylerse?** Asistan fiyatı mağazadan okur ve aynen aktarmakla yükümlü;
  şüphede "fiyat geçmişini göster" de, kaynağı görürsün.
