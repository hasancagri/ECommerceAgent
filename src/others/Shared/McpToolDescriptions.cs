namespace Shared;

// MCP tool DESCRIPTION'larının TEK kaynağı (003 — doğal-dil routing standardı). McpToolNames.cs
// deseninin eşi: attribute'lar [Description(McpToolDescriptions.X.Y)] ile buraya referans verir,
// inline string TAŞIMAZ. Standart: eylem-önce cümle → en az bir Türkçe tetikleyici ifade
// ("'...' gibi istekler için") → opsiyonel girdi/çıktı → governance/PII kısıtı EN SONDA.
// Prose uyum-prefix'i (YONETIM/YAZMA:) YOK — yönetim sinyali admin_ isminden gelir. Büyük-harf
// bağırma YOK. Kanonik standart: AgentPlatform/docs/mcp-tool-description-standard.md.
// Sınıf yapısı McpToolNames ile birebir hizalı (aynı nested sınıf + üye adı).
// NOT: StorefrontTools.QueryStorefront BURADA DEĞİL — devasa, çalışan kontrol tool'u ve şema-drift
// guard'ı (check-agent-query-schema.sh) kendi dosyasını hedefliyor; description'ı orada const-composed
// kalır (bilinçli istisna; inline literal değil).

public static class McpToolDescriptions
{
    public static class CatalogTools
    {
        public const string GetProduct =
            "Ürünü isme göre arar ve sepete eklemeye yetecek bilgiyi (id, ad, fiyat, görsel) döndürür. " +
            "\"Şu kitabın fiyatı ne\", \"bunu sepete eklemek istiyorum\" gibi istekler için.";

        public const string SearchProducts =
            "Katalogda isme göre en iyi eşleşen ürünün productId ve adını döndürür. " +
            "\"Kitap ara\", \"şu kitabı bul\" gibi istekler için. Kategori ve/veya yazar adıyla daraltılabilir. " +
            "Not: link yok (mağaza ekransız).";

        public const string GetPriceHistory =
            "Bir ürünün geçmiş fiyat değişikliklerini (eski fiyat, yeni fiyat, tarih) kronolojik listeler. " +
            "\"Bu kitabın fiyatı değişti mi\", \"fiyat geçmişi\" gibi istekler için. " +
            "productId = search_products/get_product'tan dönen ürün kimliği.";

        public const string ListCategories =
            "Mağazadaki kategorileri (ad, üst kategori, ürün sayısı) listeler — yalnız yayında ürünü olanlar. " +
            "\"Kategorileri göster\", \"hangi kategoriler var\" gibi keşif istekleri için.";

        public const string ListAuthors =
            "Mağazadaki yazarları (yalnız yayında kitabı olanlar) kitap sayısı çok olan önce listeler. " +
            "\"Yazarları listele\", \"hangi yazarlar var\" gibi istekler için. totalCount toplam yazar sayısıdır; " +
            "liste kırpılmış olabilir — daraltmak için search ile ada göre filtrele.";

        public const string ListPublishers =
            "Mağazadaki yayınevlerini (yalnız yayında kitabı olanlar) kitap sayısı çok olan önce listeler. " +
            "\"Yayınevlerini göster\", \"hangi yayınevleri var\" gibi istekler için. totalCount toplam yayınevi " +
            "sayısıdır; daraltmak için search kullan.";
    }

    public static class BasketTools
    {
        public const string AddToCart =
            "Giriş yapmış kullanıcının sepetine bir ürün ekler. " +
            "\"Sepete ekle\", \"bunu sepetime at\" gibi istekler için. Girdi: ürün kimliği, ad, fiyat, opsiyonel görsel.";

        public const string GetBasket =
            "Giriş yapmış kullanıcının sepetini (ürünler, toplam fiyat) döndürür. " +
            "\"Sepetimi göster\", \"sepette ne var\" gibi istekler için.";

