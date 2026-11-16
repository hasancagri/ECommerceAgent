# Quickstart / Doğrulama: Arama Geçmişi (013)

**Ön koşul**: Sistemi Aspire AppHost'tan başlat; `identity-server` + `personalization-api` + `gateway` + `catalog-api` yeşil. React (`~/dev/ECommerceWeb`) `npm run dev`. Login ol (006 OIDC). 012'den birkaç arama yapılmış olmalı (geçmiş dolsun).

Gateway taban: `http://localhost:5060`. DB: `personalizationDb`, tablo `personalization.mt_doc_user_search_record` (parola: `docker exec <pg> printenv POSTGRES_PASSWORD`).

| Senaryo | Adım | Beklenen |
|---|---|---|
| **US1** görüntüle | Login'ken "Arama Geçmişim" sayfasını aç | Benzersiz sorgular, en yeni üstte, sayfalı; yalnız kendi geçmişin |
| **US1** boş-durum | Hiç araması olmayan kullanıcıyla aç | Boş-durum mesajı (hata/500 değil) |
| **US1** anonim | Token'sız `GET /api/personalization/v1/search-history` | **401** |
| **US1** izolasyon | A kullanıcısının token'ıyla çağır | Yalnız A'nın kayıtları; B'ninki ASLA |
| **US1** sayfalama | 20+ benzersiz sorgu, `?page=2` | 2. sayfa öğeleri; `totalCount` doğru |
| **US2** tekrar-ara | Geçmiş öğesine tıkla | Arama ekranına gider, o sorgu+mod çalışır, sonuç gösterilir |
| **US2** şişme yok | Tekrar-aramadan sonra geçmişi aç | Aynı sorgu YENİ öğe eklemez; en üste taşınır (recency) |
| **US3** tek sil | Bir öğeyi sil → DB kontrol | Öğe listeden kalkar; o sorgunun TÜM ham satırları gitmiş (hard) |
| **US3** tümünü temizle | "Tümünü temizle" + onayla | Kullanıcının tüm kayıtları gider; başka kullanıcı etkilenmez |
| **US3** geri gelmez | Silinen sorguyu yeniden ara | Yeni kayıt oluşur (eski geri gelmez) |

**Pür-unit doğrulama (İlke VI)**: `dotnet test tests/Personalization.Api.Tests` — `SearchHistoryProjectionTests`: dedup (tekrar→1), Türkçe-fold ("Dostoyevski"=="dostoyevski"), en-son-mod/zaman seçimi, sıralama.

**DB teyidi**:
```sql
-- silme sonrası o sorgu gitti mi (doğru schema+tablo adı!)
select data->>'Query', count(*) from personalization.mt_doc_user_search_record
where data->>'UserId' = '<sub>' group by 1 order by 2 desc;
```

Detaylar: [contracts/search-history-api.md](./contracts/search-history-api.md), [data-model.md](./data-model.md).
