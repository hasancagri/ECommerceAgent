namespace ScoutAgent.Api.Search;

// 022: niyet-çıkarma system prompt'u (structured output → BookSearchFilters). Spike v2 (2026-11-24,
// canlı 3/3 tutarlı). Üç kural kritik: (1) UYDURMA YASAĞI, (2) yazarlık vs hakkında, (3) yayınevi tanıma.
// Program.cs'e gömülmez (conventions — veri kendi holder'ında). Model YALNIZ kullanıcının kelimelerini
// doğru slota koyar; çeviri/enum/sayı/id dönüşümü YOK (sunucu FilterInterpreter + Catalog yorumlar).
public static class ExtractPrompt
{
    public const string Instructions =
        "Kullanıcının kitap arama cümlesini analiz et, alanlara DAĞIT. Kullanıcının KENDİ kelimelerini " +
        "kullan — çeviri/enum/sayıya-dönüştürme/kanonikleştirme YOK (sunucu yapar). " +
        "⚠️ EN ÖNEMLİ KURAL — UYDURMA: Bir alanı YALNIZCA kullanıcı cümlesinde O ŞEY AÇIKÇA geçiyorsa " +
        "doldur. Cümlede olmayan sıralama/fiyat/stok/indirim/cilt ASLA ekleme — yoksa null/false bırak. " +
        "Örn. 'ucuzdan pahalıya' geçmiyorsa SortHint null; 'stokta' geçmiyorsa Flags.OnlyInStock false. " +
        "YAZARLIK vs HAKKINDA: 'X'in kitapları/eserleri/romanları', 'yazar X', 'X yazmış' → X bir YAZAR, " +
        "Text.Author'a yaz (Text.Query'ye KOYMA). 'X hakkında/üzerine/ile ilgili/X'i anlatan' → X bir " +
        "KONU, Text.Query'ye yaz, Text.Author BOŞ. Örn. 'Dostoyevski hakkında kitaplar' → Query='Dostoyevski', " +
        "Author=null. Örn. 'Mo Yan'ın kitapları' → Author='Mo Yan', Query=null. " +
        "YAYINEVİ: 'Yayınları/Yayınevi/Kültür/Kitap/Basım' içeren ya da bilinen yayınevi adları (Can, " +
        "İş Bankası, İş Kültür, Yapı Kredi/YKY, İletişim, Everest, İthaki, Doğan) → Publisher.Include. " +
        "'X hariç/dışında/olmayan' → Publisher.Exclude. Yayınevini Text.Query'ye KOYMA. Örn. 'Tolstoy'un " +
        "İş Kültür kitapları' → Author='Tolstoy', Publisher.Include='İş Kültür'. " +
        "DİĞER (metin aynen, çevirme): fiyat ifadesi ('ucuz','100-300 TL','300 altı') → PriceHint; " +
        "sıralama ('ucuzdan pahalıya','en çok satan','yeni') → SortHint; cilt ('ciltli'/'karton') → " +
        "BindingHint; sayfa ('kalın','kısa','300 sayfadan fazla') → PageHint. Tür/ruh-hali/konu kelimeleri " +
        "(roman, karanlık, distopya, öykü, çocuk, aşk) → Text.Query'de bırak. " +
        "BENZERLİK: 'X'e benzer', 'X gibi başka', 'X tarzında', 'X'i sevdim benzerini öner' → X bir ÇAPA " +
        "kitap adı, YALIN hâlini (ek'siz) SimilarToTitle'a yaz; Text ve diğer alanları BOŞ bırak. Örn. " +
        "'Anna Karenina'ya benzer kitaplar' → SimilarToTitle='Anna Karenina', Text=null. " +
        "SEMANTİK/BETİMLEYİCİ: kullanıcı başlık/yazar söylemeden kitabın RUHUNU/konusunu/hissini tarif " +
        "ediyorsa (ör. 'umut veren hafif bir yaz romanı', 'savaşın insanı nasıl değiştirdiğini anlatan', " +
        "'kayıp üstüne sakin bir kitap') → tüm betimlemeyi SemanticQuery'ye yaz, Text/yazar/yayınevi BOŞ. " +
        "Somut başlık ('Simyacı') ya da yazar ('Dostoyevski') VARSA SemanticQuery KULLANMA (Text/Author'a git). " +
        "Tek tür kelimesi ('distopya') belirsizse Text.Query'de kalır; cümle-boyu betimleme ise SemanticQuery. " +
        "KAVRAM/GELENEK: bir edebiyat geleneği/akımı/ülke-edebiyatı ya da geniş konu-kavramı ('Rus edebiyatı', " +
        "'Latin Amerika romanı', 'bilimkurgu klasikleri', 'stoacı felsefe', 'varoluşçu roman', 'Japon öyküsü') " +
        "→ bu KAVRAMI SemanticQuery'ye yaz (literal kelime eşleşmesi değil, ANLAM; başlıkta geçmeyen eserler de " +
        "gelsin), Text.Query'ye KOYMA. Somut yazar/başlık varsa yine Text/Author (kavram değil). " +
        "ÖNEMLİ: betimleyici cümledeki konu/ruh-hali kelimelerini Text.Query'ye AYIRMA/BÖLME — cümlenin " +
        "TAMAMI SemanticQuery'de kalsın, Text.Query null. İlgisiz tüm alanları null/false bırak.";
}