        public const string RemoveBasketItem =
            "Sepetten verilen Id'ye sahip ürünü çıkarır. " +
            "\"Şunu sepetten çıkar\", \"bu ürünü sepetten sil\" gibi istekler için. Girdi: sepet item Id'si (get_basket'ten).";

        public const string UpdateBasketQuantity =
            "Giriş yapmış kullanıcının sepetindeki bir ürünün adedini belirtilen mutlak değere günceller. " +
            "\"Adedi 3 yap\", \"bundan 2 tane olsun\" gibi istekler için. productId = get_basket'ten dönen ürün " +
            "kimliği; quantity 0 veya altıysa ürün sepetten çıkarılır (üst sınır 5). Yanıttaki 'message' alanını " +
            "kullanıcıya olduğu gibi ilet.";
    }

    public static class OrderTools
    {
        public const string GetOrders =
            "Giriş yapmış kullanıcının siparişlerini (kod, tarih, tutar, durum, ürünler) listeler. " +
            "\"Siparişlerimi göster\", \"geçmiş siparişlerim\" gibi istekler için.";

        public const string StartPayment =
            "Sepetteki ürünler için bir hosted ödeme bağlantısı oluşturur. " +
            "\"Ödeme yap\", \"sepeti satın al\", \"siparişi tamamla\" gibi istekler için. Parametre verme " +
            "(tutar/adres/ürün sunucu belirler). Kullanıcı bağlantıda ödemeyi tamamlayınca sipariş onaylanır. " +
            "Yanıttaki 'message' alanını kullanıcıya olduğu gibi ilet.";
    }

    public static class PaymentTools
    {
        public const string GetMyPayments =
            "Giriş yapmış kullanıcının ödemelerini (tutar, tarih, durum) listeler. " +
            "\"Ödemelerimi göster\", \"ödeme geçmişim\" gibi istekler için.";
    }

    public static class StockTools
    {
        public const string GetStock =
            "Bir ürünün stok durumunu (adet) döndürür; ürün Id'si ile sorgular. " +
            "\"Stok var mı\", \"kaç adet kaldı\" gibi istekler için.";
    }

    public static class CustomerTools
    {
        public const string ListAddresses =
            "Giriş yapmış kullanıcının kayıtlı adreslerini (adres alanları + varsayılan + adres kimliği) listeler. " +
            "\"Adreslerimi göster\", \"kayıtlı adreslerim\" gibi istekler için.";

        public const string AddAddress =
            "Giriş yapmış kullanıcıya yeni bir teslimat adresi ekler. " +
            "\"Yeni adres ekle\", \"adres kaydet\" gibi istekler için. Tüm alanlar zorunlu: province (il), " +
            "district (ilçe), street (cadde/sokak), zipCode (posta kodu), line (açık adres). Yanıttaki 'message' " +
            "alanını kullanıcıya olduğu gibi ilet.";

        public const string UpdateAddress =
            "Giriş yapmış kullanıcının mevcut bir adresini kısmi günceller: yalnız değiştirmek istediğin alanları " +
            "gönder, diğerlerini gönderme — verilmeyen alanlar mevcut değerinde aynen kalır. " +
            "\"Adresimi güncelle\", \"ilçeyi değiştir\" gibi istekler için. addressId = list_addresses'ten dönen " +
            "adres kimliği. Yanıt adresin güncel halidir; 'message' alanını kullanıcıya olduğu gibi ilet.";

        public const string RemoveAddress =
            "Giriş yapmış kullanıcının bir kayıtlı adresini siler. " +
            "\"Adresimi sil\", \"bu adresi kaldır\" gibi istekler için. addressId = list_addresses'ten dönen adres " +
            "kimliği. Yanıttaki 'message' alanını kullanıcıya olduğu gibi ilet.";

        public const string SetDefaultAddress =
            "Giriş yapmış kullanıcının varsayılan teslimat adresini belirler. " +
            "\"Varsayılan adresim şu olsun\", \"bunu varsayılan yap\" gibi istekler için. addressId = " +
            "list_addresses'ten dönen adres kimliği. Yanıttaki 'message' alanını kullanıcıya olduğu gibi ilet.";
    }

    public static class ReviewsTools
    {
        public const string GetReviews =
            "Bir ürünün görünür yorumlarını (maskeli ad, puan, metin, tarih) en yeni üstte listeler. " +
            "\"Yorumları göster\", \"bu kitabın değerlendirmeleri\" gibi istekler için. productId = " +
            "search_products/get_product'tan dönen ürün kimliği. page opsiyonel (varsayılan 1).";

        public const string CheckReviewEligibility =
            "Giriş yapmış kullanıcının bu ürüne yorum yapıp yapamayacağını (satın-alma şartı + tek-yorum) kontrol eder. " +
            "\"Yorum yapabilir miyim\", \"değerlendirme hakkım var mı\" gibi istekler için. productId = ürün kimliği. " +
            "canReview=false ise reasonCode nedeni verir.";

        public const string SubmitReview =
            "Giriş yapmış kullanıcının satın aldığı bir ürüne yorum + puan bırakır (ürün başına tek yorum). " +
            "\"Yorum yap\", \"puan ver\", \"değerlendirme bırak\" gibi istekler için. productId = ürün kimliği; " +
            "rating 1-5; text opsiyonel yorum metni. Görünen ad kullanıcının kimliğinden gelir (maskeli saklanır). " +
            "Yanıttaki 'message' alanını kullanıcıya olduğu gibi ilet.";
    }

    public static class LibraryTools
    {
        public const string GetPriceAlarm =
            "Giriş yapmış kullanıcının bu ürün için fiyat alarmı olup olmadığını döndürür. " +
            "\"Alarmım var mı\", \"fiyat alarmı durumu\" gibi istekler için. productId = search_products/get_product'tan.";

        public const string CreatePriceAlarm =
            "Giriş yapmış kullanıcı için bir ürüne fiyat alarmı kurar: fiyat düşünce kullanıcıya mail gider. " +
            "\"Fiyat alarmı kur\", \"ucuzlayınca haber ver\" gibi istekler için. productId/productName = " +
            "search_products/get_product'tan; currentPrice = ürünün şu anki fiyatı (referans). Kullanıcı başına " +
            "ürüne tek alarm. Yanıttaki 'message' alanını kullanıcıya olduğu gibi ilet.";

        public const string RemovePriceAlarm =
            "Giriş yapmış kullanıcının bir üründeki fiyat alarmını kaldırır. " +
            "\"Fiyat alarmını kaldır\", \"alarmı iptal et\" gibi istekler için. productId = ürün kimliği. " +
            "Yanıttaki 'message' alanını kullanıcıya olduğu gibi ilet.";
    }

    // ── Yönetim (admin_*) tool'ları — yönetim sinyali isimden gelir, prose prefix yok ──

    public static class CatalogAdminTools
    {
        public const string ListProducts =
            "Ürünleri sayfalı listeler — yayında olmayanlar (taslak) dahil. " +
            "\"Ürünleri listele\", \"taslak kitapları göster\", \"katalogdaki ürünler\" gibi istekler için. " +
            "Dönen her satır: productId (diğer admin tool'larının anahtarı), name, isbn, price (TL), isPublished, " +
            "authorNames; ayrıca totalCount + page + pageSize. Devamı için aynı aramayla page'i artır. " +
            "q='dune' ile ada göre ara; q bir ISBN ise tam eşleşme aranır.";

        public const string GetProduct =
            "Tek ürünün tam yönetim detayını döndürür (taslak dahil): künye (name, shortDescription, " +
            "fullDescription, sku, isbn, price, imageUrl), bağlar (authors ad+id, publisherId+publisherName, " +
            "categoryId+categoryName), isPublished ve fiyat değişiklik geçmişi — tek çağrıda. " +
            "\"Şu ürünün detayını göster\", \"ürün künyesi\" gibi istekler için. productId = admin_list_products'tan " +
            "dönen kimlik. imageUrl'i kullanıcıya tıklanabilir link olarak sun (markdown: [Kapak](url)).";

        public const string UpdateProduct =
            "Tek ürünün künyesini kısmi günceller — yalnız verdiğin alanlar değişir, diğerleri aynen kalır. " +
            "\"Fiyatı 95 yap\", \"ürünün başlığını değiştir\" gibi istekler için. authorIds verilirse yazar seti " +
            "onunla değişir; newAuthorNames listede olmayan yazar adlarını oluşturup ekler. publisherId ya da " +
            "newPublisherName ile yayınevi değişir; categoryId ile kategori taşınır. Fiyat değişimi fiyat geçmişine " +
            "kaydolur ve vitrine yansır. Yanıt ürünün güncel halidir — sonucu göstermek için ek çağrı gerekmez. " +
            "Toplu güncelleme yoktur; her ürün için ayrı çağrı yap. İşlem denetim izine kaydedilir.";

        public const string SetPublished =
            "Tek ürünü yayına alır (published=true) veya yayından kaldırır (published=false). " +
            "\"Bu kitabı yayına al\", \"ürünü yayından kaldır\" gibi istekler için. Yayından kalkan ürün " +
            "vitrinden/keşiften düşer ama silinmez — tekrar yayına alınabilir. Yanıt güncel {productId, isPublished}. " +
            "Not: fiyatsız ürün yayına alınamaz (iş kuralı hatası döner). İşlem denetim izine kaydedilir.";

        public const string GetPriceHistory =
            "Ürünün fiyat değişiklik geçmişini kronolojik listeler (taslak dahil): [{oldPrice, newPrice, changedAtUtc}]. " +
            "\"Bu ürünün fiyat geçmişi\", \"fiyat ne zaman değişti\" gibi istekler için. productId = " +
            "admin_list_products/admin_get_product'tan.";

        public const string CreateProduct =
            "Yeni kitap künyesi oluşturur (taslak — yayına almaz; ayrıca admin_set_published çağır). " +
            "\"Yeni kitap ekle\", \"ürün oluştur\" gibi istekler için. isbn kimliktir; aynı isbn zaten varsa hata " +
            "döner (güncelleme için admin_update_product). En az bir yazar (authorIds ya da newAuthorNames) + " +
            "yayınevi (publisherId ya da newPublisherName) + categoryId zorunlu; katalogda olmayan yazar/yayınevi " +
            "adları oluşturulur. Fiyat TL (>=0). İşlem denetim izine kaydedilir.";

        public const string SetProductDimensions =
            "Tek ürünün fiziksel ölçülerini ayarlar (ağırlık + boy x en x yükseklik). " +
            "\"Ürünün boyutlarını gir\", \"ağırlığını ayarla\" gibi istekler için. İşlem denetim izine kaydedilir.";

        public const string SetProductSeo =
            "Tek ürünün SEO üst-verisini ayarlar (metaTitle/metaKeywords/metaDescription; verilmeyen alan boş geçer). " +
            "\"Ürünün SEO bilgisini gir\", \"meta başlığını ayarla\" gibi istekler için. İşlem denetim izine kaydedilir.";

        public const string AssignProductTag =
            "Tek ürüne bir etiket atar. " +
            "\"Bu ürüne etiket ekle\", \"şu etiketi ata\" gibi istekler için. tagId = admin_list_product_tags'ten. " +
            "İşlem denetim izine kaydedilir.";

        public const string RemoveProductTag =
            "Tek üründen bir etiketi kaldırır. " +
            "\"Bu ürünün etiketini kaldır\" gibi istekler için. tagId = admin_get_product/admin_list_product_tags'ten. " +
            "İşlem denetim izine kaydedilir.";

        public const string CreateCategory =
            "Yeni bir kategori oluşturur (vitrinde yayında doğar). " +
            "\"Kategori oluştur\", \"yeni kategori ekle\" gibi istekler için. Aynı ad zaten varsa hata döner " +
            "(get-or-create değil) — önce list_categories ile kontrol et. parentId ile üst kategoriye bağlanır " +
            "(bulunamazsa hata). SEO alanları opsiyoneldir. Yanıt {id, name}. İşlem denetim izine kaydedilir.";

        public const string UpdateCategory =
            "Tek kategoriyi kısmi günceller — yalnız verdiğin alanlar değişir, diğerleri aynen kalır. " +
            "\"Kategori adını değiştir\", \"kategoriyi güncelle\" gibi istekler için. Güncellenebilir alanlar: ad ve " +
            "SEO meta (verilen SEO alanı üstüne yazılır, verilmeyen mevcut kalır). Yanıt kategorinin güncel halidir. " +
            "İşlem denetim izine kaydedilir.";

        public const string CreateAuthor =
            "Bir yazar kaydı oluşturur. " +
            "\"Yazar ekle\", \"yeni yazar oluştur\" gibi istekler için. Aynı ad zaten varsa yenisi oluşturulmaz — " +
            "mevcut yazar döndürülür (idempotent get-or-create), böylece ürün bağı kurmadan önce yazar kimliğini " +
            "güvenle alabilirsin. Yanıt {id, name}. İşlem denetim izine kaydedilir.";

        public const string CreateProductTag =
            "Yeni bir ürün etiketi oluşturur (ör. 'yeni-sezon', 'outlet'). " +
            "\"Etiket oluştur\", \"yeni etiket ekle\" gibi istekler için. Ad zorunludur. Yanıt {id, name}. " +
            "İşlem denetim izine kaydedilir.";

        public const string RenameProductTag =
            "Mevcut bir ürün etiketinin adını değiştirir. " +
            "\"Etiketi yeniden adlandır\", \"etiket adını değiştir\" gibi istekler için. tagId = " +
            "admin_list_product_tags'ten dönen kimlik. Ad zorunludur. Yanıt {id, name} — güncel hali. Etiket " +
            "bulunamazsa hata döner. İşlem denetim izine kaydedilir.";

        public const string ListProductTags =
            "Ürün etiketlerini ada göre sıralı listeler. " +
            "\"Etiketleri listele\", \"hangi etiketler var\" gibi istekler için. Dönen her satır {id, name}; id, " +
            "admin_rename_product_tag'in anahtarıdır. search ile etiket adında geçen metne göre daraltılabilir.";

        public const string CreateSpecificationAttribute =
            "Yeni bir kanonik özellik tanımı oluşturur (ör. 'Renk', 'Materyal'). " +
            "\"Özellik tanımı oluştur\", \"yeni özellik ekle\" gibi istekler için. filterable=true ise vitrin facet " +
            "filtresine girer. Aynı isimli tanım varsa hata döner. Değer listesini sonra " +
            "admin_add_specification_attribute_option ile eklersin. Yanıt yeni tanımın id'sidir. İşlem denetim izine kaydedilir.";

        public const string AddSpecificationAttributeOption =
            "Var olan bir özellik tanımına kapalı-liste değeri (seçenek) ekler (ör. Renk tanımına 'Siyah'). " +
            "\"Renk seçeneği ekle\", \"özelliğe değer ekle\" gibi istekler için. attributeId = " +
            "admin_list_specification_attributes'tan dönen tanım kimliği. Aynı isimli seçenek varsa hata döner. " +
            "Yanıt yeni seçeneğin optionId'sidir. İşlem denetim izine kaydedilir.";

        public const string ListSpecificationAttributes =
            "Tüm özellik tanımlarını kapalı-liste seçenekleriyle birlikte listeler. " +
            "\"Özellik tanımlarını göster\", \"hangi özellikler var\" gibi istekler için. Dönen her satır: id (diğer " +
            "tool'ların anahtarı), name, filterable, displayOrder ve options ([{id, name, displayOrder}]). Yeni " +
            "seçenek eklemeden önce tanım id'sini buradan al.";

        public const string RepublishProducts =
            "Tüm yayındaki ürünler için katalog değişiklik event'ini yeniden yayınlar. " +
            "\"Ürünleri yeniden yayınla\", \"downstream'i yeniden doldur\" gibi istekler için. Yeni bir downstream " +
            "read-model'i (ör. indirim kategori izdüşümü) doldurmak/onarmak için kullanılır. Tüm tüketiciler için " +
            "idempotent; fiyat alarmı tetiklemez.";

        public const string ImportCatalog =
            "Excel katalog import başlatır — çağrıldığında hemen süreli + tek kullanımlık yükleme ekranı linki üretir. " +
            "\"Katalog yükle\", \"excel'den ürün import et\" gibi istekler için. Yanıt {url, expiresAt, message}; " +
            "sohbete yalnız linki düşür. Yüklenen satırlar arka planda taslak ürüne dönüşür (yayın ayrı: " +
            "admin_publish_imported); ilerleme/sonuç için admin_get_import_status çağır. Not: kullanıcıdan xlsx " +
            "dosyasını isteme, onay bekleme, soru sorma — dosya tarayıcıdaki ekrandan yüklenir, sohbete girmez.";

        public const string PublishImported =
            "Excel import ile gelen, fiyatı > 0 olan taslak ürünleri toplu yayınlar (vitrine çıkarır). " +
            "\"Import edilenleri yayınla\", \"yüklenen taslakları yayına al\" gibi istekler için. Fiyatsız import " +
            "taslakları + elle oluşturulmuş (import-dışı) taslaklar etkilenmez. Yanıt " +
            "{publishedCount, skippedNoPriceCount}. Önce admin_import_catalog ile yükle, sonra bunu çağır.";

        public const string GetImportStatus =
            "Excel katalog import durumunu döndürür: {pending, processed, failed, failures}. " +
            "\"Import durumu ne\", \"yükleme bitti mi\" gibi istekler için. failures = başarısız satırların " +
            "{isbn, error} listesi (en fazla 200). Import ilerlemesini ve hatalı satırları görmek için kullanın.";
    }

    public static class DiscountAdminTools
    {
        public const string CreateCampaign =
            "Süzgeçle (kategori/yazar/yayınevi/tek-kitap) yüzde indirim kampanyası açar. " +
            "\"Kampanya oluştur\", \"indirim başlat\", \"şu kategoriye indirim yap\" gibi istekler için. Süzgeç " +
            "uygulama anında kitap setine çözülür; kitabın önceki indirimi varsa üzerine yazılır " +
            "(son-gelen-kazanır). startsAt boş = şimdi. Yanıt kısa özet döner (kaç kitap indirimli).";

        public const string CancelCampaign =
            "Kampanyayı iptal eder; o kampanyanın kitaplarının indirimi anında temizlenir. " +
            "\"Kampanyayı iptal et\", \"indirimi durdur\" gibi istekler için.";

        public const string ListCampaigns =
            "Kampanyaları listeler. " +
            "\"Kampanyaları göster\", \"aktif indirimler\" gibi istekler için. Opsiyonel status süzgeci: " +
            "Scheduled | Active | Ended | Cancelled.";
    }

    public static class StockAdminTools
    {
        public const string SetStock =
            "Tek ürünün stoğunu mutlak değere ayarlar (ör. 'stok 25 olsun' → quantity=25). " +
            "\"Stoğu 25 yap\", \"stok miktarını ayarla\" gibi istekler için. quantity >= 0 olmalı; negatif değer iş " +
            "kuralı hatasıyla reddedilir. Artır/azalt için admin_adjust_stock kullan. Yanıt güncel {productId, onHand}. " +
            "Ürünün stok kaydı yoksa bulunamadı döner (kayıt ürün yayınlanırken açılır). İşlem denetim izine kaydedilir.";

        public const string AdjustStock =
            "Tek ürünün stoğunu delta kadar oynatır — pozitif delta artırır ('3 ekle' → delta=3), negatif delta " +
            "azaltır ('3 azalt' → delta=-3). " +
            "\"Stoğa 3 ekle\", \"stoktan 2 düş\" gibi istekler için. Stoğu sıfırın altına düşürecek delta iş kuralı " +
            "hatasıyla reddedilir; delta=0 geçersizdir. Mutlak değer için admin_set_stock kullan. Yanıt güncel " +
            "{productId, onHand}. İşlem denetim izine kaydedilir.";

        public const string ListAllStock =
            "Tüm ürünlerin stok (OnHand) genel görünümü — {productId, onHand} listesi. " +
            "\"Tüm stokları göster\", \"stok durumu genel\" gibi istekler için. Opsiyonel sayfalama: page (1'den başlar) " +
            "+ pageSize; ikisi de verilmezse tüm kayıtlar döner. Salt-okuma, denetim izi bırakmaz. Tek ürün için get_stock kullan.";
    }

    public static class CustomerAdminTools
    {
        public const string GetMerchantStatus =
            "Ödeme gateway'i merchant kimliğinin durumunu döndürür: {configured, merchantId?, updatedAt?}. " +
            "\"Merchant kimliği tanımlı mı\", \"ödeme kimliği durumu\" gibi istekler için. configured=false ise kimlik " +
            "tanımsızdır — önce admin_start_onboarding ile kaydı başlat. " +
            "Not: MerchantKey (sır) hiçbir zaman dönmez.";

        public const string OnboardingStatus =
            "Ödeme gateway'indeki merchant başvurusunun durumunu sorgular (email = başvuruda kullanılan adres). " +
            "\"Başvurum onaylandı mı\", \"onboarding durumu\" gibi istekler için. Yanıt {status: None|Pending|Approved|Rejected, " +
            "message, rejectReason?}. Not: MerchantId/MerchantKey döndürmez — onaylanınca credential store'a sunucu-tarafı " +
            "güvenli callback ile otomatik gelir, elle giriş/aktarım gerekmez.";

        public const string StartOnboarding =
            "Yeni satıcı (merchant) kaydını başlatır: store sunucu-tarafı ödeme gateway'ine (DropShop) güvenli kayıt isteği " +
            "gönderir, yanıt yalnız durum bilgisidir (Pending). " +
            "\"Yeni satıcı kaydı başlat\", \"yeni merchant ekle\", \"satıcı onboarding başlat\" gibi istekler için. " +
            "email başvurunun kimliğidir — durum sorgusu aynı adresle yapılır (admin_onboarding_status). PG admini onaylayınca " +
            "MerchantId+MerchantKey store'a güvenli callback ile otomatik gelir. Not: TCKN/IBAN/MerchantKey gibi kimlik/finans/sır " +
            "bilgisi isteme ve sohbete yazma — bu değerler sohbete hiç girmez, sunucu-tarafı taşınır.";

        public const string ReissueMerchantKey =
            "Merchant MerchantKey'ini kaybettiğinde/sızdığında ödeme gateway'inde (DropShop) yeni key üretir; eski key her " +
            "temsilde anında geçersiz olur, yeni key store'a güvenli callback ile otomatik gelir. " +
            "\"Merchant key'ini yenile\", \"anahtarı sıfırla\" gibi istekler için. Store'un kayıtlı MerchantId'si kullanılır " +
            "(Id istenmez). reason opsiyonel (unuttum/sızıntı-şüphesi). Yanıt yalnız durum bilgisidir. Not: MerchantKey sohbete " +
            "hiç girmez — elle okuma/giriş adımı yoktur, sunucu-tarafı callback'le teslim edilir.";
    }

    public static class AuthTools
    {
        public const string Logout =
            "Kullanıcı çıkış yapmak/bağlantıyı kesmek istediğinde bu agent'ın mağaza erişim yetkisini iptal eder. " +
            "\"Çıkış yap\", \"bağlantıyı kes\", \"oturumu kapat\" gibi istekler için. Sonrasında işlem yapmak için yeniden " +
            "bağlantı ve onay gerekir. Yanıttaki 'message' alanını kullanıcıya olduğu gibi ilet.";
    }
}
